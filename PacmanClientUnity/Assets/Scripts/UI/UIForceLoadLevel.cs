using Spiral.PacmanGame;
using Spiral.PacmanGame.Game;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIForceLoadLevel : MonoBehaviour
{
    public Button button;
    public RemoteGameController controller;

    private bool m_active = true;
    public bool active
    {
        get { return m_active; }
        set
        {
            if (m_active == value) return;
            m_active = value;
            button.gameObject.SetActive(value);
            button.interactable = value; // чтобы уж наверняка
        }
    }

    private void Awake()
    {
        button.onClick.AddListener(ForceLoad);     
    }

    private void Update()
    {
        if (!controller.client.connected) // мы не подключены
        {
            active = false;
            return;
        }

        if (!controller.inGame)
        {
            active = true;
            return;
        }

        active = false;
    }

    private void ForceLoad()
    {
        controller.GetLevel();
    }
}
