using Microsoft.Extensions.Logging;
using XDS.Features.MessagingInfrastructure.Infrastructure.Common.DTOs;
using XDS.Features.MessagingInfrastructure.Infrastructure.Common.Wallet;
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

        public ISegWitAddress FindAddressInIndex(string bech32)
        {
            var equalValue = new IndexEntry
            {
                Address = bech32
            };

            this.xdsAddressIndex.Entries.TryGetValue(equalValue, out IndexEntry address);

            return address;
        }

        public ISegWitAddress GetOrAddAnyonesAddress(string bech32, int blockHeight)
        {
            var address = FindAddressInIndex(bech32) as IndexEntry;
            

            if (address != null)
            {
                address.LastSeenHeight = blockHeight;
            }
            else
            {
                if (bech32 == "unspendable")
                {
                    address = new IndexEntry
                    {
                        AddressType = AddressType.MatchAll,
                        ScriptPubKeyHex = null,
                        Address = bech32,
                        LastSeenHeight = blockHeight
                    };
                }
                else
                {
                    address = new IndexEntry
                    {
                        AddressType = AddressType.MatchAll,
                        ScriptPubKeyHex = bech32.GetScriptPubKey().ToHex(),
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
