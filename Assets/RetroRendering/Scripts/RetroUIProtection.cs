using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace YourMovieSucks.Retro
{
    /// <summary>Protect dynamically created UGUI canvases without changing the gameplay label code.</summary>
    public sealed class RetroUIProtection : MonoBehaviour
    {
        private readonly List<GameObject> roots = new List<GameObject>(32);
        private readonly List<Canvas> canvases = new List<Canvas>(32);
        private readonly List<Transform> children = new List<Transform>(32);
        private readonly Dictionary<GameObject, int> layers = new Dictionary<GameObject, int>();
        private readonly Dictionary<Canvas, Camera> cameras = new Dictionary<Canvas, Camera>();
        private readonly List<GameObject> deadObjects = new List<GameObject>();
        private readonly List<Canvas> deadCanvases = new List<Canvas>();
        private int refreshCount;

        public void Refresh(Camera source, Camera presentation)
        {
            // Reused lists avoid per-frame FindObjects arrays. Scan late, after words/carets are spawned.
            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                var scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.isLoaded) continue;
                roots.Clear();
                scene.GetRootGameObjects(roots);
                foreach (var root in roots)
                {
                    canvases.Clear();
                    root.GetComponentsInChildren(true, canvases);
                    foreach (var canvas in canvases)
                    {
                        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay) continue;
                        // Do not steal canvases owned by another gameplay camera.
                        if (canvas.worldCamera && canvas.worldCamera != source && canvas.worldCamera != presentation) continue;
                        if (!cameras.ContainsKey(canvas)) cameras.Add(canvas, canvas.worldCamera);
                        if (canvas.renderMode == RenderMode.WorldSpace || canvas.worldCamera == source)
                            canvas.worldCamera = presentation;
                        children.Clear();
                        canvas.GetComponentsInChildren(true, children);
                        foreach (var child in children)
                        {
                            var go = child.gameObject;
                            if (!layers.ContainsKey(go)) layers.Add(go, go.layer);
                            go.layer = 5;
                        }
                    }
                }
            }
            if (++refreshCount % 120 != 0) return;
            deadObjects.Clear(); deadCanvases.Clear();
            foreach (var pair in layers) if (!pair.Key) deadObjects.Add(pair.Key);
            foreach (var key in deadObjects) layers.Remove(key);
            foreach (var pair in cameras) if (!pair.Key) deadCanvases.Add(pair.Key);
            foreach (var key in deadCanvases) cameras.Remove(key);
        }
        public void Restore()
        {
            foreach (var pair in layers) if (pair.Key) pair.Key.layer = pair.Value;
            foreach (var pair in cameras) if (pair.Key) pair.Key.worldCamera = pair.Value;
            layers.Clear(); cameras.Clear();
        }
        private void OnDisable() => Restore();
    }
}
