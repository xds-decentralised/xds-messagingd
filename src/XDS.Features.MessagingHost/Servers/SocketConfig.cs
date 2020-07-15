namespace XDS.Features.MessagingHost.Servers
{
    static class SocketConfig
    {
        public const int UdpServerPort = 38335;
        public const int UdpReceiveBufferSize = 1024*1024*100;

        public const int TcpServerPort = 38334;
        public const int TcpListenerBacklog = 100;
        public const ushort TcpReaderBufferSize = 4096*2;
	    public const int TcpMaxConnections = 1000;
    }
}
