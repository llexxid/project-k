using System;
using UnityEngine;

namespace KingdomIdle.Divine
{
    /// <summary>
    /// 신 스킬이 파티에 거는 일시 버프의 전역 상태.
    /// 전투 코드가 매 프레임 값만 읽어 쓰는 얇은 레이어로, 버프 인스턴스 목록을 두지 않는다.
    /// (같은 종류의 버프가 겹치면 더 강한 쪽 / 더 늦은 만료 시각으로 갱신)
    ///
    /// 소비처
    ///  - Player.TakeDamage      → ApplyDamageReduction
    ///  - ActiveSkill 쿨다운      → SkillIntervalMult
    ///  - PlayerOrder.SyncMoveSpeed → MoveSpeedMult
    /// </summary>
    public static class DivineBuffState
    {
        /// <summary>받는 피해 배율. 1 = 감소 없음.</summary>
        public static float DamageTakenMult { get; private set; } = 1f;

        /// <summary>기본 스킬 간격 배율. 1 = 변화 없음, 0.7 = 간격 -30%.</summary>
        public static float SkillIntervalMult { get; private set; } = 1f;

        /// <summary>이동속도 배율. 1 = 변화 없음.</summary>
        public static float MoveSpeedMult { get; private set; } = 1f;

        private static float _guardEndTime;
        private static float _hasteEndTime;

        /// <summary>버프가 걸리거나 만료될 때 발생. 이동속도 재동기화 등에 사용.</summary>
        public static event Action OnChanged;

        public static bool GuardActive => _guardEndTime > 0f;
        public static bool HasteActive => _hasteEndTime > 0f;

        /// <summary>남은 버프 시간(초) 중 가장 긴 값. HUD 표시용.</summary>
        public static float RemainingSeconds =>
            Mathf.Max(0f, Mathf.Max(_guardEndTime, _hasteEndTime) - Time.time);

        /// <summary>받는 피해 감소 버프. reducePercent 0.2 = 피해 -20%.</summary>
        public static void ApplyGuard(float reducePercent, float duration)
        {
            if (duration <= 0f) return;
            float mult = Mathf.Clamp01(1f - reducePercent);

            DamageTakenMult = Mathf.Min(DamageTakenMult, mult);
            _guardEndTime = Mathf.Max(_guardEndTime, Time.time + duration);
            OnChanged?.Invoke();
        }

        /// <summary>스킬 간격 단축 + 이동속도 증가 버프.</summary>
        public static void ApplyHaste(float intervalReducePercent, float moveSpeedIncreasePercent, float duration)
        {
            if (duration <= 0f) return;

            SkillIntervalMult = Mathf.Min(SkillIntervalMult, Mathf.Clamp(1f - intervalReducePercent, 0.1f, 1f));
            MoveSpeedMult = Mathf.Max(MoveSpeedMult, 1f + Mathf.Max(0f, moveSpeedIncreasePercent));
            _hasteEndTime = Mathf.Max(_hasteEndTime, Time.time + duration);
            OnChanged?.Invoke();
        }

        /// <summary>만료 검사. DivineSkillManager.Update 에서 매 프레임 호출한다.</summary>
        public static void Tick()
        {
            bool changed = false;

            if (_guardEndTime > 0f && Time.time >= _guardEndTime)
            {
                _guardEndTime = 0f;
                DamageTakenMult = 1f;
                changed = true;
            }

            if (_hasteEndTime > 0f && Time.time >= _hasteEndTime)
            {
                _hasteEndTime = 0f;
                SkillIntervalMult = 1f;
                MoveSpeedMult = 1f;
                changed = true;
            }

            if (changed) OnChanged?.Invoke();
        }

        /// <summary>스테이지 전환·환생 등으로 전투 상태를 초기화할 때 호출.</summary>
        public static void ClearAll()
        {
            bool changed = _guardEndTime > 0f || _hasteEndTime > 0f;

            _guardEndTime = 0f;
            _hasteEndTime = 0f;
            DamageTakenMult = 1f;
            SkillIntervalMult = 1f;
            MoveSpeedMult = 1f;

            if (changed) OnChanged?.Invoke();
        }

        /// <summary>받는 피해에 감소 버프를 적용한 값. 최소 1은 보장한다.</summary>
        public static ulong ApplyDamageReduction(ulong damage)
        {
            if (DamageTakenMult >= 0.9999f || damage == 0UL) return damage;
            double reduced = damage * DamageTakenMult;
            return reduced < 1d ? 1UL : (ulong)reduced;
        }
    }
}
