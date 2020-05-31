using Spiral.Core;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Spiral.PacmanGame.Client
{
    public class ClientSocketV2 : IDisposable
    {
        #region CONST 
        // внимание: это всё должно совпадать на стороне сервера тоже!

        /// <summary>
        /// Размер буфера
        /// </summary>
        public const int bufferSize = 1024; 

        /// <summary>
        /// Сепаратор строк
        /// </summary>
        public const char delimiter = '\n';

        /// <summary>
        /// Обозначение конца пакета/строки
        /// </summary>
        public const char terminator = '\0';

        /// <summary>
        /// Наш протокол. По умолчанию TCP, чтобы нам не приходилось 
        /// рассказывать не доходящие до адресата шутки про UDP
        /// </summary>
        private const ProtocolType protocolType = ProtocolType.Tcp;

        /// <summary>
        /// Семейство адресов у нас IPv4
        /// </summary>
        private const AddressFamily addressFamily = AddressFamily.InterNetwork;

        /// <summary>
        /// Читаем и пишем потоком
        /// </summary>
        private const SocketType socketType = SocketType.Stream; 
        #endregion

        #region IP DATA
        private int m_port = 55000;
        /// <summary>
        /// Порт, на который мы подключаемся
        /// Обратите внимание, что выставление нового значения автоматом разорвёт подключение
        /// </summary>
        public int port
        {
            get { return m_port; }
            set
            {
                if (value == m_port) return;
                Disconnect();
                m_port = value;
            }
        }

        private string m_host = "127.0.0.1";
        /// <summary>
        /// Хост сервера, например 127.0.0.1
        /// Хост сервера должен быть IPv4 для AddressFamily = AddressFamily.InterNetwork
        /// Обратите внимание, что выставление нового значения автоматом разорвёт подключение
        /// </summary>
        /// TODO: в идеале можно написать обработку и для других случаев
        private string host
        {
            get { return m_host; }
            set
            {
                if (value == m_host) return;
                Disconnect();
                m_host = value;
            }
        }
        #endregion

        #region OWN INFO
        /// <summary>
        /// Буфер для посылок
        /// </summary>
        private readonly byte[] buffer = new byte[bufferSize];

        /// <summary>
        /// Количество попыток подключения
        /// </summary>
        public int trialCounter { get; private set; } = 0;

        private bool m_useMaxTrial = true; // TODO: проверить причину зависания для false флага
        /// <summary>
        /// Использовать лимит
        /// </summary>
        public bool useMaxTrial
        {
            get { return m_useMaxTrial; }
            set
            {
                if (m_useMaxTrial == value) return;
                m_useMaxTrial = value;
                ResetTrialCounter();
            }
        }

        private int m_maxTrialCounter = 50;
        /// <summary>
        /// Максимальное число попыток переподключения без сброса
        /// </summary>
        public int maxTrialCounter
        {
            get { return m_maxTrialCounter; }
            set
            {
                value = value > 1 ? value : 1;
                if (m_maxTrialCounter == value) return;
                m_maxTrialCounter = value;
                ResetTrialCounter(true);
            }
        }

        /// <summary>
        /// Достигнуто максимальное число попыток переподключения?
        /// </summary>
        public bool isMaxTrial 
        { 
            get 
            {
                if (!useMaxTrial) return false;
                return trialCounter >= maxTrialCounter; 
            } 
        }

        /// <summary>
        /// Клиент подключён
        /// Вернёт false также в случае, если клиент не создан
        /// </summary>
        public bool connected { get { return client == null ? false : client.Connected; } }

        /// <summary>
        /// Клиент вообще создан (если нет, клиент будет пересоздан при попытке подключения
        /// и ряде других действий)
        /// </summary>
        public bool existing { get { return client != null; } }

        /// <summary>
        /// Проверяем, находимся ли мы в попытке подключения
        /// </summary>
        public bool inConnectingProcess { get; private set; } = false;

        /// <summary>
        /// Проверяем, находимся ли мы в попытке отключения
        /// </summary>
        public bool inDisconnectionProcess { get; private set; } = false;
        #endregion

        #region EVENTS
        public event Action onConnectionStarted;
        public event Action onConnectionSuccess;
        public event Action onConnectionFailed;

        public event Action<int> onReconnectionStarted;
        public event Action onReconnectionLimit;
        public event Action onReconnectionTrialCounterReset; 
        
        public event Action onDisconnectionSuccess;
        public event Action onDisconnectionFailed;

        public event Action onSendError;

        public event Action<string> onMessageReceived;
        #endregion

        #region EXCEPTION MONITOR
        public Exception lastDiconnectionError { get; private set; } = null;
        public Exception lastConnectionError { get; private set; } = null; 
        public Exception lastSendingError { get; private set; } = null;
        #endregion

        #region STATUS
        /// <summary>
        /// Флаг выставляется для того, чтобы переподключаться к серверу в нужном потоке Юнити
        /// </summary>
        public bool requiresEmergencyExternalReboot { get; private set; } = false;
        #endregion

        // INITIALIZING ===========================================================================
        // Инициализация нас
        //=========================================================================================
        #region INITIALIZATION
        private Socket client = null; // наш сокет

        public ClientSocketV2(int port, string host)
        {
            m_port = port;
            m_host = host;
            onConnectionSuccess += OnConnectionSuccess;
            onSendError += OnSendError;
            requiresEmergencyExternalReboot = false;
        }
        #endregion

        // SERVER CONNECTION ======================================================================
        // Подключение к серверу
        //=========================================================================================
        #region SERVER CONNECTION
        /// <summary>
        /// Ручной сброс счётчика неудачных попыток переподключения
        /// </summary>
        /// <param name="autoconnect">Произвести попытку переподключения, если мы не подключены</param>
        public void ResetTrialCounter(bool autoconnect = true)
        {
            trialCounter = 0;
            onReconnectionTrialCounterReset?.Invoke();
            if (autoconnect && !connected && !inConnectingProcess)
            {
                ConnectToServer();
            }
        }

        /// <summary>
        /// Перебилживает сокет при необходимости
        /// </summary>
        /// <returns>Был ли сокет перебилжен</returns>
        private bool CheckClientRebuilded()
        {
            if (client == null)
            {
                client = new Socket(addressFamily, socketType, protocolType);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Пытаемся подключиться к серверу
        /// </summary>
        public void ConnectToServer()
        {
            requiresEmergencyExternalReboot = false;

            int thread = Thread.CurrentThread.ManagedThreadId;
            if (thread != 1)
            {
                StackTrace trace = new StackTrace();
                UnityEngine.Debug.LogWarning($"Trying to access not from Unity main thread:\n{trace}");
            }

            trialCounter++;

            if (useMaxTrial)
            {
                if (trialCounter <= maxTrialCounter)
                {
                    if (trialCounter > 1)
                    {
                        onReconnectionStarted?.Invoke(trialCounter - 1);
                    }
                    AsyncConnect();
                }
                else
                {
                    onReconnectionLimit?.Invoke();
                }
            }
            else
            {
                AsyncConnect();
            }
        }

        /// <summary>
        /// Ждём подключения
        /// </summary>
        private async void AsyncConnect()
        {
            inConnectingProcess = true;
            onConnectionStarted?.Invoke();
            bool result = await ConnectionTask();
            inConnectingProcess = false;
            if (result)
            {
                lastConnectionError = null;
                trialCounter = 0;
                onConnectionSuccess?.Invoke();
            }
            else
            {
                onConnectionFailed?.Invoke();
                ConnectToServer();
            }

            Task<bool> ConnectionTask()
            {
                bool taskResult = false;
                return Task.Run(() =>
                {
                    try
                    {
                        CheckClientRebuilded();
                        Thread.Sleep(50); // делаем задержку между подключениями
                        IPAddress address = IPAddress.Parse(host);
                        IPEndPoint endPoint = new IPEndPoint(address, port);
                        client.SendTimeout = 1000;
                        client.ReceiveTimeout = 1000;
                        client.Connect(endPoint);
                        taskResult = true;
                    }
                    catch (SocketException error)
                    {
                        lastConnectionError = error;
                        if (error.SocketErrorCode == SocketError.IsConnected)
                        {
                            try
                            {
                                client.Disconnect(false); 
                                client.Close();
                                client = null;
                            }
                            catch (Exception again)
                            {
                                lastConnectionError = again;
                            }
                        }
                    }
                    catch (Exception error)
                    {
                        lastConnectionError = error;
                    }
                    Thread.Sleep(1);
                    return taskResult;
                });
            }
        }
        #endregion

        // EVENT CALLBACKS ========================================================================
        // Некоторые внутренние ивенты
        //=========================================================================================
        #region EVENT CALLBACKS
        private void OnConnectionSuccess()
        {
            // получение данных мы делаем асинхронно
            BeginReceive();

            // а отправку данных мы делаем в отдельном потоке
            sendingThread = new Thread(SendingThread) { IsBackground = true };
            sendingThread.Start();
        }
        #endregion

        // SEND TO SERVER =========================================================================
        // Отправляем пирожки бабушке
        //=========================================================================================
        #region SENDING
        private readonly ConcurrentQueue<string> queueSend = new ConcurrentQueue<string>();
        private Thread sendingThread = null;

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

        /// <summary>
        /// Убивает всю очередь посылок, если необходимо
        /// </summary>
        public void KillSenderStreak()
        {
            if (queueSend.Count > 0) while (queueSend.TryDequeue(out _)) { };
        }

        /// <summary>
        /// Тред с посылками, занимающийся разгребанием поставленных в очередь посылок
        /// </summary>
        private void SendingThread()
        {
            if (client == null) return; // клиента вообще нету
            if (disposing) return; // осторожно, двери закрываются

            while (client.Connected)
            {
                if (queueSend.TryDequeue(out string message))
                {
                    try
                    {
                        byte[] data = Encoding.ASCII.GetBytes(message);
                        int sent = client.Send(data); // отправка на связанный сокет
                    }
                    catch (SocketException error)
                    {
                        lastSendingError = error;
                        onSendError?.Invoke();
                    }
                    catch (Exception error)
                    {
                        lastSendingError = error;
                        onSendError?.Invoke();
                    }
                }
                Thread.Sleep(10);
                if (client == null) break;
                if (disposing) break;
            }

            requiresEmergencyExternalReboot = true;
        }

        /// <summary>
        /// Ошибка при отправке как правило может означать разрыв соединения или смерть сокета,
        /// реже - что-то другое. Здесь мы просто проверяем состояние сокета и пр., и если всё
        /// очень плохо - пытаемся восстановить связь. Если же ошибка была не столь критична -
        /// то ну и чёрт с ней
        /// </summary>
        private void OnSendError()
        {
            if (disposing) return; // игнорируем возможные ошибки треда в этом случае

            UnityEngine.Debug.LogWarning($"Sending error: {lastSendingError}");
            lastSendingError = null;

            CheckClientRebuilded(); // проверяем сокет
            if (!client.Connected)
            {
                requiresEmergencyExternalReboot = true;
            }
        }
        #endregion

        // RECIEVING ==============================================================================
        // Здесь мы принимаем данные и создаем очередь приехавших нам запросов
        //=========================================================================================
        #region RECEIVING
        private readonly ManualResetEvent receiveResetEvent = new ManualResetEvent(false);
        private readonly StringBuilder receiver = new StringBuilder(); 
        private readonly ConcurrentQueue<string> queueReceive = new ConcurrentQueue<string>(); 
        private Exception lastReceiveException = null;

        /// <summary>
        /// Начинаем асинхронный приём данных
        /// </summary>
        private void BeginReceive() 
        {
            client.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, ReceieveAsyncCallback, null);
        }

        /// <summary>
        /// Убиваем очередь приёмника
        /// В основном приходится использовать, чтобы держать актуальным последний прилетевший пакет
        /// Можно было бы, конечно, использовать стак (возможно было бы лучше использовать стак),
        /// но пока выбрана была очередь, см. ниже запаковка в очередь на тот случай, если
        /// нам прилетают склейки
        /// </summary>
        public void KillReceiverStreak()
        {
            if (queueReceive.Count > 0)
            {
                while (queueReceive.TryDequeue(out _)) { };
            }
        }

        /// <summary>
        /// Коллбек к асинхронному приёму от сервака: срабатывает, когда нам что-то прилетело
        /// </summary>
        /// <param name="asyncResult">Результат приёма</param>
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
                        /* Да, можно было использовать header'ы пакетов, чтобы избежать склеек в потоке
                         * В данном случае я просто накидываю стоп-символы в конец посылки. Если нам
                         * приедет посылка с несколькими стоп-символами, её просто порежет на кусочки
                         * и закинет в очередь ожидания */

                        string[] parse = input.Split(terminator);
                        int count = parse.Length;
                        for (int i = 0; i < count; i++)
                        {
                            if (i != count - 1) // всё между разделителями пакуем в очередь
                            {
                                string pack = parse[i];
                                if (!string.IsNullOrEmpty(pack)) receiver.Append(pack);
                                // нулевые или полностью пустые строки будут проигнорены,
                                // однако white-space строки также пойдут в буфер на тот случай
                                // если нам это по каким-то причинам понадобится - передавать пустые
                                // сообщения таким образом
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
                else // приём завершился, но пришло нам красивое ни...чего - разрываем соединение
                {
                    Disconnect(true); // и пытаемся подключиться заново
                }
            }
            catch (IOException error) // ошибка ввода-вывода, вероятно имеет смысл переподключиться
            {
                lastReceiveException = error;
                Disconnect(true);
            }
            catch (ObjectDisposedException error) // похоже, мы самоубились
            {
                lastReceiveException = error;
                Disconnect(false);
            }
            catch (Exception error) // какое-то штатное исключение, продолжаем
            {
                lastReceiveException = error;
            }
            finally // выполнится независимо от всего выше
            {
                if (lastReceiveException != null) 
                {
                    // аварийно выгружаем в очередь всё, что успели принять, если таковое имеется
                    if (receiver.Length != 0) ReceiverPackToQueue();
                    Console.WriteLine(lastReceiveException);
                }
            }
            receiveResetEvent.Set(); // обозначили, что всё

            // разгребли очередь приёмки
            while (queueReceive.TryDequeue(out string message))
            {
                onMessageReceived?.Invoke(message);
            }

            void ReceiverPackToQueue()
            {
                string packToQueue = receiver.ToString();
                queueReceive.Enqueue(packToQueue);
                receiver.Clear();
            }
        }
        #endregion

        // SERVER DICONNECT =======================================================================
        // Отключение от сервера
        //=========================================================================================
        #region SERVER DISCONNECTION
        /// <summary>
        /// Отрубаемся от сервера приличными способами
        /// </summary>
        /// <param name="tryToReconnect">Попытаться автоматически переподключиться к серверу по завершении</param>
        public void Disconnect(bool tryToReconnect = false)
        {
            if (client == null) return;
            AsyncDisconnect(tryToReconnect);
        }

        /// <summary>
        /// Асинхронный дисконнект.
        /// Запускает задачу нормального отключения, после чего сигналит, получилось у него
        /// нормально отключиться или нет
        /// </summary>
        /// <param name="tryToReconnect">Попытаться автоматически переподключиться к серверу по завершении</param>
        private async void AsyncDisconnect(bool tryToReconnect = false)
        {
            inDisconnectionProcess = false;
            bool result = await DisconnectionTask();
            inDisconnectionProcess = true;
            if (result)
            {
                lastDiconnectionError = null;
                onDisconnectionSuccess?.Invoke();
            }
            else
            {
                onDisconnectionFailed?.Invoke();
            }
            if (tryToReconnect) requiresEmergencyExternalReboot = true; 

            Task<bool> DisconnectionTask()
            {
                bool taskResult = false;
                return Task.Run(() =>
                {
                    try
                    {
                        if (client == null) return true; // нас вообще не
                        if (!client.Connected) return true; // мы и так отключены
                        client.Disconnect(false);
                        client.Close();
                        client = null;
                        taskResult = true;
                    }
                    catch (Exception error)
                    {
                        lastDiconnectionError = error;
                        return false;
                    }
                    return taskResult;
                });
            }
        }
        #endregion

        // ON DISPOSE =============================================================================
        // Commited suicide!
        //=========================================================================================
        #region DISPOSING
        private bool disposing = false;
        public void Dispose()
        {
            if (disposing)
            {
                // в душе не ведаю, как так может получиться, но это свидетельствует о какой-то капитальной неполадке
                Console.WriteLine("Cannot dispose socket with disposing flag on");
                return; 
            }

            disposing = true;

            // закрываем лавочку
            if (client != null)
            {
                if (client.Connected) client.Disconnect(false);
                client.Close();
                client = null;
            }

            // убиваем все подписки
            EventTools.KillInvokations(ref onConnectionStarted);
            EventTools.KillInvokations(ref onConnectionSuccess);
            EventTools.KillInvokations(ref onConnectionFailed);

            EventTools.KillInvokations(ref onReconnectionStarted);
            EventTools.KillInvokations(ref onReconnectionLimit);
            EventTools.KillInvokations(ref onReconnectionTrialCounterReset);

            EventTools.KillInvokations(ref onDisconnectionSuccess);
            EventTools.KillInvokations(ref onDisconnectionFailed);
        }

        ~ClientSocketV2()
        {
            if (!disposing) Dispose(); // не забывайте прибраться за собой
        }
        #endregion
    }

}