using System;
using System.Collections.Generic;
using System.Text;
using XDS.Features.MessagingInfrastructure.Addresses;

namespace XDS.Features.MessagingInfrastructure
{
    class Constants
    {
        /// <summary>
        /// A coin has 100_000_000 Satoshis.
        /// </summary>
        public const long SatoshisPerCoin = 100_000_000;

        /// <summary>
        /// The wallet key file version the code requires.
        /// When loading the file, and it has a lower version,
        /// it should be converted to this version.
        /// If the file has a higher version an error should
        /// be raised.
        /// </summary>
        public const int X1WalletFileVersion = 2;

        /// <summary>
        /// Length of a bech32 PubKeyHash address.
        /// </summary>
        public static int PubKeyHashAddressLength
        {
            get
            {
                // todo
                var rand = new Random();
                var buffer = new byte[20];
                rand.NextBytes(buffer);
                return buffer.ToPubKeyHashAddress().Length;
            }
        }

        /// <summary>
        /// Length of a bech32 Script address.
        /// </summary>
        public static int ScriptAddressLength
        {
            get
            {
                // todo
                var rand = new Random();
                var buffer = new byte[32];
                rand.NextBytes(buffer);
                return buffer.ToScriptAddress().Length;
            }
        }
    }
}
