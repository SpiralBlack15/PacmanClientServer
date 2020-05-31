using Spiral.Core;
using Spiral.PacmanGame.Game;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Spiral.PacmanGame.UI
{
    [RequireComponent(typeof(Text))]
    public class UILocalAddress : MonoBehaviour
    {
        public ClientMono client;

        private Text m_text = null;
        private Text text { get { return this.Take(ref m_text); } }

        private void Awake()
        {
            client.onConnected.AddListener(UpdateText);
        }

        private void UpdateText()
        {
            text.text = client.endPoint.ToString();
        }
    }
}
