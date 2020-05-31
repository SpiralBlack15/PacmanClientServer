using Spiral.Core;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace PacmanServerConsole
{
    public class ClientConnection : IDisposable
    {
        public const int bufferSize = 1024;

        public const char terminator = '\0';
        public const string terminatorStr = "\0";

        public const char delimiter = '\n';
        public const string delimiterStr = "\n";

        private readonly byte[] buffer = new byte[bufferSize];

        public Socket client { get; private set; } = null;
        public int port { get; private set; } = 55000;
        public EndPoint endPoint { get; private set; } = null;
        public PacmanGame game { get; private set; } = null;

        public event Action<ClientConnection, string> onMessageReceived;
        public event Action<ClientConnection> onClientDisconnected;

        // INITIALIZING ============================================================================
        // Инициализация
        //=========================================================================================
        public ClientConnection(Socket client, int port)
        {
            this.client = client;
            this.port = port;
            endPoint = client.RemoteEndPoint;
            game = new PacmanGame(); // cоздаём новый экземпляр игры для этого клиента

            // получение данных мы делаем асинхронно
            BeginReceive();

            // а отправку данных мы просто делаем в отдельном потоке
            if (disconnecting) return; // это может потребоваться, если ошибка вылетит в BeginReceieve
            lock (sendingThreadLocker)
            {
                sendingThread = new Thread(SendingThread) { IsBackground = true };
                sendingThread.Start();
            }
        }

        // RECIEVING ==============================================================================
        // Здесь мы принимаем данные и создаем очередь приехавших нам запросов
        //=========================================================================================
        private readonly ManualResetEvent receiveResetEvent = new ManualResetEvent(false);
        private readonly StringBuilder receiver = new StringBuilder(); // текущее считывание
        private readonly ConcurrentQueue<string> queueReceive = new ConcurrentQueue<string>(); // очередь на приём
        private Exception lastReceiveException = null;

        private void BeginReceive() // начинаем принимать данные
        {
            try
            {
                client.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, ReceieveAsyncCallback, null);
            }
            catch (SocketException)
            {
                Disconnect(); // можно ошибку не кидать, и так ясно, что проблема с подключением
            }
            catch (Exception error)
            {
                Logger.ColorLog($"{endPoint}: RECIEVER ERROR {error}", ConsoleColor.Red);
                Disconnect();
            }
        }

        private void ReceieveAsyncCallback(IAsyncResult asyncResult)
        {
            try
            {
                lastReceiveException = null;
                int bytes = client.EndReceive(asyncResult);
                if (bytes > 1) // мы что-то получаем
                {
                    string input = Encoding.ASCII.GetString(buffer, 0, bytes);
                    if (input.Contains(terminator)) // наш приём содержит признак конца передачи
                    {
                        string[] parse = input.Split(terminator);
                        int count = parse.Length;
                        for (int i = 0; i < count; i++)
                        {
                            if (i != count - 1) // всё между разделителями пакуем
                            {
                                receiver.Append(parse[i]);
                                ReceiverPackToQueue();
                            }
                            else // остальное не пакуем
                            {
                                if (parse[i].Length > 0) receiver.Append(parse[i]);
                            }
                        }
                    }
                    else receiver.Append(input); // добавляем, идём дальше

                    BeginReceive(); // возвращаемся обратно к прослушке клиента
                }
                else
                {
                    Disconnect();
                }
            }
            catch (SocketException)
            {
                Disconnect(); // в этом случае мы просто молча отключаемся
            }
            catch (IOException error)
            {
                lastReceiveException = error;
                Disconnect();
            }
            catch (ObjectDisposedException error)
            {
                lastReceiveException = error;
                Disconnect();
            }
            catch (Exception error)
            {
                lastReceiveException = error;
            }
            finally // выполнится независимо от всего выше
            {
                if (lastReceiveException != null)
                {
                    if (receiver.Length != 0) ReceiverPackToQueue();
                    Logger.ColorLog($"{endPoint}: RECIEVE ERROR {lastReceiveException}", ConsoleColor.Red);
                }
            }
            receiveResetEvent.Set(); // обозначили, что всё

            // разгребли очередь приёмки
            while (queueReceive.TryDequeue(out string message))
            {
                onMessageReceived?.Invoke(this, message);
            }

            void ReceiverPackToQueue()
            {
                string packToQueue = receiver.ToString();
                queueReceive.Enqueue(packToQueue);
                receiver.Clear();
            }
        }

        // SENDER =================================================================================
        // Здесь мы отправляем пирожки клиенту
        //=========================================================================================
        private readonly ConcurrentQueue<string> queueSend = new ConcurrentQueue<string>();
        /// <summary>
        /// Ставим сообщение в очередь на отправку
        /// </summary>
        /// <param name="message">Сообщение</param>
        /// <param name="terminated">Обязательно закончить нуль-символом</param>
        public void Send(string message, bool terminated = true)
        {
            if (terminated) message += terminator;
            queueSend.Enqueue(message);
        }

        private readonly object sendingThreadLocker = new object();
        private Thread sendingThread = null;
        private void SendingThread()
        {
            if (client == null) return; 
            while (client.Connected && !disconnecting)
            {
                if (queueSend.TryDequeue(out string message))
                {
                    Logger.ColorLog($"{endPoint}: <- {message}", ConsoleColor.Gray);
                    try
                    {
                        byte[] data = Encoding.ASCII.GetBytes(message);
                        client.Send(data);
                    }
                    catch (SocketException)
                    {
                        Disconnect();
                    }
                    catch (Exception error)
                    {
                        Logger.ColorLog($"{endPoint}: SENDING ERROR: {error}", ConsoleColor.Red);
                    }
                }
                Thread.Sleep(10);
                if (client == null) break;
            }
        }

        // DISCONNECTION ==========================================================================
        // Разрываем подключение
        //=========================================================================================
        private bool disconnecting = false;
        public void Disconnect()
        {
            disconnecting = true;
            try
            {
                Logger.ColorLog($"{endPoint}: disconnecting...", ConsoleColor.Yellow);
                lock (sendingThreadLocker)
                {
                    if (sendingThread != null)
                    {
                        if (sendingThread.IsAlive) sendingThread.Abort();
                    }
                    if (client != null)
                    {
                        client.Close();
                        client = null;
                    }
                }
            }
            catch (Exception error)
            {
                Logger.ColorLog($"{endPoint} DISCONNECTION ERROR: {error}", ConsoleColor.Red);
            }
            onClientDisconnected?.Invoke(this); // вызываем в любом случае!
        }

        private bool disposing = false;
        public void Dispose()
        {
            disposing = true;
            if (client != null)
            {
                if (client.Connected) Disconnect();
            }

            EventTools.KillInvokations(ref onClientDisconnected);
            EventTools.KillInvokations(ref onMessageReceived);
        }

        ~ClientConnection()
        {
            if (!disposing) Dispose();
        }
    }
}
