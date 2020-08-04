using Microsoft.Extensions.Logging;
using XDS.Features.Photon.Model;
using XDS.Features.Photon.Tools;

namespace XDS.Features.Photon.Addresses
{
    sealed class AddressService
    {
        readonly AddressIndex addressIndex;
        readonly IndexFileHelper indexFileHelper;
        readonly ILogger logger;

        public AddressService(AddressIndex addressIndex, IndexFileHelper indexFileHelper, ILoggerFactory loggerFactory)
        {
            this.addressIndex = addressIndex;
            this.indexFileHelper = indexFileHelper;
            this.logger = loggerFactory.CreateLogger<AddressService>();
        }

        public IndexUtxo FindUtxo(Hash256 txId, int n)
        {
            var entries = this.addressIndex.Entries;
            foreach (var entry in entries)
            {
                foreach (var utxo in entry.Received)
                {
                    if (utxo.Index == n && utxo.HashTx == txId)
                        return utxo;
                }
            }

            return null;
        }

        public IndexEntry FindAddressInIndex(string bech32)
        {
            var equalValue = new IndexEntry(bech32)
            {
                Address = bech32
            };

            this.addressIndex.Entries.TryGetValue(equalValue, out IndexEntry address);

            return address;
        }

        public IndexEntry GetOrCreateAddressInIndex(string bech32, int blockHeight)
        {
            var address = FindAddressInIndex(bech32);
            

            if (address != null)
            {
                address.LastSeenHeight = blockHeight;
            }
            else
            {
                if (bech32 == "unspendable")
                {
                    address = new IndexEntry(bech32)
                    {
                        Address = bech32,
                        LastSeenHeight = blockHeight
                    };
                }
                else
                {
                    address = new IndexEntry(bech32)
                    {
                        Address = bech32,
                        LastSeenHeight = blockHeight
                    };
                }
                this.addressIndex.Entries.Add(address);
            }

            return address;
        }
    }
}
