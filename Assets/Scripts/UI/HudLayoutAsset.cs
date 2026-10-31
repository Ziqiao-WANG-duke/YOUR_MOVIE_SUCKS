using System;
using UnityEngine;

namespace YourMovieSucks.UI
{
    /// <summary>单个 HUD 元素的布局：位置偏移、缩放、透明度、显隐。</summary>
    [Serializable]
    public sealed class HudElementLayout
    {
        public string name = "HUD";
        public bool visible = true;
        [Tooltip("相对默认位置的像素偏移（+X 右，+Y 上）")]
        public Vector2 offset = Vector2.zero;
        [Range(0.2f, 3f)] public float scale = 1f;
        [Range(0f, 1f)] public float alpha = 1f;
    }

    /// <summary>HUD 布局资产：运行时可调、可保存，下次运行沿用。</summary>
    [CreateAssetMenu(menuName = "YourMovieSucks/HUD Layout", fileName = "HudLayout")]
    public sealed class HudLayoutAsset : ScriptableObject
    {
        public HudElementLayout[] elements = Array.Empty<HudElementLayout>();

        public HudElementLayout Get(string elementName)
        {
            if (elements == null) return null;
            foreach (var e in elements)
                if (e != null && e.name == elementName) return e;
            return null;
        }

        public HudElementLayout Ensure(string elementName)
        {
            var found = Get(elementName);
            if (found != null) return found;

            found = new HudElementLayout { name = elementName };
            var list = new System.Collections.Generic.List<HudElementLayout>(elements ?? Array.Empty<HudElementLayout>());
            list.Add(found);
            elements = list.ToArray();
            return found;
        }

        /// <summary>把某个元素恢复默认。</summary>
        public void Reset(string elementName)
        {
            var e = Get(elementName);
            if (e == null) return;
            e.offset = Vector2.zero;
            e.scale = 1f;
            e.alpha = 1f;
            e.visible = true;
        }
    }
}
