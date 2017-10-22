using System;
using System.Collections.Generic;
using System.Text;

namespace MDCSharpNetLib.Network
{
    public class ServerSession : Session
    {
        public ServerSession(Int64 uniqueId, IPacketDispatcher dispatcher) : base(uniqueId, dispatcher)
        {

        }
    }
}
