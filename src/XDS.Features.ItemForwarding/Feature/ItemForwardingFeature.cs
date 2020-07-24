using System;
using System.Text;
using System.Threading.Tasks;
using Blockcore.Builder.Feature;
using Blockcore.Connection;
using Blockcore.Utilities;
using Microsoft.Extensions.Logging;
using XDS.Features.ItemForwarding.Client;

namespace XDS.Features.ItemForwarding.Feature
{
    public class ItemForwardingFeature : FullNodeFeature
    {
        readonly IConnectionManager connectionManager;
        readonly ILogger logger;
        readonly ItemForwarding itemForwarding;

        public ItemForwardingFeature(IConnectionManager connectionManager, ILoggerFactory loggerFactory, INodeStats nodeStats, ItemForwarding itemForwarding)
        {
            this.connectionManager = connectionManager;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.itemForwarding = itemForwarding;

            nodeStats.RegisterStats(AddComponentStatsAsync, StatsType.Component, GetType().Name);
            nodeStats.RegisterStats(AddInlineStats, StatsType.Inline, GetType().Name, 800);
        }

        public override Task InitializeAsync()
        {
            this.itemForwarding.Start();
            return Task.CompletedTask;
        }
       

        void AddInlineStats(StringBuilder log)
        {

        }

        async void AddComponentStatsAsync(StringBuilder log)
        {
            try
            {
                log.AppendLine();
                log.AppendLine($"======= XDS Item Forwarding =======");

                var stats =this.itemForwarding.GetConnections();
                if (stats != null)
                {
                    foreach (var connection in stats)
                    {
                        log.AppendLine($"{connection.MessageRelayRecord.IpAddress}:{connection.MessageRelayRecord.MessagingPort} in/out {connection.BytesReceived}/{connection.BytesSent}bytes, {connection.ConnectionState}");
                    }
                   
                }
            }
            catch (Exception e)
            {
                this.logger.LogError(e.Message);
            }
        }
    }
}
