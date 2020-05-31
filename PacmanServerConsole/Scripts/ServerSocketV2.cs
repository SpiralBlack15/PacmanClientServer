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
                Logger.ColorLog($"SERVER binded on port: {port}");
                Logger.ColorLog($"SERVER is waiting for clients...");
                onServerBind?.Invoke();
                server.Listen(10);
                server.BeginAccept(OnAcceptClientAsync, server);
            }
            catch (Exception error)
            {
                Logger.ColorLog($"SERVER ERROR: {error}", ConsoleColor.Red);
            }

            void OnAcceptClientAsync(IAsyncResult asyncResult)
            {
                if (server == null) return; // да, это случается при выходе
                try
                {
                    var client = server.EndAccept(asyncResult);
                    asseptResetEvent.Set();

                    // сообщаем, что есть входящее соединение
                    Logger.ColorLog($"SERVER accepted connection: {client.RemoteEndPoint}", ConsoleColor.Green);

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
                    Logger.ColorLog($"SERVER ERROR: {error}", ConsoleColor.Red);
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
                    Logger.ColorLog($"SERVER removed client: {ep}", ConsoleColor.Yellow);
                    Logger.ColorLog($"SERVER client count: {connections.Count}", ConsoleColor.Yellow);
                    if (connections.Count == 0)
                    {
                        Logger.ColorLog($"SERVER is waiting for clients...");
                        Logger.ColorLog($"(Press Enter to close server)", ConsoleColor.DarkGray);
                    }
                }
            }
            catch (Exception error)
            {
                Logger.ColorLog($"SERVER ERROR while removing client: {error}", ConsoleColor.Red);
            }
        }

        protected internal void Disconnect()
        {
            if (server != null)
            {
                server.Close();
                server = null;
            }
        }

        private bool disposing = false;
        public void Dispose()
        {
            disposing = true;
            if (server != null) Disconnect();

            EventTools.KillInvokations(ref onClientDisconnected);
            EventTools.KillInvokations(ref onMessageReceived);
        }

        ~ServerSocketV2()
        {
            if (!disposing) Dispose();
        }
    }
}
