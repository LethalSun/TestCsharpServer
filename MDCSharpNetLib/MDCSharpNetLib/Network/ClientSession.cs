using System;
using System.Collections.Generic;
using System.Text;

namespace MDCSharpNetLib.Network
{
    class ClientSession : Session
    {
        public ClientSession(Int64 uniqueId, IPacketDispatcher dispatcher) : base(uniqueId, dispatcher)
        {
        }

        public void Ban()
        {
            try
            {
                byebye();
            }
            catch (Exception)
            {
                Close();
            }
        }

        void byebye()
        {
            Packet bye = Packet.Create(NetworkDefine.SYS_CLOSE_REQ);
            Send(bye);
        }
    }
}
