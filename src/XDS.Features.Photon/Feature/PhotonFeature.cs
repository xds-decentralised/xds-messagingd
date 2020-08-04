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

        public override async Task InitializeAsync()
        {
            while (this.fullNode.State != FullNodeState.Started)
                await Task.Delay(100);

            while (this.initialBlockDownloadState.IsInitialBlockDownload())
                Task.Delay(1000).Wait();

            this.blockchainLookup.Sync();
        }
    }
}
