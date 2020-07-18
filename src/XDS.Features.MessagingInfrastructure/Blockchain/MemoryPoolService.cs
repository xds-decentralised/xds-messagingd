using System;
using System.Collections.Generic;
using System.Text;
using XDS.Features.MessagingInfrastructure.Infrastructure.Common.DTOs;
using XDS.Features.MessagingInfrastructure.Infrastructure.Common.Wallet;

namespace XDS.Features.MessagingInfrastructure.Blockchain
{
    static class MemoryPoolService
    {
        public static MemoryPoolEntry GetMemoryPoolEntry(Hash256 hashTx, HashSet<MemoryPoolEntry> memoryPoolEntries)
        {
            memoryPoolEntries.TryGetValue(new MemoryPoolEntry { Transaction = new TransactionMetadata { HashTx = hashTx } },
                out var existingEntry);
            return existingEntry;
        }

        //public static MemoryPoolEntry CreateMemoryPoolEntry(TransactionMetadata walletTransaction, TransactionBroadcastEntry broadcastEntry)
        //{
        //    var entry = new MemoryPoolEntry
        //    {
        //        Transaction = walletTransaction,
        //        TransactionTime = DateTime.UtcNow.ToUnixTime()
        //    };

        //    if (broadcastEntry != null)
        //    {
        //        entry.BroadcastState = broadcastEntry.State.ToBroadcastState();
        //        entry.MemoryPoolError = broadcastEntry.MempoolError.GetMemoryPoolError();
        //        entry.ConsensusError = broadcastEntry.MempoolError.GetMemoryPoolError();
        //    }

        //    return entry;
        //}

        //public static void UpdateMemoryPoolEntry(MemoryPoolEntry memoryPoolEntry, TransactionBroadcastEntry broadcastEntry)
        //{
        //    var newState = broadcastEntry.State.ToBroadcastState();
        //    var newErrorM = broadcastEntry.MempoolError.GetMemoryPoolError();
        //    var newErrorC = broadcastEntry.MempoolError.GetMemoryPoolConsensusError();

        //    var sb = new StringBuilder();
        //    sb.AppendLine();
        //    if (newState != memoryPoolEntry.BroadcastState)
        //        sb.AppendLine($"BroadcastState {memoryPoolEntry.BroadcastState} -> {newState}");
        //    if (newErrorM != memoryPoolEntry.MemoryPoolError)
        //        sb.AppendLine($"MemoryPoolError {memoryPoolEntry.MemoryPoolError} -> {newErrorM}");
        //    if (newErrorC != memoryPoolEntry.ConsensusError)
        //        sb.AppendLine($"MemoryPoolError {memoryPoolEntry.ConsensusError} -> {newErrorC}");

        //   this.logger.LogDebug($"Updating Tracked tx {memoryPoolEntry.Transaction.HashTx},  changes: {sb}");

        //    memoryPoolEntry.BroadcastState = newState;
        //    memoryPoolEntry.MemoryPoolError = newErrorM;
        //    memoryPoolEntry.ConsensusError = newErrorC;
        //}
    }
}
