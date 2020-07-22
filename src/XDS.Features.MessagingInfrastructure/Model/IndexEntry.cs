using System;
using System.Collections.Generic;

namespace XDS.Features.MessagingInfrastructure.Model
{
    public class IndexEntry : IEquatable<IndexEntry>
    {
        public IndexEntry(string address)
        {
            if(address == null)
                throw new ArgumentNullException(nameof(address));

            this.Received = new HashSet<IndexUtxo>();
        }

        /// <summary>
        /// Address is the unique key of this item, and GetHashCode() uses it as well.
        /// </summary>
        public string Address { get; set; }

        public HashSet<IndexUtxo> Received { get; set; }

        public int? LastSeenHeight { get; set; }

        #region overrides of Equals, GetHashCode, ==, != (for use with HashSet<T>)

        public override bool Equals(object obj)
        {
            return Equals(obj as IndexEntry);
        }

        public bool Equals(IndexEntry other)
        {
            return other != null &&
                   this.Address == other.Address;
        }

        public override int GetHashCode()
        {
            return this.Address.GetHashCode();
        }

        public static bool operator ==(IndexEntry left, IndexEntry right)
        {
            return EqualityComparer<IndexEntry>.Default.Equals(left, right);
        }

        public static bool operator !=(IndexEntry left, IndexEntry right)
        {
            return !(left == right);
        }

        #endregion

        public override string ToString()
        {
            return this.Address;
        }
       
    }
}