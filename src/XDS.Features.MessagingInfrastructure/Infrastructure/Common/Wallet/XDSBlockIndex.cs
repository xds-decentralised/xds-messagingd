using System.Collections.Concurrent;
using XDS.Features.MessagingInfrastructure.Infrastructure.Common.DTOs;

namespace XDS.Features.MessagingInfrastructure.Infrastructure.Common.Wallet
{
    public sealed class XDSBlockIndex
    {
        public XDSBlockIndex()
        {
            this.Blocks = new ConcurrentDictionary<int, BlockMetadata>();
        }

        public int Version { get; set; }

        public long CreatedUtc { get; set; }

        public long ModifiedUtc { get; set; }

        /// <summary>
        /// The IndexIdentifier correlates the XDSAddressIndex and the XDSBlockIndex.
        /// </summary>
        public string IndexIdentifier { get; set; }

        /// <summary>
        /// The height of the last block that was synced.
        /// </summary>
        public int SyncedHeight { get; set; }

        /// <summary>
        /// The hash of the last block that was synced.
        /// </summary>
        public Hash256 SyncedHash { get; set; }

        public Hash256 CheckpointHash { get; set; }

        public int CheckpointHeight { get; set; }

        public ConcurrentDictionary<int, BlockMetadata> Blocks { get; set; }

    }
}