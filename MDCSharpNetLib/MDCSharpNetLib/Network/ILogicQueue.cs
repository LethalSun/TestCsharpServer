using System;
using System.Collections.Generic;
using System.Text;

namespace MDCSharpNetLib.Network
{
    public interface ILogicQueue
    {
        void Enqueue(Packet msg);

        Queue<Packet> TakeAll();
    }
}
