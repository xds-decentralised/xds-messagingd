using System.Collections.Generic;
using System.Runtime.Serialization;

namespace XDS.Features.MessagingInfrastructure.Infrastructure.Common.DTOs
{
        public sealed class Balance
        {
            public Balance(int height)
            {
                this.Height = height;
                this.SpendableCoins = new Dictionary<string, SegWitCoin>();
                this.StakingCoins = new Dictionary<string, SegWitCoin>();
            }

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
            public Dictionary<string, SegWitCoin> SpendableCoins { get; set; }

            /// <summary>
            /// Staking outputs with the sum od Stakable.
            /// Key: HashTx-N.
            /// </summary>
            [IgnoreDataMember]
            public Dictionary<string, SegWitCoin> StakingCoins { get; set; }

            /// <summary>
            /// Amount of utxos incl. Memory Pool regardless of being mature.
            /// </summary>
            public int TotalUnspentOutputCount { get; set; }

            public long TotalReceived { get; set; }
            public long TotalSpent { get; set; }
            public long TotalReceivedPending { get; set; }
            public long TotalSpentPending { get; set; }
        }
}
