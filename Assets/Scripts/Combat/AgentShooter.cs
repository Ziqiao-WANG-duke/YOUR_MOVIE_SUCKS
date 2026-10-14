using UnityEngine;
using UnityEngine.InputSystem;
using YourMovieSucks.Input;
using YourMovieSucks.Typing;

namespace YourMovieSucks.Combat
{
    /// <summary>
    /// 特工轨射击：逐字母开火（仅视觉反馈），打完全词直接击杀。
    /// </summary>
    public class AgentShooter : MonoBehaviour
    {
        [SerializeField] private TypingController typing;

        public event System.Action OnFired;

        private void OnEnable()
        {
            if (typing == null) typing = GetComponent<TypingController>();
            if (typing == null) typing = Object.FindFirstObjectByType<TypingController>();
            if (typing == null)
            {
                Debug.LogError("[AgentShooter] no TypingController found.");
                return;
            }

            typing.OnLetterMatched += HandleLetterMatched;
            typing.OnLetterMismatch += HandleLetterMismatch;
            typing.OnWordCompleted += HandleWordCompleted;
        }

        private void OnDisable()
        {
            if (typing != null)
            {
                typing.OnLetterMatched -= HandleLetterMatched;
                typing.OnLetterMismatch -= HandleLetterMismatch;
                typing.OnWordCompleted -= HandleWordCompleted;
            }
        }

        private void HandleLetterMatched(TypingWord word, int progress)
        {
            if (word.Track != CharacterTrack.Agent) return;

            var target = word.Target != null ? word.Target.GetComponent<TypingTarget>() : null;
            if (target == null) return;

            target.Hit(); // 命中闪光，无伤害
            OnFired?.Invoke();
        }

        private void HandleWordCompleted(TypingWord word)
        {
            if (word.Track != CharacterTrack.Agent) return;

            var target = word.Target != null ? word.Target.GetComponent<TypingTarget>() : null;
            if (target == null) return;

            target.Kill(); // 打完词直接击杀
        }

        private void HandleLetterMismatch(TypingWord word, Key key)
        {
            // 失误惩罚：统一让“离玩家最近、还带着词条、且还在走”的那只怪加速逼近。
            // ① 锁定中的特工轨词条打错 → 直接罚这只（对齐设计：对应怪物加速靠近）
            // ② 纯误击（按的字母没锁上任何词条）/ 导演轨 / 输入门 → 罚最近那只
            var target = word != null && word.Track == CharacterTrack.Agent && word.Target != null
                ? word.Target.GetComponent<TypingTarget>()
                : null;

            if (target == null) target = FindMistypeTarget();
            if (target != null) target.Rush();
        }

        /// <summary>失误时该罚哪只：离相机最近、还带着非门词条、且不在攻击态（攻击态的 Rush 是无效的）。</summary>
        private TypingTarget FindMistypeTarget()
        {
            var camera = Camera.main;
            TypingTarget best = null;
            float bestDistance = float.MaxValue;

            foreach (var candidate in Object.FindObjectsByType<TypingTarget>(FindObjectsSortMode.None))
            {
                if (candidate == null || candidate.IsDead || candidate.IsAttacking) continue;
                if (!HasActiveWord(candidate)) continue;

                float distance = camera != null
                    ? (candidate.transform.position - camera.transform.position).sqrMagnitude
                    : candidate.transform.position.sqrMagnitude;

                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = candidate;
            }

            return best;
        }

        private bool HasActiveWord(TypingTarget target)
        {
            if (typing == null) return false;

            foreach (var word in typing.Words)
            {
                if (word == null || word.IsGate || word.Target == null) continue;
                if (word.Target == target.gameObject) return true;
            }

            return false;
        }
    }
}
