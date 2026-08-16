using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KingdomIdle.Divine;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 신 스킬 컬렉션 카드 셀. 프리팹 Item_DivineCard. 인스펙터 편집 가능.
    /// 팝업이 8칸을 1회 생성 후 재사용한다 — Set()은 바뀐 값만 다시 쓴다(모바일 할당/리빌드 절약).
    /// 미보유 셀도 탭할 수 있다(상세 페인에 미보유 안내를 띄우기 위해 입력을 막지 않는다).
    /// </summary>
    public sealed class DivineCardItemView : MonoBehaviour
    {
        public Button button;
        public Image background;      // 안쪽 배경 (등급색 다크 틴트)
        public Image gradeFrame;      // 등급색 테두리 (미보유 = 회색)
        public Image selectedFrame;   // 선택 강조 테두리 (평소 비활성)
        public Image icon;            // 카드 아이콘 (미보유 = 근흑 실루엣)
        public TMP_Text iconFallback; // 아이콘 없을 때 "?" 표시 (흰 박스 방지)
        public TMP_Text nameLabel;    // 미보유 = "???"
        public TMP_Text levelLabel;   // "Lv.N" (미보유/0레벨 숨김)
        public Image lockOverlay;     // 미보유 어둡게
        public Image equippedPill;    // "장착" 알약 (좌상단)
        public Image dupBadge;        // "+N" 중복 배지 (우상단)
        public TMP_Text dupLabel;

        private static readonly Color SilhouetteTint = new Color(0.07f, 0.06f, 0.09f, 1f);
        private static readonly Color BgDark = new Color(0.11f, 0.12f, 0.17f, 1f);

        // 마지막 표시 상태 — 같은 값이면 텍스트/색을 다시 쓰지 않는다 (제자리 갱신용)
        private DivineSkillSO _card;
        private bool _hasState;
        private bool _owned, _equipped, _selected;
        private int _level = -1, _dups = -1;

        private Action _onClick;
        private bool _clickHooked;

        /// <summary>셀 표시 갱신. 팝업 Refresh마다 호출되지만 바뀐 항목만 실제로 쓴다.</summary>
        public void Set(DivineSkillSO card, bool owned, int level, int dups, bool equipped, bool selected, Action onClick)
        {
            // 클릭 리스너는 1회만 배선 — Refresh마다 Remove/Add로 델리게이트를 재생성하지 않는다
            _onClick = onClick;
            if (!_clickHooked && button != null)
            {
                button.onClick.AddListener(() => _onClick?.Invoke());
                _clickHooked = true;
            }

            bool fullRedraw = !_hasState || _card != card || _owned != owned;

            if (fullRedraw)
            {
                Color grade = card != null ? DivineSkillSO.GetGradeColor(card.grade) : UguiTheme.RarityNormal;

                if (gradeFrame != null) gradeFrame.color = owned ? grade : UguiTheme.DisabledGrey;
                if (background != null)
                    background.color = owned ? Color.Lerp(grade, BgDark, 0.72f) : BgDark;

                // 아이콘: 있으면 표시(미보유 = 근흑 실루엣), 없으면 "?" 폴백 — 흰 박스 금지
                bool hasIcon = card != null && card.icon != null;
                if (icon != null)
                {
                    icon.enabled = hasIcon;
                    if (hasIcon) icon.sprite = card.icon;
                    icon.color = owned ? Color.white : SilhouetteTint;
                }
                if (iconFallback != null) iconFallback.gameObject.SetActive(!hasIcon);

                if (nameLabel != null)
                {
                    nameLabel.text = owned ? (card != null ? card.DisplayName : "-") : "???";
                    nameLabel.color = owned ? UguiTheme.TextPrimary : UguiTheme.TextTertiary;
                }

                if (lockOverlay != null) lockOverlay.gameObject.SetActive(!owned);
            }

            // 레벨 배지 — 표시값이 바뀐 경우에만 문자열 생성
            bool showLv = owned && level > 0;
            if (levelLabel != null)
            {
                if (levelLabel.gameObject.activeSelf != showLv) levelLabel.gameObject.SetActive(showLv);
                if (showLv && (_level != level || fullRedraw))
                    levelLabel.text = "Lv." + level;
            }

            // 중복 배지
            bool showDup = owned && dups > 0;
            if (dupBadge != null && dupBadge.gameObject.activeSelf != showDup)
                dupBadge.gameObject.SetActive(showDup);
            if (showDup && dupLabel != null && (_dups != dups || fullRedraw))
                dupLabel.text = "+" + dups;

            if (equippedPill != null && (!_hasState || _equipped != equipped))
                equippedPill.gameObject.SetActive(equipped);

            if (selectedFrame != null && (!_hasState || _selected != selected))
                selectedFrame.gameObject.SetActive(selected);

            _card = card;
            _owned = owned;
            _level = level;
            _dups = dups;
            _equipped = equipped;
            _selected = selected;
            _hasState = true;
        }
    }
}
