using UnityEngine;

namespace YourMovieSucks.Retro
{
    public enum RetroResolution { R320x240, R426x240, R640x480, Native }
    public enum RetroStyle { CleanRetro, ArcadeHorror, AggressivePS1 }
    public enum RetroShading { Smooth, Faceted }

    [CreateAssetMenu(menuName = "Retro Rendering/Preset")]
    public sealed class RetroPreset : ScriptableObject
    {
        public RetroResolution resolution = RetroResolution.R426x240;
        public FilterMode filtering = FilterMode.Point;
        public bool quantization = true;
        [Range(2, 256)] public int colorLevels = 32;
        [Range(0, 1)] public float dithering;
        [Range(0, 1)] public float vertexSnapping = 0.15f;
        [Range(0, 1)] public float affineMapping = 0.15f;
        public RetroShading shading = RetroShading.Faceted;
    }
}
