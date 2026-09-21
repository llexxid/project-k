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

        /// <summary>메인 화면 바인딩 시 호출한다. 이전 화면 참조를 비우고 새 화면의 다섯 대상을 연결한다.</summary>
        public void Register(MainScreenView owner)
        {
            _owner = owner;
            _targets.Clear();
            if (owner == null) return;
            _targets[GameDirectTarget.Development] = owner.tabDevelopment != null ? owner.tabDevelopment.transform as RectTransform : null;
            _targets[GameDirectTarget.KingdomArmy] = owner.tabKingdomArmy != null ? owner.tabKingdomArmy.transform as RectTransform : null;
            _targets[GameDirectTarget.Dungeon] = owner.tabDungeon != null ? owner.tabDungeon.transform as RectTransform : null;
            _targets[GameDirectTarget.Gacha] = owner.tabGacha != null ? owner.tabGacha.transform as RectTransform : null;
            _targets[GameDirectTarget.Reincarnation] = owner.btnReincarnation != null ? owner.btnReincarnation.transform as RectTransform : null;
        }

        /// <summary>해제 중인 화면이 여전히 소유자인 경우만 비운다. 오래된 Dispose가 새 화면 등록을 지우지 않는다.</summary>
        public void Unregister(MainScreenView owner)
        {
            if (_owner != owner) return;
            _owner = null;
            _targets.Clear();
        }

        /// <summary>살아 있고 표시 중이며 크기가 있는 대상만 반환한다. 매번 하이어라키를 검색하거나 할당하지 않는다.</summary>
        public bool TryGet(GameDirectTarget id, out RectTransform target)
        {
            target = null;
            return _owner != null && _owner.gameObject.activeInHierarchy && _targets.TryGetValue(id, out target) &&
                target != null && target.gameObject.activeInHierarchy && target.rect.width > 0 && target.rect.height > 0;
        }
    }
}
