using Spiral.Core;
using Spiral.PacmanGame.Client;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace Spiral.PacmanGame.Game
{
    public class IntEvent : UnityEvent<int> { public IntEvent() { }  }
    public class ClientMono : MonoBehaviour
    {
        [Header("Network")]
        public int port = 55000;
        public int bufferSize = 1024;
        public string host = "127.0.0.1";
        public bool autoConnect = true;
        public float reconnectionTimer = 10f;
        
        // мы делаем это юнити-ивентами тупо для возможности подписки из эдитора
        // юнити ивенты оч медленные, но на стороне интерфейса быстродействие не столь критично
        [Space][Header("Events")]
        public UnityEvent onConnected = new UnityEvent();
        public UnityEvent onConnectionStarted = new UnityEvent();
        public IntEvent onReconnection = new IntEvent();
        public UnityEvent onReconnectionLimit = new UnityEvent();
        public UnityEvent onDisconnected = new UnityEvent();

        public bool connected { get { return client.connected; } }
        public int maxTrialCounter { get { return client.maxTrialCounter; } }
        public bool inConnectingProcess { get { return client.inConnectingProcess; } }

        private ClientSocketV2 client;

        // MONO BEHAVIOUR =========================================================================
        //=========================================================================================
        private void Start()
        {
            if (autoConnect) CheckSocket(); 
        }

        public float timeToReconnect { get; private set; } = 0;
        public bool longReconnectTimer { get; private set; } = false;
        private void FixedUpdate()
        {
            if (client != null) 
            {
                if (!client.connected) // обнаружили отсутствие подключения
                {
                    if (longReconnectTimer) // поставлен на удержание
                    {
                        timeToReconnect += Time.fixedDeltaTime;
                        if (timeToReconnect > reconnectionTimer)
                        {
                            timeToReconnect = 0;
                            longReconnectTimer = false;
                            client.ResetTrialCounter(true);
                        }
                    }

                    if (!client.connected && client.requiresEmergencyExternalReboot)
                    {
                        Debug.Log("Try to reconnect");
                        client.ConnectToServer();
                    }
                }
            }
            else
            {
                CheckSocket(); // проверяем сокет
            }
        }

        private void OnDestroy()
        {
            client?.Dispose();
            client = null; // чтоб наверняка
        }

        // FUNC ===================================================================================
        //=========================================================================================
        public void Disconnect()
        {
            if (client == null) return;
            try
            {
                client.Dispose();
                client = null;
                CheckSocket();
            }
            catch (Exception error)
            {
                Debug.Log(error);
            }
        }

        public void CheckSocket()
        {
            if (client == null)
            {
                client = new ClientSocketV2(port, host);

                client.onConnectionStarted   += ClientSocket_onConnectionStarted;
                client.onConnectionSuccess   += ClientSocket_onConnected;
                client.onConnectionFailed    += ClientSocket_onConnectionFailed;

                client.onReconnectionStarted += ClientSocket_onReconnectionBegin;
                client.onReconnectionLimit   += ClientSocket_onReconnectionLimit;

                client.onDisconnectionSuccess += ClientSocket_onDisconnected;

                client.onMessageReceived += Client_onMessageReceived;
            }
            if (!client.connected) client.ConnectToServer();
        }

        public void KillRecievingStreak()
        {
            client.KillReceiverStreak();
        }

        public void KillSendingStreak()
        {
            client.KillSenderStreak();
        }

        public void SendRequest(string content)
        {
            client.Send(content, true);
        }

        public string PickLastAnswer() // внимание, это зачистит всю очередь!
        {
            int count = queue.Count;

            switch (count)
            {
                case 0: return "";

                case 1:
                    queue.TryDequeue(out string single);
                    return single;

                default:
                    string result = "";
                    while (queue.TryDequeue(out string dequeue))
                    {
                        result = dequeue;
                    }
                    return result;
            }
        }

        public async Task<string> PickFirstAnswer(int tries = -1)
        {
            return await Task.Run(() =>
            {
                int times = 0;
                bool haveSomething = false;
                string result = "";
                while (!haveSomething)
                {
                    haveSomething = queue.TryDequeue(out result);
                    times++;
                    if (tries > 0 && times > tries) return result;
                    Thread.Sleep(1);
                }
                return result;
            });
        }

        public async Task<List<string>> PickLastPackedAnswer()
        {
            string result = await PickFirstAnswer(); 
            return new List<string>(result.Split(ClientSocketV2.delimiter));
        }

        private readonly ConcurrentQueue<string> queue = new ConcurrentQueue<string>();
        private void Client_onMessageReceived(string message)
        {
            queue.Enqueue(message);
        }

        // EVENTS =================================================================================
        //=========================================================================================
        private void ClientSocket_onConnectionFailed()
        {
            CheckSocket();
            //Debug.Log($"Connection Failed: {client.lastConnectionError}");
        }

        private void ClientSocket_onReconnectionBegin(int trialCounter)
        {
            onReconnection.Invoke(trialCounter);
        }

        private void ClientSocket_onReconnectionLimit()
        {
            longReconnectTimer = true;
            onReconnectionLimit?.Invoke();
        }

        private void ClientSocket_onConnected()
        {
            onConnected?.Invoke();
        }

        private void ClientSocket_onDisconnected()
        {
            onDisconnected?.Invoke();
        }

        private void ClientSocket_onConnectionStarted()
        {
            onConnectionStarted?.Invoke();
        }

    }
}
