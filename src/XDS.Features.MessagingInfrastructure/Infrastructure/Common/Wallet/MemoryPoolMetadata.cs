using System;
using System.Collections.Generic;

namespace XDS.Features.MessagingInfrastructure.Infrastructure.Common.Wallet
{
    public class MemoryPoolMetadata 
    {
        public HashSet<MemoryPoolEntry> Entries;
    }

    public class MemoryPoolEntry : IEquatable<MemoryPoolEntry>
    {
        public TransactionMetadata Transaction;

        public BroadcastState BroadcastState;

        public string ConsensusError;
        public string MemoryPoolError;
        public long TransactionTime;

        #region overrides of Equals, GetHashCode, ==, !=

        public override bool Equals(object obj)
        {
            return Equals(obj as MemoryPoolEntry);
        }

        public bool Equals(MemoryPoolEntry other)
        {
            return other != null &&
                   this.Transaction == other.Transaction;
        }

        public override int GetHashCode()
        {
            return -1052816746 + EqualityComparer<TransactionMetadata>.Default.GetHashCode(this.Transaction);
        }

        public static bool operator ==(MemoryPoolEntry left, MemoryPoolEntry right)
        {
            return EqualityComparer<MemoryPoolEntry>.Default.Equals(left, right);
        }

        public static bool operator !=(MemoryPoolEntry left, MemoryPoolEntry right)
        {
            return !(left == right);
        }

        #endregion
    }

    public enum BroadcastState
    {
        NotSet = 0,
        NotRequested = 1,
        ToBroadcast = 10,
        Broadcasted = 20,
        Propagated = 25,
        CantBroadcast = 50,
        
    }

    
}
