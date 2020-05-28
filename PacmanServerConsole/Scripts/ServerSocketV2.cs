using Spiral.Core;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace PacmanServerConsole
{
    public class ServerSocketV2 : IDisposable
    {
        private const AddressFamily addressFamily = AddressFamily.InterNetwork; // ipv4
        private const SocketType socketType = SocketType.Stream;
        private const ProtocolType protocolType = ProtocolType.Tcp;

        public int port { get; private set; } = 55000;

        private List<ClientConnection> connections = new List<ClientConnection>();
        private Socket server;

        public event Action<ClientConnection> onClientDisconnected;
        public event Action<ClientConnection, string> onMessageReceived;
        public event Action onServerBind;

        private void RebuildSocket()
        {
            server = new Socket(addressFamily, socketType, protocolType);
        }

        public ManualResetEvent asseptResetEvent { get; private set; } = new ManualResetEvent(false);
        public void RunListener()
        {
            if (server == null) RebuildSocket();
            try
            {
                EndPoint localEP = new IPEndPoint(IPAddress.Any, port);
                server.Bind(localEP); 
                Console.WriteLine($"Binded on port: {port}");
                onServerBind?.Invoke();
                server.Listen(10);
                server.BeginAccept(OnAcceptClientAsync, server);
            }
            catch (Exception error)
            {
                Console.WriteLine($"Server error: {error}");
            }

            void OnAcceptClientAsync(IAsyncResult asyncResult)
            {
                if (server == null) return; // да, это случается при выходе
                try
                {
                    var client = server.EndAccept(asyncResult);
                    asseptResetEvent.Set();

                    // сообщаем, что есть входящее соединение
                    Console.WriteLine($"Accepted connection: {client.RemoteEndPoint}");

                    // удаляем если есть
                    int idx = connections.FindIndex(x => x.client == client);
                    if (idx >= 0) RemoveConnection(idx);

                    // добвляемся
                    lock (connections)
                    {
                        ClientConnection connection = new ClientConnection(client, port); // автоматом запустит прослушку
                        connection.onMessageReceived += Connection_onMessageReceived;
                        connection.onClientDisconnected += Connection_onClientDisconnected;
                        connections.Add(connection);
                    }

                    // ждём новых подключений
                    server.BeginAccept(OnAcceptClientAsync, server);
                }
                catch (Exception error)
                {
                    Console.WriteLine(error);
                }
            }
        }

        public void SendToClient(ClientConnection client, string message, bool terminated)
        {
            client.Send(message, terminated);
        }

        private void Connection_onClientDisconnected(ClientConnection clientDisconnected)
        {
            onClientDisconnected?.Invoke(clientDisconnected);
            RemoveConnection(connections.FindIndex(x => x == clientDisconnected));
        }

        private void Connection_onMessageReceived(ClientConnection client, string message)
        {
            onMessageReceived?.Invoke(client, message);
        }

        private void RemoveConnection(int connectionIDX)
        {
            try
            {
                lock (connections)
                {
                    if (connectionIDX < 0 || connectionIDX >= connections.Count) return;
                    ClientConnection clientConnection = connections[connectionIDX];
                    EndPoint ep = clientConnection.endPoint;
                    if (clientConnection != null)
                    {
                        clientConnection.onMessageReceived -= Connection_onMessageReceived;
                        clientConnection.onClientDisconnected -= Connection_onClientDisconnected;
                        clientConnection.Dispose();
                    }
                    connections.RemoveAt(connectionIDX);
                    Console.WriteLine($"Remove Client: {ep}");
                }
            }
            catch (Exception error)
            {
                Console.WriteLine($"Error removing client connection: {error}");
            }
        }

        protected internal void Disconnect()
        {
            server.Close();
            server = null;
        }

        private bool disposing = false;
        public void Dispose()
        {
            disposing = true;
            Disconnect();

            EventTools.KillInvokations(ref onClientDisconnected);
            EventTools.KillInvokations(ref onMessageReceived);
        }

        ~ServerSocketV2()
        {
            if (!disposing) Dispose();
        }
    }
}
