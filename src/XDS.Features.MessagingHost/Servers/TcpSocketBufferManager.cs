using System.Collections.Generic;
using System.Net.Sockets;

namespace XDS.Features.MessagingHost.Servers
{
	/// <summary>
	/// Reusable buffer to prevent native memory fragmentation,
	/// garbage collection etc. (Work in progress)
	/// </summary>
	sealed class TcpSocketBufferManager
	{
        readonly int bufferMemorySize;
        readonly int socketBufferSize;
        readonly Stack<int> unusedIndexes;

		byte[] bufferMemory;               
		int currentIndex;

		public TcpSocketBufferManager(int bufferMemorySize, int socketBufferSize)
		{
			this.bufferMemorySize = bufferMemorySize;
			this.currentIndex = 0;
			this.socketBufferSize = socketBufferSize;
			this.unusedIndexes = new Stack<int>();
		}

		public void InitBuffer()
		{
			// create one big large buffer and divide that 
			// out to each SocketAsyncEventArg object
			this.bufferMemory = new byte[this.bufferMemorySize];
		}

		// Assigns a buffer from the buffer pool to the 
		// specified SocketAsyncEventArgs object
		//
		// <returns>true if the buffer was successfully set, else false</returns>
		public bool SetBuffer(SocketAsyncEventArgs args)
		{

			if (this.unusedIndexes.Count > 0)
			{
				args.SetBuffer(this.bufferMemory, this.unusedIndexes.Pop(), this.socketBufferSize);
			}
			else
			{
				if ((this.bufferMemorySize - this.socketBufferSize) < this.currentIndex)
				{
					return false;
				}
				args.SetBuffer(this.bufferMemory, this.currentIndex, this.socketBufferSize);
				this.currentIndex += this.socketBufferSize;
			}
			return true;
		}

		// Removes the buffer from a SocketAsyncEventArg object.  
		// This frees the buffer back to the buffer pool
		public void FreeBuffer(SocketAsyncEventArgs args)
		{
			this.unusedIndexes.Push(args.Offset);
			args.SetBuffer(null, 0, 0);
		}
	}
}