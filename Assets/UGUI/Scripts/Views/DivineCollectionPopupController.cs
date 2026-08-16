using System;
using System.Collections.Generic;
using UnityEngine;
using KingdomIdle.Divine;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 신 스킬 컬렉션북(도감) 팝업 컨트롤러 (프리팹 기반).
    /// 프리팹 Popup_DivineCollection(=DivineCollectionPopupView)을 1회 인스턴스화해 캐시하고,
    /// 카드 그리드는 Item_DivineCard 프리팹을 1회 생성 후 제자리 갱신한다(파괴/재생성 없음 —
    /// 카드 종수가 바뀐 경우에만 다시 만든다). 코드로 UI 구조를 생성하지 않는다.
    ///  - 셀 탭 → 상세 페인 갱신 (미보유 카드도 선택 가능 — 미보유 안내 표시)
    ///  - [장착]/[레벨업] → DivineSkillManager 호출 → OnStateChanged 이벤트 경로로 갱신 (Update 폴링 없음)
    ///  - OnStateChanged는 열려 있는 동안만 구독하고 닫을 때 해제한다
    /// </summary>
    public static class DivineCollectionPopupController
    {
        private static DivineCollectionPopupView _view;
        private static readonly List<DivineCardItemView> _cells = new();
        private static readonly List<DivineSkillSO> _sortedCards = new();   // 등급 → id 순, 셀과 1:1
        private static readonly List<Action> _cellClicks = new();           // 셀별 클릭 델리게이트 (재빌드 시 1회 생성)

        private static int _selectedId = -1;
        private static int _lastSourceCount = -1;   // 이 수가 바뀐 경우에만 셀을 다시 만든다
        private static bool _subscribed;

        private static readonly Color SilhouetteTint = new Color(0.07f, 0.06f, 0.09f, 1f);

        public static bool IsOpen => _view != null && _view.gameObject.activeSelf;

        public static void Show()
        {
            if (!EnsureBuilt()) return;

            var mgr = DivineSkillManager.Instance;
            if (mgr != null && (_selectedId < 0 || mgr.GetCardById(_selectedId) == null))
                _selectedId = PickDefaultSelection(mgr);

            _view.gameObject.SetActive(true);
            _view.transform.SetAsLastSibling();   // BringToFront
            if (_view.panelBox != null) UITween.PopIn(_view.panelBox);

            Subscribe();
            Refresh();
        }

        public static void Hide()
        {
            Unsubscribe();
            if (_view == null) return;
            _view.gameObject.SetActive(false);

            // 장착/레벨 변경이 HUD에 바로 보이도록
            if (DivineSkillHudController.Instance != null)
                DivineSkillHudController.Instance.Refresh();
        }

        // ────────────────────────────────────────────
        //  구독 (열려 있는 동안만)
        // ────────────────────────────────────────────
        private static void Subscribe()
        {
            if (_subscribed) return;
            var mgr = DivineSkillManager.Instance;
            if (mgr == null) return;
            mgr.OnStateChanged += OnStateChanged;
            _subscribed = true;
        }

        private static void Unsubscribe()
        {
            if (!_subscribed) return;
            var mgr = DivineSkillManager.Instance;
            if (mgr != null) mgr.OnStateChanged -= OnStateChanged;
            _subscribed = false;
        }

        private static void OnStateChanged()
        {
            // UI 루트가 재생성돼 뷰가 파괴된 경우 — 죽은 참조/구독을 정리한다
            if (_view == null)
            {
                Unsubscribe();
                _view = null;
                _cells.Clear();
                _cellClicks.Clear();
                _sortedCards.Clear();
                _lastSourceCount = -1;
                return;
            }
            if (!_view.gameObject.activeSelf) return;
            Refresh();
        }

        // ────────────────────────────────────────────
        //  빌드
        // ────────────────────────────────────────────
        private static bool EnsureBuilt()
        {
            if (_view != null) return true;

            var mgr = UIManager.Instance;
            if (mgr == null || mgr.LayerPopups == null || mgr.Catalog == null || mgr.Catalog.popupDivineCollection == null)
            {
                Debug.LogWarning("[DivineCollection] 카탈로그의 popupDivineCollection 프리팹이 없습니다.");
                return false;
            }

            var go = UnityEngine.Object.Instantiate(mgr.Catalog.popupDivineCollection, mgr.LayerPopups, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            _view = go.GetComponent<DivineCollectionPopupView>();
            if (_view == null)
            {
                Debug.LogError("[DivineCollection] DivineCollectionPopupView 컴포넌트가 없습니다.");
                UnityEngine.Object.Destroy(go);
                return false;
            }

            _cells.Clear();
            _cellClicks.Clear();
            _sortedCards.Clear();
            _lastSourceCount = -1;

            if (_view.backdropButton != null) _view.backdropButton.onClick.AddListener(Hide);
            if (_view.closeButton != null) _view.closeButton.onClick.AddListener(Hide);
            if (_view.equipButton != null) _view.equipButton.onClick.AddListener(OnEquipClicked);
            if (_view.levelUpButton != null) _view.levelUpButton.onClick.AddListener(OnLevelUpClicked);

            _view.gameObject.SetActive(false);   // 첫 Show가 표시를 제어한다
            return true;
        }

        /// <summary>기본 선택: 장착 카드 → 첫 보유 카드 → 첫 카드.</summary>
        private static int PickDefaultSelection(DivineSkillManager mgr)
        {
            if (mgr.EquippedCard != null) return mgr.EquippedCardId;

            var cards = mgr.GetAllCards();
            int first = -1;
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] == null) continue;
                if (first < 0) first = cards[i].id;
                if (mgr.IsOwned(cards[i].id)) return cards[i].id;
            }
            return first;
        }

        // ────────────────────────────────────────────
        //  갱신 (Show / OnStateChanged / 셀 탭 시에만 — 매 프레임 없음)
        // ────────────────────────────────────────────
        private static void Refresh()
        {
            if (_view == null) return;

            var mgr = DivineSkillManager.Instance;
            RefreshHeader(mgr);
            RefreshGrid(mgr);
            RefreshDetail(mgr);
        }

        private static void RefreshHeader(DivineSkillManager mgr)
        {
            if (_view.bonusLabel == null) return;

            if (mgr == null)
            {
                _view.bonusLabel.text = "컬렉션 보너스: -";
                return;
            }

            float pct = mgr.CollectionBonusRate * 100f;
            _view.bonusLabel.text =
                $"컬렉션 보너스: 공격력·체력 +{pct:0.#}%  ({mgr.OwnedCount}/{mgr.TotalCardCount} 수집)";
        }

        /// <summary>카드 종수가 바뀐 경우에만 셀을 다시 만들고, 평소에는 제자리 갱신한다.</summary>
        private static void RefreshGrid(DivineSkillManager mgr)
        {
            if (_view.cardGrid == null) return;

            var cards = mgr != null ? mgr.GetAllCards() : null;
            int count = cards != null ? cards.Count : 0;

            if (count != _lastSourceCount)
            {
                RebuildCells(cards);
                _lastSourceCount = count;
            }

            for (int i = 0; i < _cells.Count && i < _sortedCards.Count; i++)
            {
                var card = _sortedCards[i];
                if (card == null || _cells[i] == null) continue;

                bool owned = mgr != null && mgr.IsOwned(card.id);
                _cells[i].Set(
                    card,
                    owned,
                    mgr != null ? mgr.GetLevel(card.id) : 0,
                    mgr != null ? mgr.GetDuplicates(card.id) : 0,
                    owned && mgr.IsEquipped(card.id),
                    card.id == _selectedId,
                    _cellClicks[i]);
            }
        }

        private static void RebuildCells(IReadOnlyList<DivineSkillSO> cards)
        {
            // 기존 셀 비활성화 후 파괴 (Destroy 지연 → 레이아웃에 끼지 않게)
            for (int i = _view.cardGrid.childCount - 1; i >= 0; i--)
            {
                var child = _view.cardGrid.GetChild(i).gameObject;
                child.SetActive(false);
                UnityEngine.Object.Destroy(child);
            }
            _cells.Clear();
            _cellClicks.Clear();
            _sortedCards.Clear();

            if (cards == null) return;

            for (int i = 0; i < cards.Count; i++)
                if (cards[i] != null) _sortedCards.Add(cards[i]);
            _sortedCards.Sort(CompareCards);   // 등급 → id 순

            var catalog = UIManager.Instance != null ? UIManager.Instance.Catalog : null;
            if (catalog == null || catalog.itemDivineCard == null)
            {
                Debug.LogWarning("[DivineCollection] 카탈로그의 itemDivineCard 프리팹이 없습니다.");
                _sortedCards.Clear();
                return;
            }

            for (int i = 0; i < _sortedCards.Count; i++)
            {
                var cellGo = UnityEngine.Object.Instantiate(catalog.itemDivineCard, _view.cardGrid, false);
                var cell = cellGo.GetComponent<DivineCardItemView>();
                if (cell == null)
                {
                    Debug.LogError("[DivineCollection] Item_DivineCard 프리팹에 DivineCardItemView가 없습니다.");
                    UnityEngine.Object.Destroy(cellGo);
                }

                int idx = i;
                _cells.Add(cell);                            // 실패 시 null 자리 유지 — 인덱스 1:1 보존
                _cellClicks.Add(() => OnCardClicked(idx));   // 셀당 1회 생성 — Refresh마다 재할당하지 않는다
            }
        }

        private static int CompareCards(DivineSkillSO a, DivineSkillSO b)
        {
            int g = a.grade.CompareTo(b.grade);
            return g != 0 ? g : a.id.CompareTo(b.id);
        }

        private static void OnCardClicked(int index)
        {
            if (index < 0 || index >= _sortedCards.Count) return;
            var card = _sortedCards[index];
            if (card == null || card.id == _selectedId) return;

            _selectedId = card.id;
            // 선택 변경은 상태 이벤트가 없다 — 셀 강조와 상세 페인만 직접 갱신한다
            // (셀은 제자리 diff 갱신이라 선택 테두리가 바뀐 두 칸만 실제로 다시 그린다)
            var mgr = DivineSkillManager.Instance;
            RefreshGrid(mgr);
            RefreshDetail(mgr);
        }

        /// <summary>상세 페인. 선택 변경 또는 OnStateChanged 시에만 호출된다 — 수치를 매 프레임 재계산하지 않는다.</summary>
        private static void RefreshDetail(DivineSkillManager mgr)
        {
            var so = mgr != null && _selectedId >= 0 ? mgr.GetCardById(_selectedId) : null;

            if (so == null)
            {
                // 매니저 부재/카드 없음 — 빈 상세 + 버튼 숨김 (흰 박스/죽은 버튼 금지)
                if (_view.illustration != null) _view.illustration.enabled = false;
                if (_view.cardNameLabel != null) _view.cardNameLabel.text = "-";
                if (_view.gradePill != null) _view.gradePill.gameObject.SetActive(false);
                if (_view.skillNameLabel != null) _view.skillNameLabel.text = "";
                if (_view.descriptionLabel != null) _view.descriptionLabel.text = "";
                if (_view.statCooldownLabel != null) _view.statCooldownLabel.text = "";
                if (_view.statMultiplierLabel != null) _view.statMultiplierLabel.text = "";
                if (_view.statValueLabel != null) _view.statValueLabel.gameObject.SetActive(false);
                if (_view.equipButton != null) _view.equipButton.gameObject.SetActive(false);
                if (_view.levelUpButton != null) _view.levelUpButton.gameObject.SetActive(false);
                if (_view.lockedHintLabel != null) _view.lockedHintLabel.gameObject.SetActive(false);
                return;
            }

            bool owned = mgr.IsOwned(so.id);
            bool equipped = owned && mgr.IsEquipped(so.id);
            Color grade = DivineSkillSO.GetGradeColor(so.grade);

            // 일러스트 → 아이콘 → 없음. 미보유는 근흑 실루엣.
            if (_view.illustration != null)
            {
                var sprite = so.illustration != null ? so.illustration : so.icon;
                _view.illustration.enabled = sprite != null;
                if (sprite != null) _view.illustration.sprite = sprite;
                _view.illustration.color = owned ? Color.white : SilhouetteTint;
            }

            if (_view.cardNameLabel != null)
            {
                _view.cardNameLabel.text = so.DisplayName;
                _view.cardNameLabel.color = grade;
            }

            if (_view.gradePill != null)
            {
                _view.gradePill.gameObject.SetActive(true);
                _view.gradePill.color = grade;
            }
            if (_view.gradePillLabel != null)
                _view.gradePillLabel.text = DivineSkillSO.GetGradeName(so.grade);

            if (_view.skillNameLabel != null) _view.skillNameLabel.text = so.skillNameKor ?? "";
            if (_view.descriptionLabel != null) _view.descriptionLabel.text = so.description ?? "";

            if (_view.statCooldownLabel != null)
                _view.statCooldownLabel.text = $"쿨타임  {so.cooldown:F0}초";
            if (_view.statMultiplierLabel != null)
                _view.statMultiplierLabel.text = $"레벨 배율  x{mgr.GetLevelMultiplier(so.id):F1}";

            // 효과 수치 — 공격형은 현재 파티 스탯 기반 예상 피해, 회복형은 MAXHP 비율.
            // 가속형은 한 줄 수치가 무의미하므로 숨긴다. (프로젝트 큰 수 표기 관례 = N0)
            if (_view.statValueLabel != null)
            {
                if (so.IsOffensive)
                {
                    _view.statValueLabel.gameObject.SetActive(true);
                    _view.statValueLabel.text = $"예상 피해  {mgr.GetCastValue(so):N0}";
                }
                else if (so.effectKind == eDivineEffectKind.HealAndGuard)
                {
                    _view.statValueLabel.gameObject.SetActive(true);
                    _view.statValueLabel.text = $"회복량  최대 체력의 {mgr.GetCastValue(so) * 100d:F0}%";
                }
                else
                {
                    _view.statValueLabel.gameObject.SetActive(false);
                }
            }

            // 버튼: 보유 → [장착/장착됨] + [레벨업 (N/M)], 미보유 → 안내만
            if (_view.lockedHintLabel != null) _view.lockedHintLabel.gameObject.SetActive(!owned);
            if (_view.equipButton != null) _view.equipButton.gameObject.SetActive(owned);
            if (_view.levelUpButton != null) _view.levelUpButton.gameObject.SetActive(owned);
            if (!owned) return;

            if (_view.equipButton != null) _view.equipButton.interactable = !equipped;
            if (_view.equipButtonLabel != null) _view.equipButtonLabel.text = equipped ? "장착됨" : "장착";

            int dups = mgr.GetDuplicates(so.id);
            int req = mgr.GetNextUpgradeReq(so.id);
            if (_view.levelUpButton != null) _view.levelUpButton.interactable = mgr.CanLevelUp(so.id);
            if (_view.levelUpButtonLabel != null) _view.levelUpButtonLabel.text = $"레벨업 ({dups}/{req})";
        }

        // ────────────────────────────────────────────
        //  액션
        // ────────────────────────────────────────────
        private static void OnEquipClicked()
        {
            var mgr = DivineSkillManager.Instance;
            if (mgr == null || _selectedId < 0) return;
            // Equip이 저장 + OnStateChanged를 발화하므로 UI는 이벤트 경로로 갱신된다
            mgr.Equip(_selectedId);
        }

        private static void OnLevelUpClicked()
        {
            var mgr = DivineSkillManager.Instance;
            if (mgr == null || _selectedId < 0) return;
            if (mgr.TryLevelUp(_selectedId) && _view != null && _view.levelUpButton != null)
                UITween.Punch((RectTransform)_view.levelUpButton.transform);
        }
    }
}
