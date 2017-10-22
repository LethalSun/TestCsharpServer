using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using MDCSharpNetLib.Utility;
namespace MDCSharpNetLib.Network
{
    public class Session
    {
        enum State
        {
            Idle,

            Connected,

            ReserveClosing,

            Closed,
        }


        public Int64 UniqueId { get; private set; } = 0;


        private int IsClosed;

        State CurrentState;
        public Socket Sock { get; set; }

        public SocketAsyncEventArgs ReceiveEventArgs { get; private set; }
        public SocketAsyncEventArgs SendEventArgs { get; private set; }

        MessageResolver MsgResolver;

        IPeer Peer;

        List<ArraySegment<byte>> SendingList;

        private object cs_sending_queue;

        IPacketDispatcher Dispatcher;

        public Action<Session> OnSessionClosed;

        public long LatestHeartbeatTime { get; private set; }
        HeartbeatSender HeartbeatSender;
        bool AutoHeartbeat;


        public Session(Int64 uniqueId, IPacketDispatcher dispatcher)
        {
            UniqueId = uniqueId;
            Dispatcher = dispatcher;
            cs_sending_queue = new object();

            MsgResolver = new MessageResolver();
            Peer = null;
            SendingList = new List<ArraySegment<byte>>();
            LatestHeartbeatTime = DateTime.Now.Ticks;

            CurrentState = State.Idle;
        }

        public void OnConnected()
        {
            CurrentState = State.Connected;
            IsClosed = 0;
            AutoHeartbeat = true;
        }

        public void SetPeer(IPeer peer)
        {
            Peer = peer;
        }

        public void SetEventArgs(SocketAsyncEventArgs receive_event_args, SocketAsyncEventArgs send_event_args)
        {
            ReceiveEventArgs = receive_event_args;
            SendEventArgs = send_event_args;
        }

        public void OnReceive(byte[] buffer, int offset, int transfered)
        {
            MsgResolver.OnReceive(buffer, offset, transfered, OnMessageCompleted);
        }

        void OnMessageCompleted(ArraySegment<byte> buffer)
        {
            if (Peer == null)
            {
                return;
            } 

            Dispatcher.IncomingPacket(this, buffer);
        }

        public void Close()
        {
            if (Interlocked.CompareExchange(ref this.IsClosed, 1, 0) == 1)
            {
                return;
            }

            if (CurrentState == State.Closed)
            {
                return;
            }

            CurrentState = State.Closed;
            Sock.Close();
            Sock = null;

            SendEventArgs.UserToken = null;
            ReceiveEventArgs.UserToken = null;

            SendingList.Clear();
            MsgResolver.ClearBuffer();

            if (Peer != null)
            {
                var msg = Packet.Create((short)-1);
                Dispatcher.IncomingPacket(this, new ArraySegment<byte>(msg.Buffer, 0, msg.Position));
            }
        }

        public void PreSend(ArraySegment<byte> data)
        {
            lock (cs_sending_queue)
            {
                SendingList.Add(data);

                if (SendingList.Count > 1)
                {
                    return;
                }
            }

            StartSend();
        }


        public void Send(Packet msg)
        {
            msg.RecordSize();
            PreSend(new ArraySegment<byte>(msg.Buffer, 0, msg.Position));
        }

        void StartSend()
        {
            try
            {
                this.SendEventArgs.BufferList = this.SendingList;

                bool pending = this.Sock.SendAsync(this.SendEventArgs);
                if (!pending)
                {
                    ProcessSend(this.SendEventArgs);
                }
            }
            catch (Exception e)
            {
                if (this.Sock == null)
                {
                    Close();
                    return;
                }

                Console.WriteLine("send error!! close socket. " + e.Message);
                throw new Exception(e.Message, e);
            }
        }

        static object cs_count = new object();

        public void ProcessSend(SocketAsyncEventArgs e)
        {
            if (e.BytesTransferred <= 0 || e.SocketError != SocketError.Success)
            {
                return;
            }

            lock (this.cs_sending_queue)
            {
                var size = this.SendingList.Sum(obj => obj.Count);

                if (e.BytesTransferred != size)
                {
                    if (e.BytesTransferred < this.SendingList[0].Count)
                    {
                        string error = string.Format("Need to send more! transferred {0},  packet size {1}", e.BytesTransferred, size);
                        Console.WriteLine(error);

                        Close();
                        return;
                    }

                    int sent_index = 0;
                    int sum = 0;
                    for (int i = 0; i < this.SendingList.Count; ++i)
                    {
                        sum += this.SendingList[i].Count;
                        if (sum <= e.BytesTransferred)
                        {
                            sent_index = i;
                            continue;
                        }

                        break;
                    }
                    this.SendingList.RemoveRange(0, sent_index + 1);

                    StartSend();
                    return;
                }

                this.SendingList.Clear();

                if (this.CurrentState == State.ReserveClosing)
                {
                    this.Sock.Shutdown(SocketShutdown.Send);
                }
            }
        }

        public void DisConnect()
        {
            try
            {
                if (this.SendingList.Count <= 0)
                {
                    this.Sock.Shutdown(SocketShutdown.Send);
                    return;
                }

                this.CurrentState = State.ReserveClosing;
            }

            catch (Exception)
            {
                Close();
            }
        }

        public bool OnSystemPacket(Packet msg)
        {

            switch (msg.ProtocolId)
            {
                case NetworkDefine.SYS_CLOSE_REQ:
                    DisConnect();
                    return true;

                case NetworkDefine.SYS_START_HEARTBEAT:
                    {
                        msg.PopProtocolId();
                        byte interval = msg.PopByte();
                        this.HeartbeatSender = new HeartbeatSender(this, interval);

                        if (this.AutoHeartbeat)
                        {
                            StartHeartbeat();
                        }
                    }
                    return true;

                case NetworkDefine.SYS_UPDATE_HEARTBEAT:
                    this.LatestHeartbeatTime = DateTime.Now.Ticks;
                    return true;
            }


            if (Peer != null)
            {
                try
                {
                    if (msg.ProtocolId == NetworkDefine.SYS_CLOSE_ACK)
                    {
                        Peer.OnRemoved();

                        if (OnSessionClosed != null)
                        {
                            OnSessionClosed(this);
                        }

                        return true;
                    }
                }
                catch (Exception)
                {
                    Close();
                }
            }

            return false;
        }

        public bool IsConnected()
        {
            return CurrentState == State.Connected;
        }


        public void StartHeartbeat()
        {
            if (HeartbeatSender != null)
            {
                HeartbeatSender.Play();
            }
        }


        public void StopHeartbeat()
        {
            if (HeartbeatSender != null)
            {
                HeartbeatSender.Stop();
            }
        }


        public void DisableAutoHeartbeat()
        {
            StopHeartbeat();
            AutoHeartbeat = false;
        }


        public void UpdateHeartbeatManually(Int32 secondTime)
        {
            if (HeartbeatSender != null)
            {
                HeartbeatSender.Update(secondTime);
            }
        }
    }
}
