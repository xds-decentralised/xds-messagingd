using System;
using System.Collections.Generic;
using XDS.SDK.Messaging.CrossTierTypes.Photon;

namespace XDS.Features.Photon.Model
{


    public class IndexUtxo : IEquatable<IndexUtxo>
    {
        public int BlockHeight { get; set; }

        public int Index { get; set; }

        public Hash256 HashTx { get; set; }

        public long Satoshis { get; set; }

        public UtxoType UtxoType { get; set; }

        public Hash256 SpendingTx { get; set; }

        /// <summary>
        /// Value is only valid if SpendingTx is not null.
        /// </summary>
        public int SpendingN { get; set; }

        /// <summary>
        /// Value is only valid if SpendingTx is not null.
        /// </summary>
        public int SpendingHeight { get; set; }


        #region overrides of Equals, GetHashCode, ==, != (for use with HashSet<T>)

        public override bool Equals(object obj)
        {
            return Equals(obj as IndexUtxo);
        }

        public bool Equals(IndexUtxo other)
        {
            return other != null &&
                   this.ToString() == other.ToString();
        }

        public override int GetHashCode()
        {
            return this.ToString().GetHashCode();
        }

        public static bool operator ==(IndexUtxo left, IndexUtxo right)
        {
            return EqualityComparer<IndexUtxo>.Default.Equals(left, right);
        }

        public static bool operator !=(IndexUtxo left, IndexUtxo right)
        {
            return !(left == right);
        }

        #endregion

        public override string ToString()
        {
            return $"{this.HashTx}-{this.Index}";
        }
    }
}