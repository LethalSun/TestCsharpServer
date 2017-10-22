using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using MDCSharpNetLib.Utility;

namespace MDCSharpNetLib.Network
{
    class MDNetworkLib
    {
        //비동기 통신을 할 때 쓸 객체를 관리하는 풀
        MDSocketAsyncEventPool ReceiveEventArgsPool;
        MDSocketAsyncEventPool SendEventArgsPool;

        //각각의 세션이 접속을 했을때 
        public Action<ClientSession> SessionClientCreatedCallBack = null;
        public Action<ServerSession> SessionServerCreatedCallBack = null;

        public IPacketDispatcher PacketDispatcher { get; private set; }
        public UserTokenManager UserManager { get; private set; }

        Int64 SequenceId = 0;

        public MDNetworkLib(IPacketDispatcher userPacketDispatcher = null)
        {
            UserManager = new UserTokenManager();

            if (userPacketDispatcher == null)
            {
                PacketDispatcher = new DefaultPacketDispatcher();
            }
            else
            {
                PacketDispatcher = userPacketDispatcher;
            }
        }

        public void Initialize(ServerOption serverOption)
        {

            int pre_alloc_count = 1;
            //
            var totalBytes = serverOption.MaxConnectionCount * serverOption.ReceiveBufferSize * pre_alloc_count;
            MDBufferManager buffer_manager = new MDBufferManager(totalBytes, serverOption.ReceiveBufferSize);

            buffer_manager.InitBuffer();


            ReceiveEventArgsPool = new MDSocketAsyncEventPool();
            SendEventArgsPool = new MDSocketAsyncEventPool();

            SocketAsyncEventArgs arg;

            for (int i = 0; i < serverOption.MaxConnectionCount; i++)
            {

                {

                    arg = new SocketAsyncEventArgs();
                    arg.Completed += new EventHandler<SocketAsyncEventArgs>(ReceiveCompleted);
                    arg.UserToken = null;

                    buffer_manager.SetBuffer(arg);

                    ReceiveEventArgsPool.Push(arg);
                }



                {

                    arg = new SocketAsyncEventArgs();
                    arg.Completed += new EventHandler<SocketAsyncEventArgs>(SendCompleted);
                    arg.UserToken = null;

                    arg.SetBuffer(null, 0, 0);

                    SendEventArgsPool.Push(arg);
                }
            }
        }

        public void Listen(string host, int port, int backlog, MDSocketOption socketOption)
        {
            MDListener client_listener = new MDListener();
            client_listener.OnNewClientCallback += OnNewClient;
            client_listener.Start(host, port, backlog, socketOption);

            byte check_interval = 10;
            UserManager.StartHeartbeatChecking(check_interval, check_interval);
        }

        public void OnConnectCompleted(Socket socket, Session token)
        {
            token.OnSessionClosed += this.OnSessionClosed;
            UserManager.Add(token);

            SocketAsyncEventArgs receive_event_arg = new SocketAsyncEventArgs();
            receive_event_arg.Completed += new EventHandler<SocketAsyncEventArgs>(ReceiveCompleted);
            receive_event_arg.UserToken = token;
            receive_event_arg.SetBuffer(new byte[1024], 0, 1024);

            SocketAsyncEventArgs send_event_arg = new SocketAsyncEventArgs();
            send_event_arg.Completed += new EventHandler<SocketAsyncEventArgs>(SendCompleted);
            send_event_arg.UserToken = token;
            send_event_arg.SetBuffer(null, 0, 0);

            BeginReceive(socket, receive_event_arg, send_event_arg);
        }

        void OnNewClient(Socket client_socket, object token)
        {
            // UserToken은 매번 새로 생성하여 깨끗한 인스턴스로 넣어준다.
            var uniqueId = Interlocked.Increment(ref SequenceId);

            var user_token = new ClientSession(uniqueId, PacketDispatcher);
            user_token.OnSessionClosed += this.OnSessionClosed;


            UserManager.Add(user_token);

            // 플에서 하나 꺼내와 사용한다.
            SocketAsyncEventArgs receive_args = this.ReceiveEventArgsPool.Pop();
            SocketAsyncEventArgs send_args = this.SendEventArgsPool.Pop();

            receive_args.UserToken = user_token;
            send_args.UserToken = user_token;


            user_token.OnConnected();

            SessionClientCreatedCallBack?.Invoke(user_token);

            BeginReceive(client_socket, receive_args, send_args);

            Packet msg = Packet.Create((short)NetworkDefine.SYS_START_HEARTBEAT);
            var send_interval = (byte)5;
            msg.Push(send_interval);
            user_token.Send(msg);
        }

        void BeginReceive(Socket socket, SocketAsyncEventArgs receive_args, SocketAsyncEventArgs send_args)
        {
            // receive_args, send_args 아무곳에서나 꺼내와도 된다. 둘다 동일한 CUserToken을 물고 있다.
            Session token = receive_args.UserToken as Session;
            token.SetEventArgs(receive_args, send_args);
            // 생성된 클라이언트 소켓을 보관해 놓고 통신할 때 사용한다.
            token.Sock = socket;

            bool pending = socket.ReceiveAsync(receive_args);
            if (!pending)
            {
                ProcessReceive(receive_args);
            }
        }

        void ReceiveCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (e.LastOperation == SocketAsyncOperation.Receive)
            {
                ProcessReceive(e);
                return;
            }

            throw new ArgumentException("The last operation completed on the socket was not a receive.");
        }

        void SendCompleted(object sender, SocketAsyncEventArgs e)
        {
            try
            {
                Session token = e.UserToken as Session;
                token.ProcessSend(e);
            }
            catch (Exception)
            {
            }
        }

        private void ProcessReceive(SocketAsyncEventArgs e)
        {
            Session token = e.UserToken as Session;
            if (e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
            {
                token.OnReceive(e.Buffer, e.Offset, e.BytesTransferred);

                bool pending = token.Sock.ReceiveAsync(e);
                if (!pending)
                {
                    ProcessReceive(e);
                }
            }
            else
            {
                try
                {
                    token.Close();
                }
                catch (Exception)
                {
                    Console.WriteLine("Already closed this socket.");
                }
            }
        }

        void OnSessionClosed(Session token)
        {
            UserManager.Remove(token);

            if (ReceiveEventArgsPool != null)
            {
                ReceiveEventArgsPool.Push(token.ReceiveEventArgs);
            }

            if (SendEventArgsPool != null)
            {
                SendEventArgsPool.Push(token.SendEventArgs);
            }

            token.SetEventArgs(null, null);
        }
    }
}
