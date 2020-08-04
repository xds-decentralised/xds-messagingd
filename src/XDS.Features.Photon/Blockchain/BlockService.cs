using System;
using System.Collections.Generic;
using NBitcoin;
using XDS.Features.Photon.Addresses;
using XDS.Features.Photon.Model;
using XDS.Features.Photon.Tools;
using XDS.SDK.Messaging.CrossTierTypes.Photon;

namespace XDS.Features.Photon.Blockchain
{
    static class BlockService
    {
        public static void AnalyzeBlock(Block block, int blockHeight, Func<string, int, IndexEntry> getOrCreateAddressInIndex, Func<Hash256, int, IndexUtxo> findUnspentOutput)
        {
            FindTransactions(block.Transactions, blockHeight, getOrCreateAddressInIndex, findUnspentOutput);
        }

        static void FindTransactions(IEnumerable<Transaction> transactions, int blockHeight, Func<string, int, IndexEntry> getOrCreateAddressInIndex, Func<Hash256, int, IndexUtxo> findUnspentOutput)
        {
            foreach (Transaction transaction in transactions)
            {
                if (!transaction.IsCoinBase)
                {
                    ProcessSpends(blockHeight, transaction, findUnspentOutput);
                }

                ProcessReceives(transaction, blockHeight, getOrCreateAddressInIndex);
            }
        }

        static void ProcessSpends(int blockHeight, Transaction transaction, Func<Hash256, int, IndexUtxo> findUnspentOutput)
        {
            List<OutPoint> prevOuts = GetPrevOuts(transaction);

            var n = 0;
            var spendCount = 0;

        findOutPoint:
            foreach (OutPoint prevOut in prevOuts)
            {
                IndexUtxo unspentOutput = findUnspentOutput(prevOut.Hash.ToHash256(), (int)prevOut.N);

                if (unspentOutput == null)
                    throw new InvalidOperationException($"The unspent output for transaction {transaction} is supposed to exist.");

                if (unspentOutput.SpendingTx != null)
                    throw new InvalidOperationException($"The unspent output {unspentOutput} was already spent!");


                // spend the unspent
                unspentOutput.SpendingTx = transaction.GetHash256();
                unspentOutput.SpendingN = n;
                unspentOutput.SpendingHeight = blockHeight;

                spendCount++;
                n++;

                if (spendCount == transaction.Inputs.Count) // we will find no more spends than inputs, quick exit
                {
                    return;
                }

                prevOuts.Remove(prevOut); // do not search for this item any more
                goto findOutPoint; // we need a new enumerator for the shortened collection
            }

        }

        static void ProcessReceives(Transaction transaction, int blockHeight, Func<string, int, IndexEntry> getOrCreateAddressInIndex)
        {
            for (var index = 0; index < transaction.Outputs.Count; index++)
            {
                TxOut output = transaction.Outputs[index];

                if (output.IsProtocolOutput(transaction))
                    continue;

                // this returns 'unspendable' if it's not a valid ScriptPubKey
                var bech32 = output.ScriptPubKey.GetAddressFromScriptPubKey();

                IndexEntry indexEntry = getOrCreateAddressInIndex(bech32, blockHeight);

                var item = new IndexUtxo
                {
                    BlockHeight = blockHeight,
                    HashTx = transaction.GetHash256(),
                    Satoshis = output.Value.Satoshi,
                    Index = index,
                    UtxoType = transaction.IsCoinBase ? UtxoType.Mined : transaction.IsCoinStake ? UtxoType.Staked : UtxoType.Received
                };

                indexEntry.Received.Add(item);
            }
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
