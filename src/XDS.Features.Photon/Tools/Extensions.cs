using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using NBitcoin;
using XDS.Features.Photon.Feature;
using XDS.Features.Photon.Model;
using XDS.SDK.Cryptography;
using XDS.SDK.Cryptography.Api.Infrastructure;

namespace XDS.Features.Photon.Tools
{
    public static class Extensions
    {
        static ILogger _logger;

        public static void Init(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger(typeof(Extensions).FullName);
        }

        /// <summary>
        /// Checks is the block hash has a default value.
        /// </summary>
        /// <param name="hashBlock">block hash</param>
        /// <param name="genesisHash">hash of the genesis block of a network</param>
        /// <returns>true, if null, zero, or GenesisHash</returns>
        public static bool IsDefaultBlockHash(this Hash256 hashBlock, byte[] genesisHash)
        {
            if (hashBlock == null || hashBlock.Equals(new Hash256(genesisHash)) || hashBlock == Hash256.Zero)
                return true;
            return false;
        }

        /// <summary>
        /// // Pattern is: 1.0.*. The wildcard is: DateTime.Today.Subtract(new DateTime(2000, 1, 1)).Days;
        /// </summary>
        public static string GetShortVersionString(this Assembly assembly)
        {
            var version = assembly.GetName().Version;
            return $"{version.Major}.{version.Minor}.{version.Build}";
        }

       

        /// <summary>
        /// Adds the item to the dictionary by overwriting the old value, and logging the occurence of the duplicate key as error. 
        /// </summary>
        public static void AddSafe<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue value, [CallerMemberName] string memberName = "", [CallerLineNumber] int line = 0)
        {
            if (dictionary.ContainsKey(key))
                _logger.LogError($"The key '{key}' was already present in the dictionary. Method: {memberName}, Line: {line}");
            dictionary[key] = value;
        }

        public static void NotNull<T>(ref HashSet<T> list, int capacity)
        {
            if (list == null)
                list = new HashSet<T>(capacity);
        }

        public static void NotNull<K, T>(ref Dictionary<K, T> list, int capacity)
        {
            if (list == null)
                list = new Dictionary<K, T>(capacity);
        }

        public static void CheckBytes(byte[] bytes, int expectedLength)
        {
            if (bytes == null || bytes.Length != expectedLength || bytes.All(b => b == bytes[0]))
            {
                var display = bytes == null ? "null" : bytes.ToHexString();
                var message =
                    $"Suspicious byte array '{display}', it does not look like a cryptographic key or hash, please investigate. Expected lenght was {expectedLength}.";
                throw new PhotonException(HttpStatusCode.BadRequest, message);
            }
        }

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
