using System;
using System.Collections.Generic;
using XDS.Features.MessagingInfrastructure.Infrastructure.Common.DTOs;

namespace XDS.Features.MessagingInfrastructure.Infrastructure.Common.Wallet
{
    public class XDSAddressIndex
    {
        public XDSAddressIndex()
        {
            this.Entries = new HashSet<IndexEntry>();
        }

        public string IndexIdentifier { get; set; }

        public HashSet<IndexEntry> Entries { get; set; }
    }

    public class IndexEntry : IEquatable<IndexEntry>, ISegWitAddress
    {
        public IndexEntry()
        {
            this.Received = new HashSet<IndexUtxo>();
            this.Spent = new HashSet<IndexUtxo>();
        }

        public string Address { get; set; }

        public HashSet<IndexUtxo> Received { get; set; }

        public HashSet<IndexUtxo> Spent { get; set; }

        public AddressType AddressType { get; set; }

        public string ScriptPubKeyHex { get; set; }

        public string Label { get; set; }

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
            return $"{nameof(IndexEntry)}: {this.Address}";
        }

        public byte[] GetEncryptedPrivateKey()
        {
            throw new NotImplementedException();
        }
    }


    public class IndexUtxo : IEquatable<IndexUtxo>
    {
        public int BlockHeight { get; set; }

        public int Index { get; set; }

        public Hash256 HashTx { get; set; }

        public long Satoshis { get; set; }

        public UtxoType UtxoType { get; set; }

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
