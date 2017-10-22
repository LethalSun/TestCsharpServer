using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using MDCSharpNetLib.Utility;

namespace MDCSharpNetLib.Network
{
    class DoubleBufferingQueue: ILogicQueue
    {
        // 실제 데이터가 들어갈 큐.
        Queue<Packet> Queue1;
        Queue<Packet> Queue2;

        // 각각의 큐에 대한 참조.
        Queue<Packet> RefInput;
        Queue<Packet> RefOutput;

        SpinLock LOCK = new SpinLock();

        public DoubleBufferingQueue()
        {
            Queue1 = new Queue<Packet>();
            Queue2 = new Queue<Packet>();
            RefInput = Queue1;
            RefOutput = Queue2;
        }


        public void Enqueue(Packet msg)
        {
            var gotLock = false;
            try
            {
                LOCK.Enter(ref gotLock);
                RefInput.Enqueue(msg);
            }
            finally
            {
                if (gotLock)
                {
                    LOCK.Exit();
                }
            }
        }


        public Queue<Packet> TakeAll()
        {
            swap();
            return RefOutput;
        }

        void swap()
        {
            var gotLock = false;
            try
            {
                LOCK.Enter(ref gotLock);

                var temp = RefInput;
                RefInput = RefOutput;
                RefOutput = temp;
            }
            finally
            {
                if (gotLock)
                {
                    LOCK.Exit();
                }
            }
        }
    }
}
