using System;
using System.Runtime.Serialization;

namespace XDS.Features.MessagingInfrastructure.Infrastructure.Common.DTOs
{
    public class XDSIndexerAddress : ISegWitAddress
    {
        [DataMember(Name = "A")]
        public string Address { get; set; }

        [DataMember(Name = "T")]
        public AddressType AddressType { get; set; }

        [DataMember(Name = "S")]
        public string ScriptPubKeyHex { get; set; }

        [DataMember(Name = "L")]
        public int? LastSeenHeight { get; set; }

        [IgnoreDataMember]
        public string Label { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public byte[] GetEncryptedPrivateKey()
        {
            throw new NotSupportedException();
        }
    }
}
