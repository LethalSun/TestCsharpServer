using System;
using System.Collections.Generic;
using System.Text;
using MDCSharpNetLib.Utility;

namespace MDCSharpNetLib.Network
{
    class DefaultPacketDispatcher : IPacketDispatcher
    {
        ILogicQueue MessageQueue = new DoubleBufferingQueue();


        public DefaultPacketDispatcher()
        {
        }


        public void IncomingPacket(Session user, ArraySegment<byte> buffer)
        {

            Packet msg = new Packet(buffer, user);
            MessageQueue.Enqueue(msg);
        }

        public Queue<Packet> DispatchAll()
        {
            return MessageQueue.TakeAll();
        }
    }
}
