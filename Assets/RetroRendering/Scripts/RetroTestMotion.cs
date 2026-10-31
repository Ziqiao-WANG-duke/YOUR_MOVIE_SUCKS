using UnityEngine;

namespace YourMovieSucks.Retro
{
    /// <summary>Test scene only. Never add camera motion to the gameplay camera.</summary>
    public sealed class RetroTestMotion : MonoBehaviour
    {
        public Camera testCamera;
        public Transform texturedWall;
        public Animator[] animators;
        public bool motion = true;
        private Vector3 cameraOrigin;
        private Quaternion cameraRotation, wallRotation;
        private float elapsed;

        private void Start()
        {
            cameraOrigin = testCamera.transform.position;
            cameraRotation = testCamera.transform.rotation;
            if (texturedWall) wallRotation = texturedWall.rotation;
            foreach (var animator in animators)
                if (animator) animator.SetFloat("Speed", 1f);
        }

        private void Update()
        {
            if (!motion) return;
            elapsed += Time.deltaTime;
            testCamera.transform.position = cameraOrigin + new Vector3(Mathf.Sin(elapsed * 0.3f) * 0.45f, 0, 0);
            testCamera.transform.rotation = cameraRotation * Quaternion.Euler(0, Mathf.Sin(elapsed * 0.3f) * -2f, 0);
            if (texturedWall) texturedWall.rotation = wallRotation * Quaternion.Euler(0, Mathf.Sin(elapsed * 0.4f) * 15f, 0);
        }
    }
}
