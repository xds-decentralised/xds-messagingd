using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Blockcore.EventBus;
using Blockcore.EventBus.CoreEvents.Peer;
using Blockcore.P2P;
using Blockcore.P2P.Protocol.Payloads;
using Blockcore.Signals;
using Microsoft.Extensions.Logging;
using XDS.Features.ItemForwarding.Client;
using XDS.SDK.Cryptography.NoTLS;
using XDS.SDK.Messaging.CrossTierTypes;
using XDS.SDK.Messaging.CrossTierTypes.BlockchainIntegration;

namespace XDS.Features.ItemForwarding
{
    public class ItemForwarding
    {
        readonly ILogger logger;
        readonly MessageRelayConnectionFactory messageRelayConnectionFactory;
        readonly SubscriptionToken peerMessageReceivedSubscriptionToken;
        readonly ISelfEndpointTracker selfEndpointTracker;

        HashSet<string> ownAddresses;

        public ItemForwarding(ILoggerFactory loggerFactory, MessageRelayConnectionFactory messageRelayConnectionFactory, ISignals signals, ISelfEndpointTracker selfEndpointTracker)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.messageRelayConnectionFactory = messageRelayConnectionFactory;
            this.peerMessageReceivedSubscriptionToken = signals.Subscribe<PeerMessageReceived>(OnPeerMessageReceived);
            this.selfEndpointTracker = selfEndpointTracker;
        }

        private void OnPeerMessageReceived(PeerMessageReceived pmr)
        {
            if (pmr.Message.Payload is VersionPayload versionPayload)
            {
                if (IsIPAddressSelf(pmr.PeerEndPoint))
                    return;

                XDSPeerServices xdsPeerServices = (XDSPeerServices)versionPayload.Services;

                if (xdsPeerServices.HasFlag(XDSPeerServices.MessageRelay))
                {
                    this.messageRelayConnectionFactory.ReceiveMessageRelayRecordAsync(pmr.PeerEndPoint.Address, pmr.PeerEndPoint.Port, xdsPeerServices, versionPayload.UserAgent);
                    this.logger.LogInformation($"Added MessageRelayRecord {pmr.PeerEndPoint.Address}:{pmr.PeerEndPoint.Port}, {versionPayload.UserAgent}, {xdsPeerServices}.");
                }
            }
        }

        bool IsIPAddressSelf(IPEndPoint endpoint)
        {
            if (this.selfEndpointTracker.IsSelf(endpoint) ||
                this.selfEndpointTracker.MyExternalAddress.Address.ToString() == endpoint.Address.ToString())
                return true;

            var moreOwnAddress = GetMoreOwnAddresses();
            var current = endpoint.Address.ToString();
            if (moreOwnAddress.Contains(current) || moreOwnAddress.Contains($"::ffff:{current}") || moreOwnAddress.Contains(current.Replace("::ffff:", "")))
                return true;

            return false;
        }


        /// <summary>
        /// https://stackoverflow.com/questions/6803073/get-local-ip-address
        /// </summary>
        HashSet<string> GetMoreOwnAddresses()
        {
            if (this.ownAddresses != null)
                return this.ownAddresses;

            var moreOwnAddresses = new HashSet<string>();

            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint endPoint = (IPEndPoint)socket.LocalEndPoint;
                moreOwnAddresses.Add(endPoint.Address.ToString());
            }

            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("127.0.0.1", 65530);
                IPEndPoint endPoint = (IPEndPoint)socket.LocalEndPoint;
                moreOwnAddresses.Add(endPoint.Address.ToString());
            }

            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("192.168.178.99", 65530);
                IPEndPoint endPoint = (IPEndPoint)socket.LocalEndPoint;
                moreOwnAddresses.Add(endPoint.Address.ToString());
            }


            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork || ip.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    moreOwnAddresses.Add(ip.ToString());
                }
            }

            this.ownAddresses = moreOwnAddresses;
            return moreOwnAddresses;
        }

        internal void Start()
        {
            this.logger.LogInformation($"{nameof(ItemForwarding)} is starting.");
            this.messageRelayConnectionFactory.IsIPAddressSelf = IsIPAddressSelf;
            var _ = this.messageRelayConnectionFactory.ConnectAsync(default, default);
        }

        /// <summary>
        /// Try forwarding the item to as many peers as possible.
        /// This method should not block or throw exceptions, because
        /// messaging clients would deadlock waiting for a reply then.
        /// </summary>
        /// <param name="command"></param>
        public void PushAndForget(Command command)
        {
            _ = Task.Run(async () =>
              {
                  try
                  {
                      this.logger.LogInformation($"Pushing a {command.CommandId} payload, {command.CommandData.Length} bytes.");
                      var resultEnvelopes = await this.messageRelayConnectionFactory.SendRequestAsync(command.CommandData);
                      foreach (IEnvelope envelope in resultEnvelopes)
                      {
                          this.logger.LogInformation($"Received response of {envelope.EncipheredPayload.Length} bytes.");
                      }
                  }
                  catch (Exception e)
                  {
                      this.logger.LogError(e.Message);
                  }
              });
        }

        public MessageRelayConnection[] GetConnections()
        {
            return this.messageRelayConnectionFactory.GetAllConnections();
        }
    }
}
