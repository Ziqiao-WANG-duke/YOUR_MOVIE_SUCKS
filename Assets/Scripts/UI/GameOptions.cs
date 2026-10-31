using UnityEngine;

namespace YourMovieSucks.UI
{
    /// <summary>
    /// 全局玩家选项（Lab C 交付要求：可以静音、可以把 VFX 关掉）。
    /// 只放「玩家能改、要记住」的开关；数值型调参仍在 LabBTuning / HUD 布局资产里。
    /// 用 PlayerPrefs 记住，下次启动沿用。
    /// </summary>
    public static class GameOptions
    {
        private const string MutedKey = "ymu.muted";
        private const string VfxKey = "ymu.vfx";

        private static bool _loaded;
        private static bool _muted;
        private static bool _vfx = true;

        /// <summary>任意选项变化（静音 / VFX）时触发，界面用来刷新提示、VfxSwitch 用来应用。</summary>
        public static event System.Action Changed;

        public static bool Muted
        {
            get { Load(); return _muted; }
            set
            {
                Load();
                if (_muted == value) return;
                _muted = value;
                PlayerPrefs.SetInt(MutedKey, value ? 1 : 0);
                PlayerPrefs.Save();
                ApplyMute();
                Changed?.Invoke();
            }
        }

        public static bool VfxEnabled
        {
            get { Load(); return _vfx; }
            set
            {
                Load();
                if (_vfx == value) return;
                _vfx = value;
                PlayerPrefs.SetInt(VfxKey, value ? 1 : 0);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        public static void ToggleMute() => Muted = !Muted;
        public static void ToggleVfx() => VfxEnabled = !VfxEnabled;

        /// <summary>启动时调用一次：把存下来的选项应用到运行时。</summary>
        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _muted = PlayerPrefs.GetInt(MutedKey, 0) == 1;
            _vfx = PlayerPrefs.GetInt(VfxKey, 1) == 1;
            ApplyMute();
        }

        private static void ApplyMute()
        {
            // 直接压总音量：BGM / SFX / VO 都是 AudioSource，统一走 AudioListener
            AudioListener.volume = _muted ? 0f : 1f;
        }
    }
}
