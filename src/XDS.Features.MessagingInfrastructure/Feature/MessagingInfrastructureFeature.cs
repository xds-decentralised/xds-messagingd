using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Blockcore.Builder.Feature;
using Blockcore.Connection;
using Blockcore.P2P.Protocol.Payloads;
using Blockcore.Utilities;
using Microsoft.Extensions.Logging;

namespace XDS.Features.MessagingInfrastructure.Feature
{
    /// <inheritdoc />
    public sealed class MessagingInfrastructureFeature : FullNodeFeature
    {
        readonly IConnectionManager connectionManager;
        readonly ILogger logger;
        readonly BlockchainLookup blockchainLookup;

        public MessagingInfrastructureFeature(IConnectionManager connectionManager, ILoggerFactory loggerFactory, INodeStats nodeStats, BlockchainLookup blockchainLookup)
        {
            this.connectionManager = connectionManager;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            nodeStats.RegisterStats(AddComponentStatsAsync, StatsType.Component, GetType().Name);
            nodeStats.RegisterStats(AddInlineStats, StatsType.Inline, GetType().Name, 800);
            this.blockchainLookup = blockchainLookup;
        }


        public override Task InitializeAsync()
        {
            return Task.CompletedTask;
        }



        static void AddInlineStats(StringBuilder log)
        {
        }

        Stopwatch sw = new Stopwatch();
        int count;
        async void AddComponentStatsAsync(StringBuilder log)
        {
            try
            {
                lock (this.blockchainLookup.lockObj)
                {
                    log.AppendLine();
                    log.AppendLine($"======= XDS Blockchain lookup  =======");
                    log.AppendLine($"Synced Height: {this.blockchainLookup.GetSyncedHeight()}");
                    log.AppendLine($"Addresses: {this.blockchainLookup.GetAddressCount()}");
                    log.AppendLine($"Process Block Cost: {this.blockchainLookup.ProcessBlockMS} ms");
                    if (!this.blockchainLookup.IsStartingUp || this.count++ % 2 == 0)
                    {
                        this.sw.Restart();
                        log.AppendLine($"Network Balance: {this.blockchainLookup.GetNetworkBalance()}");
                        log.AppendLine($"Money Supply: {this.blockchainLookup.GetSyncedHeight() * 50}");
                        log.AppendLine($"Balance Query Cost: {this.sw.ElapsedMilliseconds} ms");
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
