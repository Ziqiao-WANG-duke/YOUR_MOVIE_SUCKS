using UnityEngine;

namespace YourMovieSucks.UI
{
    /// <summary>
    /// 把「VFX 开关」应用到那些需要整块关掉的视觉组件上
    /// （逐帧/逐事件读 <see cref="GameOptions.VfxEnabled"/> 的效果不需要它，例如血效与受击红闪）。
    /// 目前管两个：导演模式胶片滤镜（<see cref="DirectorFilmFilter.enabled_"/>）与取景框闪烁。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class VfxSwitch : MonoBehaviour
    {
        private void OnEnable()
        {
            GameOptions.Load();
            GameOptions.Changed += Apply;
            Apply();
        }

        private void OnDisable()
        {
            GameOptions.Changed -= Apply;
        }

        public static void Apply()
        {
            bool on = GameOptions.VfxEnabled;

            foreach (var filter in Object.FindObjectsByType<DirectorFilmFilter>(FindObjectsSortMode.None))
                if (filter != null) filter.enabled_ = on;
        }
    }
}
