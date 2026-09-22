using UnityEngine;
using KingdomIdle.MageTower;
using KingdomIdle.Balance;
using Scripts.Core;

namespace KingdomIdle.UGUI
{
    public static class MageSkillPresentation
    {
        public static string PowerSummary(MageTowerSkillSO skill, long power, bool bloom)
        {
            long effective = bloom ? BalanceMath.Damage(power, (decimal)skill.bloomPowerMultiplier) : power;
            string label = bloom && skill.spellKind == MageSpellKind.ArcaneVolley ? "충돌" :
                bloom && skill.spellKind == MageSpellKind.IceSpike ? "단일" : skill.IsHealing ? "1회 회복" : "1회 피해";
            return $"{label} {NumberNotation.Format(effective)}";
        }

        public static string Description(MageTowerSkillSO skill, bool bloom)
        {
            if (!bloom) return skill.description;
            return skill.spellKind switch
            {
                MageSpellKind.Lightning => "뇌운을 모아 넓은 범위에 거대한 벼락을 내립니다.",
                MageSpellKind.IceSpike => "적이 하나면 거대한 빙정으로 타격하고, 여러 적이면 얼음 송곳을 두 차례 생성합니다.",
                MageSpellKind.ArcaneVolley => "거대한 운석으로 타격한 뒤 붉은 균열 장판으로 지속 피해를 주고 적을 감속합니다.",
                _ => skill.description
            };
        }

        public static string EffectivePower(MageTowerSkillSO skill, long power, bool bloom)
        {
            string unit=skill.IsHealing ? "회복" : "피해";
            if (!bloom) return $"최종 1회 {unit}: {NumberNotation.Format(power)}";
            string primary=NumberNotation.Format(BalanceMath.Damage(power,(decimal)skill.bloomPowerMultiplier));
            if (skill.spellKind==MageSpellKind.ArcaneVolley)
                return $"충돌 피해: {primary}\n장판 1틱: {NumberNotation.Format(BalanceMath.Damage(power,(decimal)skill.bloomAreaPowerMultiplier))}";
            if (skill.spellKind==MageSpellKind.IceSpike)
                return $"단일 적 피해: {primary}\n다수 적 1회: {NumberNotation.Format(BalanceMath.Damage(power,(decimal)skill.bloomAreaPowerMultiplier))}";
            return $"최종 1회 {unit}: {primary}";
        }

        public static string AwakeningEffects(MageTowerSkillSO skill, int awakening, bool bloom)
        {
            string unit = skill.IsHealing ? "회복" : "공격";
            string count = skill.spellKind == MageSpellKind.Meteor
                ? "운석 1회 + 잔열 2회 (각성으로 횟수 증가 없음)"
                : $"기본 {unit} {skill.baseHits}회 → 4각성 {MageSkillRules.HitCount(skill, 4)}회 → 8각성 {MageSkillRules.HitCount(skill, 8)}회";
            string current = skill.spellKind == MageSpellKind.Meteor ? "" : $"\n현재 기본 시전: {MageSkillRules.HitCount(skill, awakening)}회";
            if (skill.spellKind == MageSpellKind.FireTornado || skill.spellKind == MageSpellKind.VenomMist || skill.spellKind == MageSpellKind.Sanctuary || skill.spellKind == MageSpellKind.VoidRift)
                current += $" · 지속 {MageSkillRules.HitCount(skill, awakening) * skill.tickInterval:0.0}초";
            if (bloom && (skill.spellKind == MageSpellKind.Lightning || skill.spellKind == MageSpellKind.IceSpike || skill.spellKind == MageSpellKind.ArcaneVolley))
                current = "\n개화 사용 중: 아래 개화 전용 횟수·효과 적용";
            return $"각성마다 기본 {unit}량 +5%, 공격력 반영 배율 +2.5%, 기본 쿨타임 -2% (합산)\n{count}{current}\n10각성: 개화 전환 해금";
        }
        public static string NextAwakening(MageTowerSkillSO skill, int enhance, int awakening, bool bloom)
        {
            if (awakening >= skill.maxAwakeningLevel) return "최대 각성입니다.";
            int next = awakening + 1;
            long partyAttack = MageTowerManager.Instance != null ? MageTowerManager.Instance.PartyAttack : 0;
            long before = BalanceMath.MageDamage((long)skill.BaseDamage, enhance, awakening, partyAttack);
            long after = BalanceMath.MageDamage((long)skill.BaseDamage, enhance, next, partyAttack);
            float factor = bloom ? skill.bloomCooldownMultiplier : 1;
            float beforeCd = (float)BalanceMath.MageInterval((decimal)skill.baseCooldown, awakening) * factor;
            float afterCd = (float)BalanceMath.MageInterval((decimal)skill.baseCooldown, next) * factor;
            string extra = "";
            if (skill.spellKind != MageSpellKind.Meteor && MageSkillRules.HitCount(skill, next) != MageSkillRules.HitCount(skill, awakening))
                extra = $"\n횟수 {MageSkillRules.HitCount(skill, awakening)} → {MageSkillRules.HitCount(skill, next)}회";
            if (next == MageSkillRules.BloomAwakening) extra += "\n개화 켜기/끄기 해금";
            return $"다음 {next}각성\n강화 1회 {(skill.IsHealing ? "회복" : "피해")} {NumberNotation.Format(before)} → {NumberNotation.Format(after)}\n쿨타임 {beforeCd:0.0} → {afterCd:0.0}초{extra}";
        }

        public static readonly Color Accent = new Color(.56f, .67f, .75f);
        public static readonly Color BloomAccent = new Color(.72f, .65f, .84f);
        public static readonly Color Frame = new Color(.35f, .40f, .48f);
    }
}
