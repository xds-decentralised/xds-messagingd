using System;
using System.Collections.Generic;
using System.Diagnostics;
using NBitcoin;
using XDS.Features.MessagingInfrastructure.Addresses;
using XDS.Features.MessagingInfrastructure.Model;
using XDS.Features.MessagingInfrastructure.Tools;
using Extensions = XDS.Features.MessagingInfrastructure.Tools.Extensions;

namespace XDS.Features.MessagingInfrastructure.Blockchain
{
    static class BlockService
    {
        public static void AnalyzeBlock(Block block, int blockHeight, Func<string, int, IndexEntry> getOrCreateAddressInIndex, Func<Hash256, int, IndexUtxo> findUtxo)
        {
            FindTransactions(block.Transactions, blockHeight, getOrCreateAddressInIndex, findUtxo);
        }

        static void FindTransactions(IEnumerable<Transaction> transactions, int blockHeight, Func<string, int, IndexEntry> getOrCreateAddressInIndex, Func<Hash256, int, IndexUtxo> findUtxo)
        {
            foreach (Transaction transaction in transactions)
            {
                AnalyzeTransaction(transaction, blockHeight, getOrCreateAddressInIndex, findUtxo);
            }
        }

        public static void AnalyzeTransaction(Transaction transaction, int blockHeight, Func<string, int, IndexEntry> getOrCreateAddressInIndex, Func<Hash256, int, IndexUtxo> findUtxo)
        {
            var spent = ExtractOutgoingFunds(blockHeight, transaction, findUtxo, out var amountSpent);
            if (spent != null)
            {
                foreach (var utxo in spent.Values)
                {
                    IndexEntry entry = getOrCreateAddressInIndex(utxo.Address, blockHeight);
                    entry.Spent.Add(utxo);
                }
            }


            var received = ExtractIncomingFunds(transaction, blockHeight, spent != null, getOrCreateAddressInIndex, out var amountReceived);
            if (received != null)
            {
                foreach (IndexUtxo utxo in received.Values)
                {
                    IndexEntry entry = getOrCreateAddressInIndex(utxo.Address, blockHeight);
                    entry.Received.Add(utxo);
                }
            }

            if (received == null && spent == null)
            {
                Debug.Assert(transaction.IsCoinBase);
            }
        }


        static Dictionary<string, IndexUtxo> ExtractOutgoingFunds(int blockHeight, Transaction transaction, Func<Hash256, int, IndexUtxo> findReceivedUtxo, out long amountSpent)
        {
            if (transaction.IsCoinBase)
            {
                amountSpent = 0;
                return null;
            }

            List<OutPoint> prevOuts = GetPrevOuts(transaction);
            Dictionary<string, IndexUtxo> spends = null;
            long sum = 0;

        // foreach (var b in findReceivedUtxo) // iterate ovr the large collection in outer loop (only once)
        //{
        findOutPointInBlock:
            foreach (OutPoint prevOut in prevOuts)
            {

                IndexUtxo prevWalletUtxo = findReceivedUtxo(prevOut.Hash.ToHash256(), (int)prevOut.N);  // do we have the indexed output?
                if (prevWalletUtxo == null)
                    throw new InvalidOperationException("For all utxos that are spent must exist a received utxo.");

                Extensions.NotNull(ref spends, transaction.Inputs.Count); // ensure the return collection is initialized

                // clone object, so that we don't change the other object because we add spending block height
                var spendAReceivedUtxo = new IndexUtxo(prevWalletUtxo.Address)
                {
                    BlockHeight = blockHeight, // use the current height, so that we can roll back after a reorg.
                    HashTx = prevWalletUtxo.HashTx,     // use the txid from the receive
                    UtxoType = prevWalletUtxo.UtxoType, // other props also from receive
                    Satoshis = prevWalletUtxo.Satoshis,
                    Index = prevWalletUtxo.Index,
                };

                spends.Add(spendAReceivedUtxo.ToString(), spendAReceivedUtxo);  // add the spend
                sum += spendAReceivedUtxo.Satoshis; // add amount

                if (spends.Count == transaction.Inputs.Count) // we will find no more spends than inputs, quick exit
                {
                    amountSpent = sum;
                    return spends;
                }

                prevOuts.Remove(prevOut); // do not search for this item any more
                goto findOutPointInBlock; // we need a new enumerator for the shortened collection
            }

            //  }
            amountSpent = sum;
            if (spends == null)
                ;
            return spends; // might be null or contain less then the tx inputs in edge cases, e.g. if an private key was removed from the wallet and no more items than the tx inputs.
        }

        public static long Burned;

        static Dictionary<string, IndexUtxo> ExtractIncomingFunds(Transaction transaction, int blockHeight, bool didSpend, Func<string, int, IndexEntry> getOrCreateAddressInIndex, out long amountReceived)
        {
            Dictionary<string, IndexUtxo> received = null;
            long sum = 0;
            int index = 0;

            foreach (var output in transaction.Outputs)
            {
                IndexEntry ownAddress = null;

                if (!output.IsProtocolOutput(transaction))
                {
                    var bech32 = output.ScriptPubKey.GetAddressFromScriptPubKey();
                    if (bech32 != null)
                    {
                        ownAddress = getOrCreateAddressInIndex(bech32, blockHeight);
                    }
                    else
                    {
                        Burned += output.Value.Satoshi;
                        ;// this must have been an op_return in a standard tx
                    }

                }


                if (ownAddress != null)
                {
                    Extensions.NotNull(ref received, transaction.Outputs.Count);

                    var item = new IndexUtxo(ownAddress.Address)
                    {
                        BlockHeight = blockHeight,
                        Address = ownAddress.Address,
                        HashTx = transaction.GetHash256(),
                        Satoshis = output.Value.Satoshi,
                        Index = index,
                        UtxoType = transaction.IsCoinBase ? UtxoType.Mined : transaction.IsCoinStake ? UtxoType.Staked : UtxoType.Received
                    };
                    received.Add(item.ToString(), item);
                    sum += item.Satoshis;
                }
                else
                {
                    if (!output.IsProtocolOutput(transaction))
                    {
                        Debug.Assert(false, "In the Indexer case, we'll never be here, because ownAddress is never null, except for protocol outputs.");
                    }

                    // For protocol tx, we are not interested in the other outputs.
                    // If we spent, the save the destinations, because the wallet wants to show where we sent coins to.
                    // if we did not spent, we do not save the destinations, because they are the other parties change address
                    // and we only received coins.
                    if (!transaction.IsCoinBase && !transaction.IsCoinStake && didSpend)
                    {
                        Debug.Assert(false);
                        //NotNull(ref notInWallet, transaction.Outputs.Count);
                        //var dest = new IndexUtxo()
                        //{
                        //    Address = output.ScriptPubKey.GetAddressFromScriptPubKey(),
                        //    HashTx = transaction.GetHash256(),
                        //    Satoshis = output.Value != null ? output.Value.Satoshi : 0,
                        //    Index = index
                        //};
                        //notInWallet.Add(dest.ToString(), dest);
                    }

                }
                index++;
            }



            amountReceived = sum;
            return received;
        }


        static List<OutPoint> GetPrevOuts(Transaction transaction)
        {
            var prevOuts = new List<OutPoint>(transaction.Inputs.Count);
            foreach (TxIn input in transaction.Inputs)
            {
                prevOuts.Add(input.PrevOut);
            }

            return prevOuts;
        }

     
    }
}
