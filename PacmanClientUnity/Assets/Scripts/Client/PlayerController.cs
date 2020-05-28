using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Spiral.PacmanGame.Game
{
    public class PlayerController : MonoBehaviour
    {
        public bool[] controlInput { get; private set; } = new bool[4];
        public bool moving { get; private set; }

        public void Update() // using WASD or Up-Right-Left-Down
        {
            bool up    = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
            bool down  = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
            bool left  = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
            bool right = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);

            controlInput[0] = up;
            controlInput[1] = down;
            controlInput[2] = left;
            controlInput[3] = right;

            moving = up || down || left || right;
        }
    }

}