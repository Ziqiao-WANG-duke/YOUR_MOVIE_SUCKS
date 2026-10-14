using UnityEngine;
using YourMovieSucks.Input;
using YourMovieSucks.Typing;

namespace YourMovieSucks.Combat
{
    /// <summary>
    /// 1.x 测试用：搭建灯光（初始暗区）、生成占位敌人并绑定词条，击杀触发糊血 + 擦镜词条。
    /// </summary>
    public class CombatTestSetup : MonoBehaviour
    {
        [SerializeField] private TypingController typing;
        [SerializeField] private ScreenBlood blood;

        private void Start()
        {
            if (typing == null) typing = GetComponent<TypingController>();
            if (blood == null) blood = GetComponent<ScreenBlood>();
            if (typing == null || blood == null) return;

            CreateLighting();
            SpawnEnemies();
        }

        private void CreateLighting()
        {
            var lightGo = new GameObject("StageLight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.intensity = 6f;
            light.color = new Color(1f, 0.4f, 0.85f); // 霓虹粉紫
            lightGo.transform.position = new Vector3(0f, 3f, 4f);

            lightGo.AddComponent<StageLighting>(); // 默认初始熄灭（暗区）

            // 导演打光词条绑定灯光
            typing.SpawnWord("NEON", CharacterTrack.Director, 0f, lightGo);
        }

        private void SpawnEnemies()
        {
            SpawnEnemy(new Vector3(-1.5f, 1.5f, 5f), "COP");
            SpawnEnemy(new Vector3(0f, 1.5f, 5.5f), "FAT");
            SpawnEnemy(new Vector3(1.5f, 1.5f, 5f), "BANG");
        }

        private void SpawnEnemy(Vector3 position, string word)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Enemy_" + word;
            cube.transform.position = position;

            var renderer = cube.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = Color.red;

            var target = cube.AddComponent<TypingTarget>();
            target.OnDeath += () =>
            {
                blood.Splatter();
                typing.SpawnWord("WIPE", CharacterTrack.Director, 0f, null);
            };

            typing.SpawnWord(word, CharacterTrack.Agent, 0f, cube);
        }
    }
}
