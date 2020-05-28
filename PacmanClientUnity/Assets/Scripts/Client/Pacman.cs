using Spiral.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Spiral.PacmanGame
{
    [RequireComponent(typeof(SpriteRenderer), typeof(Animator))]
    public class Pacman : MonoBehaviour
    {
        private SpriteRenderer m_sprite = null;
        public SpriteRenderer sprite { get { return this.Take(ref m_sprite); } }

        private Animator m_animator = null;
        public Animator animator { get { return this.Take(ref m_animator); } }
    }
}

