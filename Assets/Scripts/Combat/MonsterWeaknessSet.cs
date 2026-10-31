using System;
using UnityEngine;

namespace YourMovieSucks.Combat
{
    /// <summary>
    /// 弱点词池（数据配置）：每种怪物类型/每个场景一份，列出若干「弱点部位 + 弱点单词 + 骨骼」。
    ///
    /// 用法：同屏最多 5 只怪 → 池里放 5 条、首字母互不相同，每只怪出生时分到一条没被占用的 →
    /// 场上永远不会出现两个同首字母的弱点词，现有「按首字母吸附锁定」就不会歧义。
    /// 新增怪物只加数据，不需要改代码或在关卡里硬编码。
    ///
    /// 注意：弱点被破坏后换上的新词不在这里配，而在战斗编辑器（CombatEncounter 资产）里按怪物类型配。
    /// </summary>
    [CreateAssetMenu(fileName = "WeaknessSet", menuName = "YourMovieSucks/Monster Weakness Set", order = 0)]
    public class MonsterWeaknessSet : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [Tooltip("部位名（显示用，如 HEAD）")]
            public string partLabel = "HEAD";

            [Tooltip("导演轨要输入的弱点词（只算字母，大写显示）")]
            public string weakWord = "HEAD";

            [Tooltip("弱点部位对应的骨骼名（在模型层级里递归查找），如 mixamorig:Head；留空则用怪物根节点")]
            public string boneName = "mixamorig:Head";
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public int Count => entries != null ? entries.Length : 0;
        public Entry[] Entries => entries;

        public Entry Get(int index)
            => entries != null && index >= 0 && index < entries.Length ? entries[index] : null;
    }
}
