using System.Collections.Generic;
using System.Runtime.Serialization;
using XDS.Features.MessagingInfrastructure.Model;

namespace XDS.Features.MessagingInfrastructure.Balances
{
    public sealed class IndexAddressBalance
    {
        public IndexAddressBalance(int height, string address)
        {
            this.Height = height;
            this.Address = address;
            this.SpendableCoins = new Dictionary<string, IndexUtxo>();
            this.StakingCoins = new Dictionary<string, IndexUtxo>();
        }

        public string Address { get; set; }

        /// <summary>
        /// The block height this Balance is valid for.
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Confirmed .
        /// </summary>
        public long Confirmed { get; set; }

        /// <summary>
        /// The amount that has enough confirmations to be already spendable.
        /// </summary>
        public long Spendable { get; set; }

        /// <summary>
        /// The amount that has enough confirmations for staking.
        /// </summary>
        public long Stakable { get; set; }

        /// <summary>
        /// Spendable outputs with the sum od Spendable.
        /// Key: HashTx-N.
        /// </summary>
        [IgnoreDataMember]
        public Dictionary<string, IndexUtxo> SpendableCoins { get; set; }

        /// <summary>
        /// Staking outputs with the sum od Stakable.
        /// Key: HashTx-N.
        /// </summary>
        [IgnoreDataMember]
        public Dictionary<string, IndexUtxo> StakingCoins { get; set; }

        /// <summary>
        /// Amount of utxos incl. Memory Pool regardless of being mature.
        /// </summary>
        public int TotalUnspentOutputCount { get; set; }

    }
}