using System.Collections.Generic;

namespace XDS.Features.MessagingInfrastructure.Infrastructure.Common.DTOs
{
    public interface IAddress
    {
        /// <summary>
        /// The Bech32 representation of the address. Unique key of the address across all types of ISegWitAddress for dictionaries and
        /// comparisons. Use ordinal case sensitive comparison. Ensure the string is lowercase.
        /// </summary>
        string Address { get; }
    }

    public interface ISegWitAddress : IAddress
    {
       
        AddressType AddressType { get; }
        string ScriptPubKeyHex { get; }
        string Label { get; set; }

        byte[] GetEncryptedPrivateKey();

        int? LastSeenHeight { get; set; }
    }

    public interface ISegWitScriptAddress
    {
        string RedeemScriptHex { get; }
    }

    /// <summary>
    /// The AddressType defines what kind of address we are dealing with (e.g. P2WPK or what type
    /// of script address it is. Also, if the underlying key(s) are Hd, the enum values are used to define
    /// the Hd key path. See <see cref="KeyHelper.CreateDerivedPrivateKey"/> for the key path mappings.
    /// </summary>
    public enum AddressType : int
    {
        MatchAll = -10,

        PubKeyHash = 0,
        MultiSig = 10,
        ColdStakingCold = 30,
        ColdStakingHot = 35
    }

    public sealed class KeyMaterial
    {
        public KeyType KeyType;

        public string KeyPath;

        public int? AddressIndex;

        public int? IsChange;

        public long CreatedUtc;

        public byte[] CipherBytes;

        /// <summary>
        /// This field must only used in a wallet dump;
        /// </summary>
        public byte[] PlaintextBytes;

        public KeyMaterial Clone()
        {
            return new KeyMaterial
            {
                AddressIndex = this.AddressIndex,
                CipherBytes = this.CipherBytes,
                CreatedUtc = this.CreatedUtc,
                IsChange = this.IsChange,
                KeyPath = this.KeyPath,
                KeyType = this.KeyType,
                PlaintextBytes = null // do not include this
            };
        }
    }

    public enum KeyType
    {
        NotSet = 0,
        Hd = 10,
        Generated = 20,
        Imported = 30
    }

    public sealed class PubKeyHashAddress : ISegWitAddress
    {
        public string Address { get; set; }

        public AddressType AddressType { get; set; }

        public string ScriptPubKeyHex { get; set; }

        public string Label { get; set; }

        /// <summary>
        /// This property must only be SET while processing transactions from the blockchain.
        /// The presence of a valid value indicates that the address is a used address.
        /// </summary>
        public int? LastSeenHeight { get; set; }

        public KeyMaterial KeyMaterial;

        public byte[] GetEncryptedPrivateKey()
        {
            return this.KeyMaterial.CipherBytes;
        }

       
    }

    public sealed class MultiSigAddress : ISegWitAddress, ISegWitScriptAddress
    {
        public AddressType AddressType { get; set; }

        public string Address { get; set; }

        public string ScriptPubKeyHex { get; set; }

        public string Label { get; set; }

        public string RedeemScriptHex { get; set; }

        public int? LastSeenHeight { get; set; }

        public KeyMaterial OwnKey { get; set; }

        public int SignaturesRequired { get; set; }

        public int MaxSignatures { get; set; }

        /// <summary>
        /// Key: Compressed public key bytes as lowercase hex string.
        /// Value: Nickname of the owner of the public key for display.
        /// </summary>
        public Dictionary<string,string> OtherPublicKeys { get; set; }

        public byte[] GetEncryptedPrivateKey()
        {
            return this.OwnKey.CipherBytes;
        }

       
    }

    public class ColdStakingAddress : ISegWitAddress, ISegWitScriptAddress
    {
        public string Address { get; set; }

        public AddressType AddressType { get; set; }

        public string ScriptPubKeyHex { get; set; }

        public string Label { get; set; }

        public string RedeemScriptHex { get; set; }

        public int? LastSeenHeight { get; set; }

        public KeyMaterial ColdKey { get; set; }

        public KeyMaterial HotKey { get; set; }

        public byte[] StakingKey { get; set; }

        public byte[] GetEncryptedPrivateKey()
        {
            if (this.AddressType == AddressType.ColdStakingCold)
                return this.ColdKey.CipherBytes;
            return this.HotKey.CipherBytes;
        }

        public ColdStakingAddress Clone()
        {
            return new ColdStakingAddress
            {
                AddressType = this.AddressType,
                Address = this.Address,
                ColdKey = this.ColdKey.Clone(),
                HotKey = this.HotKey.Clone(),
                Label = this.Label,
                LastSeenHeight = this.LastSeenHeight,
                RedeemScriptHex = this.RedeemScriptHex,
                ScriptPubKeyHex = this.ScriptPubKeyHex,
                StakingKey = this.StakingKey

            };
        }
    }
}
