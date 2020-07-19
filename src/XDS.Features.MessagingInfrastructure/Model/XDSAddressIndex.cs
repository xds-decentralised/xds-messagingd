using System.Collections.Generic;

namespace XDS.Features.MessagingInfrastructure.Model
{
    public class XDSAddressIndex
    {
        public XDSAddressIndex()
        {
            this.Entries = new HashSet<IndexEntry>();
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

        public HashSet<IndexEntry> Entries { get; set; }
    }
}
