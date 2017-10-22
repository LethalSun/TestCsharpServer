using System;
using System.Collections.Generic;
using System.Text;

namespace MDCSharpNetLib.Utility
{
    struct Defines
    {
        public static readonly short HEADERSIZE = 4;
    }

    class MessageResolver
    {
        int MessageSize = 0;

        byte[] MessageBuffer = new byte[1024];

        int CurrentPosition = 0;

        int PositionToRead = 0;

        int RemainBytes = 0;


        public MessageResolver() { }

        bool ReadUntil(byte[] buffer, ref int srPosition)
        {
            var copySize = PositionToRead - CurrentPosition;

            if (RemainBytes < copySize)
            {
                copySize = RemainBytes;
            }

            Array.Copy(buffer, srPosition, MessageBuffer, CurrentPosition, copySize);

            srPosition += copySize;

            CurrentPosition += copySize;

            RemainBytes -= copySize;

            if (CurrentPosition < PositionToRead)
            {
                return false;
            }

            return true;
        }

        public void OnReceive(byte[] buffer, int offset, int transffered, Action<ArraySegment<byte>> callback)
        {
            RemainBytes = transffered;

            var src_position = offset;

            while (RemainBytes > 0)
            {
                var completed = false;

                if (CurrentPosition < Defines.HEADERSIZE)
                {
                    PositionToRead = Defines.HEADERSIZE;

                    completed = ReadUntil(buffer, ref src_position);
                    if (!completed)
                    {
                        return;
                    }

                    MessageSize = GetTotalMessageSize();

                    if (MessageSize <= 0)
                    {
                        ClearBuffer();
                        return;
                    }

                    PositionToRead = MessageSize;

                    if (RemainBytes <= 0)
                    {
                        return;
                    }
                }

                completed = ReadUntil(buffer, ref src_position);

                if (completed)
                {
                    var clone = new byte[this.PositionToRead];
                    Array.Copy(this.MessageBuffer, clone, PositionToRead);
                    ClearBuffer();
                    callback(new ArraySegment<byte>(clone, 0, PositionToRead));
                }
            }
        }

        int GetTotalMessageSize()
        {
            if (Defines.HEADERSIZE == 2)
            {
                return BitConverter.ToInt16(MessageBuffer, 0);
            }
            else if (Defines.HEADERSIZE == 4)
            {
                return BitConverter.ToInt32(MessageBuffer, 0);
            }

            return 0;
        }

        public void ClearBuffer()
        {
            Array.Clear(MessageBuffer, 0, MessageBuffer.Length);

            CurrentPosition = 0;
            MessageSize = 0;

        }
    }
}
