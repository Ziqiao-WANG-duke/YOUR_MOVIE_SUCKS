using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YourMovieSucks.UI;

namespace YourMovieSucks.EditorTools
{
    /// <summary>
    /// Lab C 交付需要的场景接线（一次性，可重复执行）：
    /// 把标题画面（TitleScreen）与 VFX 开关（VfxSwitch）挂到 Hud 上并保存场景。
    /// 菜单：YourMovieSucks/Lab C/接线：标题画面 + VFX 开关
    /// </summary>
    public static class LabCSetup
    {
        private const string ScenePath = "Assets/Scenes/PolePosition.unity";

        [MenuItem("YourMovieSucks/Lab C/接线：标题画面 + VFX 开关")]
        public static void WireFromMenu() => Wire();

        /// <summary>批处理入口（编辑器没开时用）。</summary>
        public static void WireFromCommandLine()
        {
            bool ok = Wire();
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static bool Wire()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject hud = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "Hud") { hud = root; break; }
            }

            if (hud == null)
            {
                Debug.LogError("[LabCSetup] 场景里找不到 Hud，未接线");
                return false;
            }

            bool changed = false;
            if (hud.GetComponent<TitleScreen>() == null)
            {
                hud.AddComponent<TitleScreen>();
                changed = true;
            }
            if (hud.GetComponent<VfxSwitch>() == null)
            {
                hud.AddComponent<VfxSwitch>();
                changed = true;
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            Debug.Log($"[LabCSetup] Hud 上已有 TitleScreen={hud.GetComponent<TitleScreen>() != null}, VfxSwitch={hud.GetComponent<VfxSwitch>() != null}；场景已保存={changed}");
            return true;
        }
    }
}
