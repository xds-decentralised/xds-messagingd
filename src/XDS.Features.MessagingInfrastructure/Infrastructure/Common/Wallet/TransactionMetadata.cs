using System;
using System.Collections.Generic;
using XDS.Features.MessagingInfrastructure.Infrastructure.Common.DTOs;

namespace XDS.Features.MessagingInfrastructure.Infrastructure.Common.Wallet
{
    public sealed class TransactionMetadata : IEquatable<TransactionMetadata>
    {
        public long ValueAdded { get; set; }

        public TxType TxType { get; set; }

        public Hash256 HashTx { get; set; }

        public Dictionary<string, UtxoMetadata> Received { get; set; }

        public Dictionary<string, UtxoMetadata> Spent { get; set; }

        public Dictionary<string, UtxoMetadata> Destinations { get; set; }

        #region overrides of Equals, GetHashCode, ==, != (for use with HashSet<T>)

        public override bool Equals(object obj)
        {
            return Equals(obj as TransactionMetadata);
        }

        public bool Equals(TransactionMetadata other)
        {
            return other != null &&
                   this.HashTx == other.HashTx;
        }

        public override int GetHashCode()
        {
            return -1052816746 + this.HashTx.GetHashCode();
        }

        public static bool operator ==(TransactionMetadata left, TransactionMetadata right)
        {
            return EqualityComparer<TransactionMetadata>.Default.Equals(left, right);
        }

        public static bool operator !=(TransactionMetadata left, TransactionMetadata right)
        {
            return !(left == right);
        }

        #endregion
    }
}