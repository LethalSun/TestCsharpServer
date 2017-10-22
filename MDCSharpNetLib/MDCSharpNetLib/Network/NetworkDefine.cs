using System;
using System.Collections.Generic;
using System.Text;

namespace MDCSharpNetLib.Network
{
    public class NetworkDefine
    {
        public const short SYS_CLOSE_REQ = 0;

        public const short SYS_CLOSE_ACK = -1;

        public const short SYS_START_HEARTBEAT = -2;

        public const short SYS_UPDATE_HEARTBEAT = -3;
    }
}
