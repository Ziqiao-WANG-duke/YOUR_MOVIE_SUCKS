using System.Collections.Generic;
using UnityEngine;

namespace YourMovieSucks.Combat
{
    /// <summary>
    /// 怪物攻击协调：同一时刻最多一只怪物攻击玩家。
    /// </summary>
    public class MonsterAttackCoordinator : MonoBehaviour
    {
        private TypingTarget _activeAttacker;
        private readonly List<TypingTarget> _waiting = new();

        public TypingTarget ActiveAttacker => _activeAttacker;

        public bool RequestAttack(TypingTarget target)
        {
            if (target == null || target.IsDead) return false;

            if (_activeAttacker == null || _activeAttacker == target)
            {
                _activeAttacker = target;
                return true;
            }

            if (!_waiting.Contains(target))
                _waiting.Add(target);
            return false;
        }

        public void ReleaseAttack(TypingTarget target)
        {
            if (_activeAttacker == target)
                _activeAttacker = null;

            _waiting.Remove(target);

            // 等待队列中选一只接替
            for (int i = 0; i < _waiting.Count; i++)
            {
                var next = _waiting[i];
                if (next == null || next.IsDead) continue;

                _waiting.RemoveAt(i);
                _activeAttacker = next;
                next.StartAttack();
                break;
            }
        }
    }
}
