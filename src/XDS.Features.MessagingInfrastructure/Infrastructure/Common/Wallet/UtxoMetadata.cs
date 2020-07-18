using XDS.Features.MessagingInfrastructure.Infrastructure.Common.DTOs;

namespace XDS.Features.MessagingInfrastructure.Infrastructure.Common.Wallet
{
    public class UtxoMetadata
    {
        public string Address { get; set; }

        public Hash256 HashTx { get; set; }

        public int Index { get; set; }

        public long Satoshis { get; set; }

        public UtxoType UtxoType { get; set; }

        public string GetKey()
        {
            return $"{this.HashTx}-{this.Index}";
        }
    }

    public enum UtxoType
    {
        NotSet = 0,
        Mined = 1,
        Staked = 2,
        Received = 3
    }
}