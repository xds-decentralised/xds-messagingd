using System;
using System.Collections.Generic;
using System.Text;
using XDS.Features.MessagingInfrastructure.Infrastructure.Common.DTOs;

namespace XDS.Features.MessagingInfrastructure.Infrastructure.Common.Wallet
{
    public class BlockMetadata
    {
        public BlockMetadata()
        {
            this.Transactions = new HashSet<TransactionMetadata>();
        }

        public Hash256 HashBlock { get; set; }

        public HashSet<TransactionMetadata> Transactions { get; set; }
        public uint Time { get; set; }
    }
}
