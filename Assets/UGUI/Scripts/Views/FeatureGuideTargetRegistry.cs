using System.Collections.Generic;
using Direction;
using UnityEngine;

namespace KingdomIdle.UGUI
{
    /// <summary>현재 메인 화면의 실제 버튼 위치를 ID로 찾는다. 원본 버튼의 부모·이벤트·선택 상태는 변경하지 않는다.</summary>
    public sealed class FeatureGuideTargetRegistry
    {
        private readonly Dictionary<GameDirectTarget, RectTransform> _targets = new();
        private MainScreenView _owner;

        /// <summary>메인 바인딩 시 이전 메인의 다섯 대상만 교체한다. 별도 수명인 마탑·팝업 등록은 보존한다.</summary>
        public void Register(MainScreenView owner)
        {
            Unregister(_owner);
            _owner = owner;
            if (owner == null) return;
            _targets[GameDirectTarget.Development] = owner.tabDevelopment != null ? owner.tabDevelopment.transform as RectTransform : null;
            _targets[GameDirectTarget.KingdomArmy] = owner.tabKingdomArmy != null ? owner.tabKingdomArmy.transform as RectTransform : null;
            _targets[GameDirectTarget.Dungeon] = owner.tabDungeon != null ? owner.tabDungeon.transform as RectTransform : null;
            _targets[GameDirectTarget.Gacha] = owner.tabGacha != null ? owner.tabGacha.transform as RectTransform : null;
            _targets[GameDirectTarget.Reincarnation] = owner.btnReincarnation != null ? owner.btnReincarnation.transform as RectTransform : null;
            FeatureGuideAnchor.Bind(owner.tabDevelopment?.button, GameDirectTarget.Development);
            FeatureGuideAnchor.Bind(owner.tabKingdomArmy?.button, GameDirectTarget.KingdomArmy);
            FeatureGuideAnchor.Bind(owner.tabDungeon?.button, GameDirectTarget.Dungeon);
            FeatureGuideAnchor.Bind(owner.tabGacha?.button, GameDirectTarget.Gacha);
        }

        /// <summary>해제 중인 화면이 소유자일 때 메인 대상만 비운다. 이전 Dispose가 새 화면이나 독립 HUD를 지우지 않는다.</summary>
        public void Unregister(MainScreenView owner)
        {
            if (_owner != owner) return;
            _owner = null;
            _targets.Remove(GameDirectTarget.Development);
            _targets.Remove(GameDirectTarget.KingdomArmy);
            _targets.Remove(GameDirectTarget.Dungeon);
            _targets.Remove(GameDirectTarget.Gacha);
            _targets.Remove(GameDirectTarget.Reincarnation);
        }

        /// <summary>패널 컨트롤러의 실제 UI를 등록한다. 같은 논리 ID가 재생성되면 최신 참조로 교체한다.</summary>
        public void RegisterTarget(GameDirectTarget id, RectTransform target) => _targets[id] = target;

        /// <summary>소유 RectTransform이 일치할 때만 삭제한다. 지연 Destroy가 새 대상 등록을 덮지 않는다.</summary>
        public void UnregisterTarget(GameDirectTarget id, RectTransform target)
        { if (_targets.TryGetValue(id, out var current) && current == target) _targets.Remove(id); }

        /// <summary>살아 있고 표시 중이며 크기가 있는 대상만 반환한다. 매번 하이어라키를 검색하거나 할당하지 않는다.</summary>
        public bool TryGet(GameDirectTarget id, out RectTransform target)
        {
            target = null;
            return _targets.TryGetValue(id, out target) &&
                target != null && target.gameObject.activeInHierarchy && target.rect.width > 0 && target.rect.height > 0;
        }
    }
}
