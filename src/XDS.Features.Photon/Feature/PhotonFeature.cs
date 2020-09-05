using System.Threading.Tasks;
using Blockcore;
using Blockcore.Builder.Feature;
using Blockcore.Interfaces;
using Blockcore.Utilities;

namespace XDS.Features.Photon.Feature
{
    /// <inheritdoc />
    public sealed class PhotonFeature : FullNodeFeature
    {
        readonly IFullNode fullNode;
        readonly IInitialBlockDownloadState initialBlockDownloadState;
        readonly BlockchainLookup blockchainLookup;


        public PhotonFeature(IFullNode fullNode, IInitialBlockDownloadState initialBlockDownloadState, INodeStats nodeStats, BlockchainLookup blockchainLookup)
        {
            this.fullNode = fullNode;
            this.initialBlockDownloadState = initialBlockDownloadState;
            this.blockchainLookup = blockchainLookup;

            nodeStats.RegisterStats(this.blockchainLookup.AddComponentStatsAsync, StatsType.Component, GetType().Name);
            nodeStats.RegisterStats(sb => { }, StatsType.Inline, GetType().Name, 800);
        }

        public override Task InitializeAsync()
        {
            // To implement a delayed init with Task.Delay, we need to wait on another thread, 
            // otherwise we'll block the whole node startup here.
            _ = Task.Run(async () =>
              {
                  while (this.fullNode.State != FullNodeState.Started)
                      await Task.Delay(100);

                  while (this.initialBlockDownloadState.IsInitialBlockDownload())
                      Task.Delay(1000).Wait();

                  this.blockchainLookup.Sync();
              });

            return Task.CompletedTask;
        }
    }
}
