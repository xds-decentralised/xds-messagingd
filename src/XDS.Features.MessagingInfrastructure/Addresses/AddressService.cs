using Microsoft.Extensions.Logging;
using XDS.Features.MessagingInfrastructure.Model;
using XDS.Features.MessagingInfrastructure.Tools;

namespace XDS.Features.MessagingInfrastructure.Addresses
{
    sealed class AddressService
    {
        readonly XDSAddressIndex xdsAddressIndex;
        readonly IndexFileHelper indexFileHelper;
        readonly ILogger logger;

        public AddressService(XDSAddressIndex xdsAddressIndex, IndexFileHelper indexFileHelper, ILoggerFactory loggerFactory)
        {
            this.xdsAddressIndex = xdsAddressIndex;
            this.indexFileHelper = indexFileHelper;
            this.logger = loggerFactory.CreateLogger<AddressService>();
        }

        public IndexUtxo FindUtxo(Hash256 txId, int n)
        {
            var entries = this.xdsAddressIndex.Entries;
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

            this.xdsAddressIndex.Entries.TryGetValue(equalValue, out IndexEntry address);

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
                this.xdsAddressIndex.Entries.Add(address);
            }

            return address;
        }
    }
}
