using System;
using System.Collections.Generic;
using System.Text;

namespace MDCSharpNetLib.Network
{
    public interface IPacketDispatcher
    {
        void IncomingPacket(Session user, ArraySegment<byte> buffer);

        Queue<Packet> DispatchAll();
    }
}
