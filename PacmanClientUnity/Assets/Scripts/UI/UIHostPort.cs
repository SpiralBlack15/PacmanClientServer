using Spiral.PacmanGame.Game;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Spiral.PacmanGame.UI
{
    public class UIHostPort : MonoBehaviour
    {
        public InputField host;
        public InputField port;
        public Text pressEnter;
        public ClientMono client;

        private void Awake()
        {
            host.onEndEdit.AddListener(OnHostPostChanged);
            port.onEndEdit.AddListener(OnHostPostChanged);

            host.onValueChanged.AddListener(OnSomethingChange);
            port.onValueChanged.AddListener(OnSomethingChange);
        }

        private void OnSomethingChange(string str)
        {
            pressEnter.gameObject.SetActive(true);
        }

        private void OnHostPostChanged(string str)
        {
            pressEnter.gameObject.SetActive(false);
            try
            {
                client.host = host.text;
                client.port = Convert.ToInt32(port.text);
                client.Disconnect();
            }
            catch (Exception error)
            {
                Debug.Log(error);
            }
        }
    }

}