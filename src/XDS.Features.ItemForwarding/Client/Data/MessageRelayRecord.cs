using System;
using System.Net;
using XDS.SDK.Messaging.CrossTierTypes;

namespace XDS.Features.ItemForwarding.Client.Data
{
    public class MessageRelayRecord : IId
    {
        public string Id { get; set; }
        public IPAddress IpAddress { get; set; }
        public int MessagingPort { get; set; }

        /// <summary>
        /// Only create a MessageRelayRecord after one successful handshake.
        /// Update LastSeenUtc after a successful handshake.
        /// </summary>
        public DateTime LastSeenUtc { get; set; }

        /// <summary>
        /// Update this value after a handshake error, so that it can be used to calculate ErrorScore.
        /// </summary>
        public DateTime LastErrorUtc { get; set; }

        /// <summary>
        /// Set this to 0 after a successful handshake, i.e. when updating LastSeenUtc.
        /// For every unsuccessful connection attempt, increment this value:
        ///     ErrorScore = oldErrorScore + full hours after the last error + 1.
        /// Delete the whole MessageRelayRecord when LastSeenUtc is long ago and the ErrorScore
        /// reaches a certain level, so that it can be assumed the node is dead.
        /// </summary>
        public int ErrorScore { get; set; }

        public override string ToString()
        {
            return $"{this.IpAddress}:{this.MessagingPort} (LastSeenUtc: {this.LastErrorUtc} ErrorScore: {this.ErrorScore})";
        }
    }
}
