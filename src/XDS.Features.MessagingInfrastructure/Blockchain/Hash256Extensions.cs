using NBitcoin;
using XDS.Features.MessagingInfrastructure.Infrastructure.Common.DTOs;
using XDS.SDK.Cryptography.Api.Infrastructure;

namespace XDS.Features.MessagingInfrastructure.Blockchain
{
    public static class Hash256Extensions
    {
        public static bool IsEqualTo(this Hash256 hash256, uint256 uint256)
        {
            if (hash256 == null && uint256 == null)
                return true;

            if (hash256 != null && uint256 != null)
                return ByteArrays.AreAllBytesEqual(hash256.Value, uint256.ToBytes());
            return false;
        }

        public static Hash256 GetHash256(this Transaction transaction)
        {
            return new Hash256(transaction.GetHash().ToBytes());
        }

        public static Hash256 GetHash256(this Block block)
        {
            return new Hash256(block.GetHash().ToBytes());
        }

        public static Hash256 ToHash256(this uint256 uint256)
        {
            if (uint256 == null)
                return null;

            return new Hash256(uint256.ToBytes());
        }
    }
}
