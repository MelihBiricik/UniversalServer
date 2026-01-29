using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace UniversalServer.Model
{
    public delegate void StatusChangedEventHandler(string s);
    public delegate void MessageReceivedEventHandler(string msg);

    class Server : IServerContract
    {
        
        private static byte[] _buffer = new byte[65537];
        private static Socket _serverSocket;
        private string _messageReceived = string.Empty;
        private static List<Socket> _clients = new List<Socket>();
        private ConnectionData _conData = new ConnectionData();
        public event StatusChangedEventHandler StatusPropertyChanged;
        public event MessageReceivedEventHandler MessageReceived;

        public void Start(IPAddress ipadr, int port)
        {
            IPEndPoint localEndPoint = new IPEndPoint(ipadr, port);

            _serverSocket = new Socket(ipadr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
           
            _serverSocket.Bind(localEndPoint);
            _serverSocket.Listen(100);
            _serverSocket.BeginAccept(new AsyncCallback(DoAccept), null);
            StatusPropertyChanged($"Waiting for Connection on {localEndPoint.Address} at Port {localEndPoint.Port}...");
        }


        public void DoAccept(IAsyncResult ar)
        {
            StatusPropertyChanged("Connection accepted.");
            Socket s = _serverSocket.EndAccept(ar);
            _clients.Add(s);
            s.BeginReceive(_buffer, 0, _buffer.Length, SocketFlags.None, new AsyncCallback(DoReceive), s);
            _serverSocket.BeginAccept(new AsyncCallback(DoAccept), null);
        }

        public void DoReceive(IAsyncResult ar)
        {
            StatusPropertyChanged("Receiving Data.");
            try
            {
                Socket s = (Socket)ar.AsyncState;
                int numBytesReceived = s.EndReceive(ar);
                byte[] dataReceivedBuffer = new byte[numBytesReceived];
                Array.Copy(_buffer, dataReceivedBuffer, numBytesReceived);
                _messageReceived = Encoding.ASCII.GetString(dataReceivedBuffer);

                //Event
                MessageReceived(_messageReceived);

                byte[] dataSendBuffer = Encoding.ASCII.GetBytes("OK");
                s.BeginSend(dataSendBuffer, 0, dataSendBuffer.Length, SocketFlags.None, new AsyncCallback(DoSend), s);
                
                _serverSocket.BeginAccept(new AsyncCallback(DoAccept), null);
            }
            catch (Exception ex)
            {
                MessageReceived("MessageReceived-Error: " + ex.Message);
            }
            
        }

        public void DoSend(IAsyncResult ar)
        {
            //StatusPropertyChanged("Data sent.");
            Socket s = (Socket)ar.AsyncState;
            s.EndSend(ar);
        }
    }
}
