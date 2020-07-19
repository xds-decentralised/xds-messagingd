using System;
using System.Collections.Generic;

namespace XDS.Features.MessagingInfrastructure.Model
{
    public class IndexUtxo : IEquatable<IndexUtxo>
    {
        public IndexUtxo(string address)
        {
            if(address == null)
                throw new ArgumentNullException();
            this.Address = address;
        }

        public string Address { get; set; }

        public int BlockHeight { get; set; }

        public int Index { get; set; }

        public Hash256 HashTx { get; set; }

        public long Satoshis { get; set; }

        public UtxoType UtxoType { get; set; }

        public Hash256 SpendingTx { get; set; }

        public int SpendingN { get; set; }

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