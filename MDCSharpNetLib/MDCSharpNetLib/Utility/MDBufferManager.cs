using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Threading;

namespace MDCSharpNetLib.Utility
{
    internal class MDBufferManager
    {
        int TotalBufferSize;                 // the total number of bytes controlled by the buffer pool
        byte[] Buffer;                // the underlying byte array maintained by the Buffer Manager
        int CurrentIndex;
        int BufferSizeBySocketAsyncEventArgs;

        public MDBufferManager(int totalBytes, int bufferSize)
        {
            if ((totalBytes % bufferSize) != 0)
            {
                throw new System.ArgumentException("P(totalBytes % bufferSize) != 0", "bufferSize");
            }

            TotalBufferSize = totalBytes;
            CurrentIndex = 0;
            BufferSizeBySocketAsyncEventArgs = bufferSize;
        }

        public void InitBuffer()
        {
            Buffer = new byte[TotalBufferSize];
        }


        public bool SetBuffer(SocketAsyncEventArgs args)
        {
            if ((TotalBufferSize - BufferSizeBySocketAsyncEventArgs) < CurrentIndex)
            {
                return false;
            }

            args.SetBuffer(Buffer, CurrentIndex, BufferSizeBySocketAsyncEventArgs);
            CurrentIndex += BufferSizeBySocketAsyncEventArgs;

            return true;
        }
    }
}
