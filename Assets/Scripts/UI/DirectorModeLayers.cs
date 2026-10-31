namespace YourMovieSucks.UI
{
    /// <summary>
    /// 导演模式专用图层。弱点标记放在这一层：低分辨率世界相机不渲染它，
    /// 而原生分辨率的表现相机渲染它，所以它永远不经过黑白/量化合成，保持纯红。
    /// </summary>
    public static class DirectorModeLayers
    {
        /// <summary>与 RetroRenderingController.weakPointLayer 一致（Unity 内置空层 6）。</summary>
        public const int WeakPoint = 6;

        /// <summary>UI 层（世界空间词条、HUD 画布）。</summary>
        public const int UI = 5;
    }
}
