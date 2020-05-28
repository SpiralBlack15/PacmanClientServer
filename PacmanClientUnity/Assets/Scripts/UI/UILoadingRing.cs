using Spiral.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Spiral.PacmanGame.UI
{
    [RequireComponent(typeof(Image))]
    public class UILoadingRing : MonoBehaviour
    {
        private Image m_image = null;
        public Image image { get { return this.Take(ref m_image); } }

        public bool rotating = false;
        public float rotatingSpeed = 15f;

        private void Awake()
        {
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = (int)Image.Origin360.Top;
            image.fillAmount = 0;
        }

        private float m_progress = 0;
        public float progress
        {
            get { return m_progress; }
            set
            {
                value = value.Clamp(0, 1);
                if (m_progress == value) return;
                image.fillAmount = m_progress = value;
            }
        }

        public void RotateZ(float stepDegrees)
        {
            Vector3 lea = transform.localEulerAngles;
            lea.z += stepDegrees;
            transform.localEulerAngles = lea;
        }
        
        public void DefaultZ()
        {
            transform.localEulerAngles = new Vector3(0, 0, 1);
        }
    }
}
