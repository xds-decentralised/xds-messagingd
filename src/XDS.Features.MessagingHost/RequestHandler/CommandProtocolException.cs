using System;

namespace XDS.Features.MessagingHost.RequestHandler
{
    public class CommandProtocolException : Exception
    {
        public CommandProtocolException(string message) : base(message)
        {
        }
    }
}
