using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Blockcore.Utilities;
using Microsoft.Extensions.Logging;
using XDS.Features.ItemForwarding.Client.Data;
using XDS.SDK.Cryptography.NoTLS;
using XDS.SDK.Messaging.CrossTierTypes;
using XDS.SDK.Messaging.CrossTierTypes.BlockchainIntegration;

namespace XDS.Features.ItemForwarding.Client
{
    public class MessageRelayConnectionFactory : ITcpConnection
    {
        const int DefaultMessagingPort = 38334;

        readonly ILogger logger;
        readonly INodeLifetime nodeLifetime;
        readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
        readonly Random random = new Random();

        readonly MessageRelayRecordRepository messageRelayRecords;

        ConcurrentDictionary<string, MessageRelayConnection> connections;

        Task connectTask;

        public MessageRelayConnectionFactory(ILoggerFactory loggerFactory, INodeLifetime nodeLifetime, MessageRelayRecordRepository messageRelayRecords)
        {
            this.messageRelayRecords = messageRelayRecords;
            this.connections = new ConcurrentDictionary<string, MessageRelayConnection>();
            this.nodeLifetime = nodeLifetime;
            this.logger = loggerFactory.CreateLogger<MessageRelayConnectionFactory>();
        }


        public bool IsConnected
        {
            get { return this.connections.Count > 0; }
        }

        public Func<IPEndPoint, bool> IsIPAddressSelf { get; internal set; }
        public Func<Task<IReadOnlyList<XIdentity>>> GetAllIdentities { get; set; }

        public async Task<bool> ConnectAsync(string remoteDnsHost, int remotePort, Func<byte[], Transport, Task<string>> receiver = null)
        {
            if (this.connectTask != null)
                return this.connections.Count > 0;

            this.connectTask = Task.Run(MaintainConnectionsAsync);
            return false;
        }


        public async Task MaintainConnectionsAsync()
        {

            while (!this.nodeLifetime.ApplicationStopping.IsCancellationRequested)
            {
                while (this.connections.Count < 32 && !this.nodeLifetime.ApplicationStopping.IsCancellationRequested)
                {
                    var connection = await SelectNextConnectionAsync(); // this method must block and only run on one thread, it's not thread safe

                    if (connection == null)
                    {
                        this.logger.LogInformation("Out off connection candidates...");
                        break;
                    }

                    _ = Task.Run(() => ConnectAndRun(connection)); // Connecting and running happens on max X threads, so that we can build up connections quickly
                }

                this.logger.LogInformation("Waiting 30 seconds..");
                await Task.Delay(30000);
            }
        }

        public async Task<MessageRelayConnection> SelectNextConnectionAsync()
        {
            IReadOnlyList<MessageRelayRecord> allRecords = await this.messageRelayRecords.GetAllMessageRelayRecordsAsync();


            if (allRecords.Count == 0)
            {
                this.logger.LogWarning("No relay addresses in the store - can't select a connection candidate!");
                return null;
            }

            // peers, shortest success ago first
            var hottestRecords = allRecords
                .Where(x => !IsExcluded(x)) // e.g. if we are already connected, or the last error is less than a certain time ago
                .OrderBy(x => DateTimeOffset.UtcNow - x.LastSeenUtc) // then, try the best candidates first
                .ThenByDescending(x => DateTimeOffset.UtcNow - x.LastErrorUtc) // then, try the candidates where the last error is longest ago
                .ToList();

            if (hottestRecords.Count == 0)
            {
                this.logger.LogDebug("After applying the filtering rules, no connection candidates remain for selection.");
                return null;
            }

            MessageRelayRecord messageRelayRecord = hottestRecords[0];
            this.logger.LogDebug($"Selected connection candidate {messageRelayRecord}, last seen {DateTimeOffset.UtcNow - messageRelayRecord.LastSeenUtc} ago, last error {DateTimeOffset.UtcNow - messageRelayRecord.LastErrorUtc} ago.");
            var messageRelayConnection = new MessageRelayConnection(messageRelayRecord, this.nodeLifetime.ApplicationStopping);
            bool addSuccess = this.connections.TryAdd(messageRelayRecord.Id, messageRelayConnection);
            Debug.Assert(addSuccess, $"Bug: Peer {messageRelayRecord} was already in the ConnectedPeers dictionary - that should not happen.");
            return messageRelayConnection;
        }

        bool IsExcluded(MessageRelayRecord relay)
        {
            if (this.connections.ContainsKey(relay.Id))
            {
                this.logger.LogDebug($"Peer {relay} is excluded, because it's in the list of connected peers.");
                return true;
            }

            if (this.IsIPAddressSelf(new IPEndPoint(relay.IpAddress, 0)))
                return true;

            if (relay.LastErrorUtc != default)
            {
                var timeSinceLastError = DateTimeOffset.UtcNow - relay.LastErrorUtc;
                if (timeSinceLastError <= TimeSpan.FromSeconds(60))
                {
                    this.logger.LogDebug(
                        $"Peer (MessageRelay) {relay} is excluded, because it's last error is only {timeSinceLastError} ago.");
                    return true;
                }
            }

            return false;
        }

        public async Task ConnectAndRun(MessageRelayConnection createdInstance)
        {
            var connectedInstance = await CreateConnectedPeerBlockingOrThrowAsync(createdInstance);

            if (connectedInstance != null)
            {
                this.logger.LogInformation(
                    $"Successfully created connected peer {createdInstance}, loading off to new thread.");
                var identities = await this.GetAllIdentities();

                foreach (var identity in identities)
                {
                    var requestCommand =
                        new RequestCommand(CommandId.PublishIdentity, identity).Serialize(CommandHeader.Yes);
                    try
                    {
                        await this.semaphore.WaitAsync();
                        await connectedInstance.SendAsync(requestCommand);
                        var _ = await connectedInstance.ReceiveAsync();
                    }
                    catch (Exception e)
                    {
                        this.logger.LogError(
                            $"Error while attempting to push {identities.Count} identities to peer {connectedInstance}: {e.Message}");
                    }
                    finally
                    {
                        this.semaphore.Release();
                    }
                }


                //await RunNetworkPeer(createdInstance);
            }
        }

        async Task<MessageRelayConnection> CreateConnectedPeerBlockingOrThrowAsync(MessageRelayConnection connection)
        {
            try
            {
                connection.ConnectionState |= XDSPeerState.Connecting;
                await connection.ConnectAsync();
                connection.ConnectionState &= ~XDSPeerState.Connecting;
                connection.ConnectionState |= XDSPeerState.Connected;

                return connection;
            }
            catch (Exception e)
            {
                await HandleFailedConnectedPeerAsync(e, connection);
                return null;
            }
        }

        async Task HandleFailedConnectedPeerAsync(Exception e, MessageRelayConnection connection)
        {
            if (connection == null)  // there was no connection available
                return;

            Debug.Assert(connection.ConnectionState.HasFlag(XDSPeerState.Failed));
            Debug.Assert(connection.ConnectionState.HasFlag(XDSPeerState.Disposed));

            if (ShouldRecordError(e, this.nodeLifetime.ApplicationStopping.IsCancellationRequested, connection.ToString(), this.logger))
            {
                // set these properties on the loaded connection instance and not only in the repository, so that we can
                // use the cached collection of Peer objects
                connection.MessageRelayRecord.LastErrorUtc = DateTime.UtcNow;
                await this.messageRelayRecords.UpdatePeerLastError(connection.MessageRelayRecord.Id,
                    connection.MessageRelayRecord.LastErrorUtc);
            }

            this.connections.TryRemove(connection.MessageRelayRecord.Id, out _);
        }

        public static bool ShouldRecordError(Exception e, bool isCancelled, string connectionDescription, ILogger logger)
        {
            if (isCancelled) // the app is closing, no error
                return false;

            if (e is MessageRelayConnectionException cpe)
            {
                if (cpe.InnerException is SocketException se)
                {
                    if (se.ErrorCode == 10051) // we have lost our internet connection, the peer was good, no error
                        return false;

                    if (se.ErrorCode == 10060)
                    {
                        var message =
                            $"Marking {connectionDescription} as bad, did not respond ({10060})"; // 'this' is the remote address
                        logger.LogInformation(message);
                        return true;
                    }

                    if (se.ErrorCode == 10061)
                    {
                        var message = $"Marking {connectionDescription} as bad, refused the connection ({10061})";
                        logger.LogInformation(message);
                        return true;
                    }
                }

                if (cpe.InnerException is IOException _) return false; // this is also the internet connection's fault
            }
            else
            {
                return
                    false; // the error has nothing to do with the socket, we do not want to mark the peer as bad in that case either
            }

            var m = $"Marking {connectionDescription} as bad, error: {e.Message}";
            logger.LogInformation(m);
            return false; // in case of doubt, we record the error - this should probably be refined.
        }

        public async Task DisconnectAsync()
        {
            await Task.CompletedTask;
        }



        public async Task<List<IEnvelope>> SendRequestAsync(byte[] request)
        {
            var response = new List<IEnvelope>();

            try
            {
                await this.semaphore.WaitAsync(); // this definitely deadlocks sometimes

                var activeConnections = GetAllActiveConnections();
                if (activeConnections.Length == 0)
                    throw new MessageRelayConnectionException("No connection(s) available, please retry later.", null) { NoConnectionAvailable = true };

                foreach (var conn in activeConnections)
                {
                    try
                    {
                        await conn.SendAsync(request);
                        response.AddRange(await conn.ReceiveAsync());
                    }
                    catch (Exception e)
                    {
                        await HandleFailedConnectedPeerAsync(e, conn);
                    }
                }
            }
            finally
            {
                this.semaphore.Release();
            }
            return response;
        }

        public MessageRelayConnection[] GetAllConnections()
        {
            return this.connections.Values.ToArray();
        }

        public MessageRelayConnection[] GetAllActiveConnections()
        {
            return this.connections.Values.Where(x => x.ConnectionState.HasFlag(XDSPeerState.Connected)).ToArray();
        }

        public async void ReceiveMessageRelayRecordAsync(IPAddress ipAddress, int port, XDSPeerServices peerServices, string userAgent)
        {
            try
            {
                if (peerServices.HasFlag(XDSPeerServices.MessageRelay))
                {
                    var messageRelayRecord = new MessageRelayRecord
                    {
                        Id = ipAddress.CreatePeerId(DefaultMessagingPort),
                        // pretend we have had a successful connection already (it is likely, since we have just had a version handshake via the protocol port
                        LastSeenUtc = DateTime.UtcNow,
                        LastErrorUtc = default,
                        ErrorScore = 0
                    };
                    await this.messageRelayRecords.AddMessageNodeRecordAsync(messageRelayRecord);
                }
            }
            catch (Exception e)
            {
                this.logger.LogError(e.Message);
            }

        }
    }
}
