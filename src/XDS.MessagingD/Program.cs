using System;
using System.Linq;
using System.Threading.Tasks;
using Blockcore.Builder;
using Blockcore.Configuration;
using Blockcore.Features.NodeHost;
using Blockcore.Features.BlockStore;
using Blockcore.Features.ColdStaking;
using Blockcore.Features.Consensus;
using Blockcore.Features.Diagnostic;
using Blockcore.Features.MemoryPool;
using Blockcore.Features.Miner;
using Blockcore.Features.RPC;
using Blockcore.Networks.Xds;
using Blockcore.Utilities;
using XDS.Features.ItemForwarding.Feature;
using XDS.Features.MessagingHost.Feature;
using XDS.Features.Photon.Feature;

namespace XDS.MessagingD
{
    public class Program
    {
#pragma warning disable IDE1006 // Naming Styles

        public static async Task Main(string[] args)
#pragma warning restore IDE1006 // Naming Styles
        {
            // If users also run an XDS wallet on their subnet and the wallet is already connected to this node, they'll have difficulties
            // connecting to this node with the messaging client as well, because the clients also connect via the blockchain protocol
            // and not only via the messaging TCP port 38334. Therefore we tweak the settings here:
            var extArgs = args.ToList();
            extArgs.Add("-iprangefiltering=0");
            args = extArgs.ToArray();

            try
            {
                var nodeSettings = new NodeSettings(networksSelector: Networks.Xds, args: args, agent: "xds-mnd");

                IFullNodeBuilder nodeBuilder = new FullNodeBuilder()
                    .UseNodeSettings(nodeSettings)
                    .UseBlockStore()
                    .UsePosConsensus()
                    .UseMempool()
                    .UseColdStakingWallet()
                    .AddPowPosMining()
                    .UseNodeHost()
                    .AddRPC()
                    .UseDiagnosticFeature()
                    .UseMessagingHost()
                    .UsePhoton()
                    .UseItemForwarding();

                await nodeBuilder.Build().RunAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine($@"There was a problem initializing the node: '{e}'");
            }
        }
    }
}
