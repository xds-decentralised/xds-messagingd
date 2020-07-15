using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Blockcore.Utilities;
using Microsoft.Extensions.Logging;
using XDS.SDK.Cryptography.NoTLS;
using XDS.SDK.Messaging.CrossTierTypes;

namespace XDS.Features.MessagingHost.Servers
{
	public class TcpAsyncServer
	{
		readonly IRequestHandler requestHandler; // must be one instance if TLS is used - if we create one per request, the TLS ratchet is always blank.
		readonly CancellationToken cancellationToken;
		readonly Semaphore maxNumberAcceptedClients;
		readonly ILogger logger;

		Socket listenSocket;
		internal static int NumConnectedSockets;


		public TcpAsyncServer(INodeLifetime nodeLifetime, ILoggerFactory loggerFactory, IRequestHandler requestHandler)
		{
            this.requestHandler = requestHandler;
			this.maxNumberAcceptedClients = new Semaphore(SocketConfig.TcpMaxConnections, SocketConfig.TcpMaxConnections);
			this.cancellationToken = nodeLifetime.ApplicationStopping;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
		}

		public void Run()
		{
			var endPoint = new IPEndPoint(IPAddress.Any, SocketConfig.TcpServerPort);
			this.listenSocket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
			this.listenSocket.Bind(endPoint);
			this.listenSocket.Listen(SocketConfig.TcpListenerBacklog);
			StartAccept(null);
			this.logger.LogInformation($"The message relay TCP server started listening at port {SocketConfig.TcpServerPort}, max connections {SocketConfig.TcpMaxConnections}, backlog {SocketConfig.TcpListenerBacklog}.");
		}


		void StartAccept(SocketAsyncEventArgs acceptEventArg)
		{
			if (this.cancellationToken.IsCancellationRequested)
				return;

			if (acceptEventArg == null)
			{
				acceptEventArg = new SocketAsyncEventArgs();
				acceptEventArg.Completed += AcceptEventArg_Completed;
			}
			else
			{
				// socket must be cleared since the context object is being reused
				acceptEventArg.AcceptSocket = null;
			}

			this.maxNumberAcceptedClients.WaitOne();
			bool willRaiseEvent = this.listenSocket.AcceptAsync(acceptEventArg);

			if (!willRaiseEvent)
			{
				ProcessAccept(acceptEventArg);
			}
		}

		// This method is the callback method associated with Socket.AcceptAsync 
		// operations and is invoked when an accept operation is complete
		void AcceptEventArg_Completed(object sender, SocketAsyncEventArgs e)
		{
			ProcessAccept(e);
		}

		void ProcessAccept(SocketAsyncEventArgs e)
		{
			Interlocked.Increment(ref NumConnectedSockets);

			SocketAsyncEventArgs args = CreateArgs();
			((AsyncUserToken)args.UserToken).Socket = e.AcceptSocket;

            // as useful it is for debugging, we really should not log in a high performance server
            // in release builds, except errors or warnings.
#if DEBUG

			this.logger.LogInformation($"Connection accepted from {e.AcceptSocket.RemoteEndPoint}. {NumConnectedSockets} connected sockets.");
#endif

			// As soon as the client is connected, post a receive to the connection
			bool willRaiseEvent = e.AcceptSocket.ReceiveAsync(args);
			if (!willRaiseEvent)
			{
				ProcessReceive(args);
			}

			// Accept the next connection request
			StartAccept(e);
		}

		void IO_Completed(object sender, SocketAsyncEventArgs e)
		{
			// determine which type of operation just completed and call the associated handler
			switch (e.LastOperation)
			{
				case SocketAsyncOperation.Receive:
					ProcessReceive(e);
					break;
				case SocketAsyncOperation.Send:
					ProcessSend(e);
					break;
				default:
					throw new ArgumentException("The last operation completed on the socket was not a receive or send");
			}
		}

		// This method is invoked when an asynchronous receive operation (by the socket) completes. 
		// If the remote host already closed the connection, then the socket is already closed and we just decrement the counter.
		// If SocketError.Success, we process the client command and reply immediately (we always send an 'ack').
		void ProcessReceive(SocketAsyncEventArgs e)
		{
			AsyncUserToken token = (AsyncUserToken)e.UserToken;
			if (e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
			{
				try
				{
					NOTLSEnvelopeExtensions.UpdatePayload(e.BytesTransferred, token);

					do
					{
						NOTLSEnvelope packet = NOTLSEnvelopeExtensions.TryTakeOnePacket(ref token.Payload);
						if (packet == null) // null -> not yet complete
						{
							if (!token.Socket.ReceiveAsync(e))
								ProcessReceive(e);
							return;
						}

						var clientInformation = token.Socket.RemoteEndPoint.ToString();
						byte[] reply = this.requestHandler.ProcessRequestAsync(packet.Serialize(), clientInformation).Result;


						if (reply != null)
						{
							SocketAsyncEventArgs sendArgs = CreateArgs();
							((AsyncUserToken)sendArgs.UserToken).Socket = token.Socket;
							sendArgs.SetBuffer(reply, 0, reply.Length);
							if (!token.Socket.SendAsync(sendArgs))
							{
								ProcessSend(sendArgs);
							}
						}
					} while (token.Payload != null);
				}
				catch (Exception ex)
				{
					this.logger.LogError($"ProcessReceive - {ex}");
				}

			}
			else
			{
				CloseClientSocket(e);
			}
		}

		void ProcessSend(SocketAsyncEventArgs e)
		{
			if (e.SocketError == SocketError.Success)
			{
				// done writing to the client
				AsyncUserToken token = (AsyncUserToken)e.UserToken;

				SocketAsyncEventArgs readArgs = CreateArgs();
				((AsyncUserToken)readArgs.UserToken).Socket = token.Socket; // copy the _right_ socket

				// read the next message from the client
				bool willRaiseEvent = token.Socket.ReceiveAsync(readArgs);
				if (!willRaiseEvent)
				{
					ProcessReceive(readArgs);
				}
			}
			else
			{
				CloseClientSocket(e);
			}
		}

		void CloseClientSocket(SocketAsyncEventArgs e)
		{
			AsyncUserToken token = e?.UserToken as AsyncUserToken;

			try
			{
				if (token == null || token.Socket == null)
				{
#if DEBUG
					this.logger.LogInformation("CloseClientSocket - token or token.Socket was null. Doing nothing.");
#endif
					return;
				}
				try
				{
					token.Socket.Shutdown(SocketShutdown.Send);
				}
				catch (Exception ex) // throws if client process has already closed
				{
					this.logger.LogError($"CloseClientSocket - Socket.Shutdown(SocketShutdown.Send) - {ex.Message}");
				}
				try
				{
					token.Socket.Dispose();
				}
				catch (Exception ex)
				{
					this.logger.LogError($"CloseClientSocket - Socket.Dispose() - {ex.Message}");
				}
			}
			finally
			{
				// decrement connection counter
				Interlocked.Decrement(ref NumConnectedSockets);
				this.maxNumberAcceptedClients.Release();
			}
#if DEBUG
			this.logger.LogInformation($"Socket closed, {NumConnectedSockets} connected sockets.");
#endif
			// Once we use the buffer manager:
			// Free the SocketAsyncEventArg for reuse by another client
			// readWritePool.Push(e);
		}

		SocketAsyncEventArgs CreateArgs()
		{
			var args = new SocketAsyncEventArgs();
			args.Completed += IO_Completed;

            var token = new AsyncUserToken
            {
                Buffer = new byte[SocketConfig.TcpReaderBufferSize]
            };
            args.UserToken = token;
			args.SetBuffer(token.Buffer, 0, SocketConfig.TcpReaderBufferSize);
			return args;
		}
	}
}
