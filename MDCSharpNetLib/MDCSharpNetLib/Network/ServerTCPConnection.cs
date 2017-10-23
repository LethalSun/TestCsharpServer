using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace MDCSharpNetLib.Network
{
    //서버대 서버연결에 대응하는 객체
    class ServerTCPConnection
    {
        public Action<Session> ConnectedCallback = null;

        Socket ClientSocket;

        MDNetworkLib RefNetworkService;


        public ServerTCPConnection(MDNetworkLib networkService)
        {
            RefNetworkService = networkService;
        }

        public void Connect(IPEndPoint remoteEndpoint, MDSocketOption socketOption)
        {
            ClientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            ClientSocket.NoDelay = socketOption.NoDelay;

            SocketAsyncEventArgs event_arg = new SocketAsyncEventArgs();
            event_arg.Completed += OnConnectCompleted;
            event_arg.RemoteEndPoint = remoteEndpoint;


            bool pending = ClientSocket.ConnectAsync(event_arg);

            if (pending == false)
            {
                OnConnectCompleted(null, event_arg);
            }
        }

        void OnConnectCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError == SocketError.Success)
            {
                Session token = new Session(1, RefNetworkService.PacketDispatcher);

                RefNetworkService.OnConnectCompleted(ClientSocket, token);

                if (ConnectedCallback != null)
                {
                    ConnectedCallback(token);
                }
            }
            else
            {
                Console.WriteLine(string.Format("Failed to connect. {0}", e.SocketError));
            }
        }
    }
}
