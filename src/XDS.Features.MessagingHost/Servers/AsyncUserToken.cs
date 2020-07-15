using System.Net.Sockets;
using XDS.SDK.Cryptography.NoTLS;

namespace XDS.Features.MessagingHost.Servers
{
	sealed class AsyncUserToken : EnvelopeReaderBuffer
	{
		internal Socket Socket;
	}
}