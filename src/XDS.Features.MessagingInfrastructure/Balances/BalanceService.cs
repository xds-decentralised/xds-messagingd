using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using XDS.Features.MessagingInfrastructure.Addresses;
using XDS.Features.MessagingInfrastructure.Infrastructure.Common.DTOs;
using XDS.Features.MessagingInfrastructure.Infrastructure.Common.Wallet;
using XDS.Features.MessagingInfrastructure.Tools;

namespace XDS.Features.MessagingInfrastructure.Balances
{
    static class BalanceService
    {
        static Network _network;

        public static void Init(Network network)
        {
            _network = network;
        }

        public static Balance GetBalance(ConcurrentDictionary<int, BlockMetadata> blocks, int syncedHeight, HashSet<MemoryPoolEntry> memoryPoolEntries, Func<string, ISegWitAddress> getOwnAddress,
            string matchAddress = null, AddressType matchAddressType = AddressType.MatchAll)
        {
            var balance = new Balance(syncedHeight);

            var spent = new Dictionary<string, UtxoMetadata>();

            var totalUnspentOutputCount = 0;

            // process all confirmed transactions first, oldest to newest
            foreach (var (height, block) in blocks)
            {
                foreach (var tx in block.Transactions)
                {
                    bool isImmatureForSpending = false;

                    if (tx.TxType.HasCoinbaseMaturity())
                    {
                        var confirmationsSpending = syncedHeight - height + 1; // if the tip is at 100 and my tx is height 90, it's 11 confirmations
                        isImmatureForSpending = confirmationsSpending < _network.Consensus.CoinbaseMaturity; // ok
                    }

                    var confirmationsStaking = syncedHeight - height + 1; // if the tip is at 100 and my tx is height 90, it's 11 confirmations
                    var isImmatureForStaking = confirmationsStaking < _network.Consensus.MaxReorgLength;

                    if (tx.Received != null)
                        foreach (UtxoMetadata utxo in tx.Received.Values)
                        {
                            for (var address = getOwnAddress(utxo.Address).Match(matchAddress, matchAddressType);
                                address != null; address = null)
                            {
                                balance.TotalReceived += utxo.Satoshis;

                                totalUnspentOutputCount++;

                                var coin = new SegWitCoin(address, utxo.HashTx, utxo.Index, utxo.Satoshis, utxo.UtxoType);

                                if (!isImmatureForSpending)
                                {
                                    balance.Spendable += utxo.Satoshis;
                                    balance.SpendableCoins.AddSafe(utxo.GetKey(), coin);
                                }
                                if (!isImmatureForStaking)
                                {
                                    balance.Stakable += utxo.Satoshis;
                                    balance.StakingCoins.AddSafe(utxo.GetKey(), coin);
                                }
                            }

                        }

                    if (tx.Spent != null)
                        foreach ((string txIdN, UtxoMetadata utxo) in tx.Spent)
                        {
                            for (var address = getOwnAddress(utxo.Address).Match(matchAddress, matchAddressType);
                                address != null;
                                address = null)
                            {
                                totalUnspentOutputCount--;
                                balance.TotalSpent += utxo.Satoshis;
                                spent.AddSafe(txIdN, utxo);
                            }
                        }

                }
            }

            // unconfirmed transactions - add them last, ordered by time, so that they come last in coin selection
            // when unconfirmed outputs get spent, to allow the memory pool and network to recognize 
            // the new unspent outputs.
            foreach (MemoryPoolEntry entry in memoryPoolEntries.OrderBy(x => x.TransactionTime))
            {
                var tx = entry.Transaction;
                if (tx.Received != null)
                    foreach (UtxoMetadata utxo in tx.Received.Values)
                    {
                        ISegWitAddress address = getOwnAddress(utxo.Address);

                        balance.TotalReceivedPending += utxo.Satoshis;

                        totalUnspentOutputCount++;

                        var coin = new SegWitCoin(address, utxo.HashTx, utxo.Index, utxo.Satoshis, utxo.UtxoType);
                        balance.SpendableCoins.AddSafe(utxo.GetKey(), coin);
                    }

                if (tx.Spent != null)
                    foreach (var (txIdN, utxo) in tx.Spent)
                    {
                        for (var address = getOwnAddress(utxo.Address).Match(matchAddress, matchAddressType);
                            address != null;
                            address = null)
                        {
                            totalUnspentOutputCount--;

                            balance.TotalSpent += utxo.Satoshis;
                            spent.AddSafe(txIdN, utxo);
                        }
                    }
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
