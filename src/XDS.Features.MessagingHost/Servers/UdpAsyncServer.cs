using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using XDS.SDK.Messaging.CrossTierTypes;

namespace XDS.Features.MessagingHost.Servers
{
    public class UdpAsyncServer
    {
        readonly UdpClient udpClient;
        readonly IRequestHandler requestHandler;
        readonly IPEndPoint ipEndPoint;
        readonly ILogger logger;

        public UdpAsyncServer(IRequestHandler requestHandler, ILogger logger)
        {
            this.udpClient.Client.ReceiveBufferSize = SocketConfig.UdpReceiveBufferSize;
            this.ipEndPoint = new IPEndPoint(IPAddress.Any, SocketConfig.UdpServerPort);
            this.udpClient = new UdpClient(this.ipEndPoint);
            this.requestHandler = requestHandler;
            this.logger = logger;
            this.logger.LogInformation($"UdpServer started. Default ReceiveBuffer size is {this.udpClient.Client.ReceiveBufferSize}");
        }

        public async Task RunAsync(CancellationToken ct)
        {
            var stopwatch = new Stopwatch();
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var udpReceiveResult = await this.udpClient.ReceiveAsync();
                    stopwatch.Restart();

                    var logReceived = $"Received {udpReceiveResult.Buffer.Length} B from {udpReceiveResult.RemoteEndPoint.Address}:{udpReceiveResult.RemoteEndPoint.Port}, ";

                    var reply = await this.requestHandler.ProcessRequestAsync(udpReceiveResult.Buffer, "via UDP");
                    if (reply == null)
                    {
                        this.logger.LogInformation(logReceived + $" not replying, {stopwatch.ElapsedMilliseconds}ms.");
                        continue;
                    }

                    await Send(reply, udpReceiveResult.RemoteEndPoint);
                    this.logger.LogInformation(logReceived + $" replying w/ {reply.Length} B, {stopwatch.ElapsedMilliseconds}ms.");
                }
                catch (Exception e)
                {
                    this.logger.LogError("Run:" + e.Message);
                }
            }
        }

        async Task Send(byte[] reply, IPEndPoint remoteEndPoint)
        {
            int bytesSent = await this.udpClient.SendAsync(reply, reply.Length, remoteEndPoint);
        }
    }
}
