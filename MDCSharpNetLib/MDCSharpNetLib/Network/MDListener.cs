using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace MDCSharpNetLib.Network
{
    class MDListener
    {
        //비동기 통신을 할때 비동기 소켓 메소드를 호출할 때 용되는 객체 
        // begin,end,IAsyncResult 대신 사용된다.
        //IAsyncResult과 다르게 매번 생성 소멸 시키는게 아니라 풀로 관리 할수 있다.
        SocketAsyncEventArgs Accept_Args;

        MDSocketOption RemoteSocketOpt = new MDSocketOption();

        //클라이언트의 접속을 받아들이는 소켓
        Socket ListenSocket;

        //비동기로 작동하는 스레드들의 순서를 동기화 해주기 위한 이벤트 객체
        AutoResetEvent FlowControlEvent;

        //새 클라이언트가 접속했을때 호출되는 델리게이트
        public Action<Socket, object> OnNewClientCallback = null;

        public MDListener() { }

        //리스너는 여러개가 될수 있다.
        public void Start(string host, int port, int backlog, MDSocketOption socketOption)
        {
            //최대 커넥션수, 버퍼크기
            RemoteSocketOpt = socketOption;
            
            //TCP소켓 생성
            ListenSocket = new Socket(AddressFamily.InterNetwork,
                            SocketType.Stream, ProtocolType.Tcp);


            IPAddress address;

            if (host == "0.0.0.0" || host == "localhist")
            {
                address = IPAddress.Any;
            }
            else
            {
                address = IPAddress.Parse(host);
            }

            //클라이언트가 도착하는 인터넷 프로토콜의 끝점
            var endpoint = new IPEndPoint(address, port);


            try
            {
                //주소정보 바인딩 리슨 시작
                ListenSocket.Bind(endpoint);
                ListenSocket.Listen(backlog);

                //비동기 호출시 필요한 객체 와 그것과 연결된 함수.
                Accept_Args = new SocketAsyncEventArgs();
                Accept_Args.Completed += new EventHandler<SocketAsyncEventArgs>(OnAcceptCompleted);

                //리슨을 수행할 스레드
                var listen_thread = new Thread(DoListen);
                listen_thread.Start();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        void DoListen()
        {
            //순서를 제어할 이벤트객체
            FlowControlEvent = new AutoResetEvent(false);

            while (true)
            {
                //억셉트할 소켓을 위해서 비워두고
                Accept_Args.AcceptSocket = null;

                bool pending = true;

                try
                {
                    //비동기 억셉트 호출 비동기 동작중이면 트루를 반환 만약에 즉시완료되면 비동기 방식으로 콜백함수가 호출되는 것이 아니므로
                    //직접 콜백함수를 호출해애 한다.
                    pending = ListenSocket.AcceptAsync(Accept_Args);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    continue;
                }

                // 동기적으로 작동하는 상황이 일어나는 이유는?
                if (!pending)
                {
                    OnAcceptCompleted(null, Accept_Args);
                }

                //비동기 콜백함수가 호출된경우 억셉트가 완료되면 다시 루프를 시작하고 아니면 기다리는 방식으로 무의미한 루프를 막는다.
                FlowControlEvent.WaitOne();

            }
        }

        void OnAcceptCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError == SocketError.Success)
            {
                //SocketAsyncEventArgs 객체에서 접속한 소켓을 저장하고
                var client_socket = e.AcceptSocket;
                client_socket.NoDelay = RemoteSocketOpt.NoDelay;

                if (OnNewClientCallback != null)
                {
                    //클라이언트가 접속한경우 외부에서 정해준동작을 실행한다. 
                    OnNewClientCallback(client_socket, e.UserToken);
                }

                //다시 루프를 시작해 준다.
                FlowControlEvent.Set();

                return;
            }
            else
            {
                Console.WriteLine("Failed to accept client. " + e.SocketError);
            }

            //실패인경우도 다시 루프를 시작해 준다.
            FlowControlEvent.Set();
        }

    }


}
