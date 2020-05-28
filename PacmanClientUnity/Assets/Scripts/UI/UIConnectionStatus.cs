using Spiral.PacmanGame.Client;
using Spiral.PacmanGame.Game;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Spiral.PacmanGame.UI
{
    public class UIConnectionStatus : MonoBehaviour
    {
        [Header("Socket")]
        public ClientMono client = null;
       
        [Header("UI")]
        public UILoadingRing loadingRing;
        public Text txtStatus;
        public float speed = 500f;
        public Color colorNotConnected = new Color(152f / 255, 0, 0);
        public Color colorConnected = new Color(0, 152f / 255, 0);

        // MONO BEHAVIOUR =========================================================================
        // Часть, относящаяся к моно бехевиору
        //=========================================================================================
        private void Awake()
        {
            client.onConnected.AddListener(OnConnected);
            client.onConnectionStarted.AddListener(OnConnectionStarted);

            client.onReconnection.AddListener(OnReconnection);
            client.onReconnectionLimit.AddListener(OnReconnectionLimit);

            client.onDisconnected.AddListener(OnDisconnected);
        }

        private void Update()
        {
            if (client.inConnectingProcess || client.longReconnectTimer)
            {
                loadingRing.RotateZ(Time.unscaledDeltaTime * speed);
            }

            if (client.connected)
            {
                loadingRing.RotateZ(-Time.unscaledDeltaTime * speed * 0.25f);
            }
        }

        private void OnDestroy()
        {
            client.onConnected.RemoveListener(OnConnected);
            client.onConnectionStarted.RemoveListener(OnConnectionStarted);

            client.onReconnection.RemoveListener(OnReconnection);
            client.onReconnectionLimit.RemoveListener(OnReconnectionLimit);

            client.onDisconnected.RemoveListener(OnDisconnected);
        }

        // EVENT CALLBACKS ========================================================================
        // Коллбеки ивентов
        //=========================================================================================
        private void SetStatus(string status)
        {
            if (txtStatus.text == status) return;
            txtStatus.text = status;
        }

        private void OnConnected()
        {
            loadingRing.progress = 0.95f;
            loadingRing.image.color = colorConnected;
            SetStatus($"Подключён к {client.host}:{client.port}");
            loadingRing.DefaultZ();
        }

        private void OnConnectionStarted()
        {
            loadingRing.progress = 0.12f;
            loadingRing.image.color = colorNotConnected;
            SetStatus($"Попытка подключения...");
        }

        private void OnReconnection(int trial)
        {
            loadingRing.progress = 0.12f;
            loadingRing.image.color = colorNotConnected;
            SetStatus($"Попытка подключения: {trial - 1}/{client.maxTrialCounter - 1}");
        }
        
        private void OnReconnectionLimit()
        {
            loadingRing.progress = 0.12f;
            loadingRing.image.color = colorNotConnected;
            SetStatus($"Ждём переподключения..."); 
        }

        private void OnDisconnected()
        {
            loadingRing.progress = 0;
            loadingRing.image.color = colorNotConnected;
            SetStatus($"Соединение разорвано");
            loadingRing.DefaultZ();
        }

    }
}

