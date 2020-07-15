using System;
using System.Text;
using System.Threading.Tasks;
using Blockcore.Builder.Feature;
using Blockcore.Connection;
using Blockcore.P2P.Protocol.Payloads;
using Blockcore.Utilities;
using Microsoft.Extensions.Logging;
using XDS.Features.MessagingHost.Servers;
using XDS.Features.MessagingHost.Storage;

namespace XDS.Features.MessagingHost.Feature
{
    /// <inheritdoc />
    public sealed class MessagingHostFeature : FullNodeFeature
    {
        readonly IConnectionManager connectionManager;
        readonly ILogger logger;
        readonly TcpAsyncServer tcpAsyncServer;
        readonly IMessageNodeRepository messageNodeRepository;

        public MessagingHostFeature(IConnectionManager connectionManager, ILoggerFactory loggerFactory, TcpAsyncServer tcpAsyncServer, INodeStats nodeStats, IMessageNodeRepository messageNodeRepository)
        {
            this.connectionManager = connectionManager;
            this.tcpAsyncServer = tcpAsyncServer;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.messageNodeRepository = messageNodeRepository;

            nodeStats.RegisterStats(AddComponentStatsAsync, StatsType.Component, GetType().Name);
            nodeStats.RegisterStats(AddInlineStats, StatsType.Inline, GetType().Name, 800);
        }


        public override Task InitializeAsync()
        {
            AdvertiseMessagingServices();
            StartMessageRelayTcpSever();

            return Task.CompletedTask;
        }

        void StartMessageRelayTcpSever()
        {
            this.tcpAsyncServer.Run();
        }

        void AdvertiseMessagingServices()
        {
            this.connectionManager.Parameters.Services |= (NetworkPeerServices)MessagingFlags.Messaging;
        }

        static void AddInlineStats(StringBuilder log)
        {
        }

        async void AddComponentStatsAsync(StringBuilder log)
        {
            try
            {
                var stats = await this.messageNodeRepository.GetStatsAsync();
                if (stats != null)
                {
                    log.AppendLine();
                    log.AppendLine($"======= XDS Messaging (Port: {SocketConfig.TcpServerPort}), {TcpAsyncServer.NumConnectedSockets} connections =======");
                    log.AppendLine($"Locally known identities: {stats.IdentitiesCount}");
                    log.AppendLine($"Messages waiting for known identities: {stats.MessagesCount}");
                    log.AppendLine($"Resend Requests: {stats.ResendRequestsCount}");
                    log.AppendLine(
                        $"Total messages received/delivered: {stats.TotalMessagesReceived}/{stats.TotalMessagesDelivered}");
                    log.AppendLine($"Query Cost: {stats.Time} ms");
                }
            }
            catch (Exception e)
            {
                this.logger.LogError(e.Message);
            }
        }
    }
}
