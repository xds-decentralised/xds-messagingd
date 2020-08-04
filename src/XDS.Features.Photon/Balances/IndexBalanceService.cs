using System.Collections.Generic;
using NBitcoin;
using XDS.Features.Photon.Model;
using XDS.Features.Photon.Tools;
using XDS.SDK.Messaging.CrossTierTypes.Photon;

namespace XDS.Features.Photon.Balances
{
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

            foreach(IndexUtxo utxo in indexEntry.Received)
            {
                if(utxo.BlockHeight > asOfBlockHeight)
                    continue;

                if(utxo.SpendingTx != null)
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

                balance.Confirmed += utxo.Satoshis;
                balance.TotalUnspentOutputCount++;


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
            
            return balance;
        }
    }
}
