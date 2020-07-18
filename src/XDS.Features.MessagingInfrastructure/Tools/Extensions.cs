using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using XDS.Features.MessagingInfrastructure.Infrastructure.Common.DTOs;
using XDS.Features.MessagingInfrastructure.Infrastructure.Common.Wallet;

namespace XDS.Features.MessagingInfrastructure.Tools
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

        public static bool HasCoinbaseMaturity(this TxType txType)
        {
            var type = (int)txType;
            return type > 0 && type < 30;
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
    }
}
