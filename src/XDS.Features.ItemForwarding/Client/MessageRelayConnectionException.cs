using System;

namespace XDS.Features.ItemForwarding.Client
{
    /// <summary>
    ///     This exception indicates an expected exception inside the network peer, which the ConnectedPeer class handles
    ///     mostly by itself.
    /// </summary>
    public class MessageRelayConnectionException : Exception
    {
        public MessageRelayConnectionException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public bool NoConnectionAvailable { get; internal set; }
    }
}