using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using NBitcoin;
using XDS.Features.MessagingInfrastructure.Addresses;
using XDS.Features.MessagingInfrastructure.Model;
using XDS.Features.MessagingInfrastructure.Tools;

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
        /// Total = Confirmed + Pending.
        /// </summary>
        public long Total { get; set; }

        /// <summary>
        /// Confirmed = TotalReceived - TotalSpent.
        /// </summary>
        public long Confirmed { get; set; }


        /// <summary>
        /// Pending = TotalReceivedPending - TotalSpentPending.
        /// </summary>
        public long Pending { get; set; }

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

        public long TotalReceived { get; set; }
        public long TotalSpent { get; set; }
        public long TotalReceivedPending { get; set; }
        public long TotalSpentPending { get; set; }
    }
    static class IndexBalanceService
    {
        static Network _network;

        public static void Init(Network network)
        {
            _network = network;
        }

        public static IndexAddressBalance GetBalance(HashSet<IndexEntry> indexEntries, int asOfBlockHeight, string address)
        {
            if (!indexEntries.TryGetValue(new IndexEntry(address) { Address = address }, out IndexEntry indexEntry))
                return null;

            var balance = new IndexAddressBalance(asOfBlockHeight, address);

            var spent = new Dictionary<string, IndexUtxo>();

            var totalUnspentOutputCount = 0;
          
            // process all confirmed transactions first, oldest to newest
            foreach(IndexUtxo utxo in indexEntry.Received)
            {
                if(utxo.BlockHeight > asOfBlockHeight)
                    continue;

                bool isImmatureForSpending = false;
                bool hasCoinbaseMaturity = utxo.UtxoType == UtxoType.Mined || utxo.UtxoType == UtxoType.Staked;
                if (hasCoinbaseMaturity)
                {
                    var confirmationsSpending = asOfBlockHeight - utxo.BlockHeight + 1; // if the tip is at 100 and my tx is height 90, it's 11 confirmations
                    isImmatureForSpending = confirmationsSpending < _network.Consensus.CoinbaseMaturity; // ok
                }
                var confirmationsStaking = asOfBlockHeight - utxo.BlockHeight + 1; // if the tip is at 100 and my tx is height 90, it's 11 confirmations
                var isImmatureForStaking = confirmationsStaking < _network.Consensus.MaxReorgLength;


                balance.TotalReceived += utxo.Satoshis;

                totalUnspentOutputCount++;


                if (!isImmatureForSpending)
                {
                    balance.Spendable += utxo.Satoshis;
                    balance.SpendableCoins.AddSafe(utxo.ToString(), utxo);
                }
                if (!isImmatureForStaking)
                {
                    balance.Stakable += utxo.Satoshis;
                    balance.StakingCoins.AddSafe(utxo.ToString(), utxo);
                }
            }

            if (indexEntry.Spent != null)
                foreach (IndexUtxo utxo in indexEntry.Spent)
                {

                    totalUnspentOutputCount--;
                    balance.TotalSpent += utxo.Satoshis;
                    spent.AddSafe(utxo.ToString(), utxo);
                }




            // remove what is already spent
            foreach (var utxoId in spent)
            {
                if (balance.SpendableCoins.ContainsKey(utxoId.Key))
                {
                    balance.Spendable -= utxoId.Value.Satoshis;
                    balance.SpendableCoins.Remove(utxoId.Key);
                }
                if (balance.StakingCoins.ContainsKey(utxoId.Key))
                {
                    balance.Stakable -= utxoId.Value.Satoshis;
                    balance.StakingCoins.Remove(utxoId.Key);
                }
            }

            // last balance updates
            balance.Confirmed = balance.TotalReceived - balance.TotalSpent;
            balance.Pending = balance.TotalReceivedPending - balance.TotalSpentPending;
            balance.Total = balance.Confirmed + balance.Pending;
            balance.TotalUnspentOutputCount = totalUnspentOutputCount;

            return balance;
        }
    }
}
