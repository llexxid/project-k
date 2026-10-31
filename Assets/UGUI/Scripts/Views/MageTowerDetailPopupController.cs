using UnityEngine;
using KingdomIdle.MageTower;
using KingdomIdle.Balance;
using Scripts.Core;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 마탑 스킬 상세 팝업 — 강화/각성/초기화 (프리팹 기반).
    /// 프리팹 Panel_MageTowerDetail(=MageTowerDetailPopupView)을 1회 인스턴스화해 캐시하고,
    /// View 참조로 값만 세팅한다. 코드로 UI 구조를 생성하지 않는다(런타임 코드빌드 제거 완료).
    /// 고정 구조라 반복 셀은 없다.
    /// </summary>
    public static class MageTowerDetailPopupController
    {
        private static MageTowerDetailPopupView _view;
        private static int _skillId;
        private static bool _subscribed;

        public static bool IsOpen => _view != null && _view.gameObject.activeSelf;

        public static void Show(int skillId)
        {
            _skillId = skillId;
            if (!EnsureBuilt()) return;
            Subscribe();

            NumberNotationBinding.Bind(_view, RefreshContent);
            RefreshContent();
            _view.gameObject.SetActive(true);
            _view.transform.SetAsLastSibling();
            if (_view.scroll != null) _view.scroll.verticalNormalizedPosition = 1f;
        }

        public static void Hide()
        {
            if (_view == null) return;
            Unsubscribe();
            _view.gameObject.SetActive(false);
            MageTowerPopupController.RefreshIfOpen();
            // (좌측 스킬 슬롯 HUD 제거됨 — 강화/각성 결과는 별도 HUD 갱신이 필요 없다)
        }

        private static bool EnsureBuilt()
        {
            if (_view != null) return true;

            var mgr = UIManager.Instance;
            if (mgr == null || mgr.LayerOverlays == null || mgr.Catalog == null || mgr.Catalog.popupMageTowerDetail == null)
            {
                Debug.LogWarning("[MageTowerDetailPopup] 카탈로그의 popupMageTowerDetail 프리팹이 없습니다.");
                return false;
            }

            var go = Object.Instantiate(mgr.Catalog.popupMageTowerDetail, mgr.LayerOverlays, false);
            ModalBackHandler.Bind(go, Hide);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            _view = go.GetComponent<MageTowerDetailPopupView>();
            if (_view == null)
            {
                Debug.LogError("[MageTowerDetailPopup] MageTowerDetailPopupView 컴포넌트가 없습니다.");
                Object.Destroy(go);
                return false;
            }

            if (_view.backdropButton != null) _view.backdropButton.onClick.AddListener(Hide);
            if (_view.closeButton != null) _view.closeButton.onClick.AddListener(Hide);
            if (_view.btnEnhance != null) _view.btnEnhance.onClick.AddListener(OnEnhanceClicked);
            if (_view.btnAwaken != null) _view.btnAwaken.onClick.AddListener(OnAwakenClicked);
            if (_view.btnReset != null) _view.btnReset.onClick.AddListener(OnResetClicked);
            if (_view.btnBloom != null) _view.btnBloom.onClick.AddListener(OnBloomClicked);

            _view.gameObject.SetActive(false);
            return true;
        }

        private static void RefreshContent()
        {
            var mgr = MageTowerManager.Instance;
            if (mgr == null || _view == null) return;

            var so = mgr.GetSkillById(_skillId);
            if (so == null) { Hide(); return; }

            if (_view.titleLabel != null) _view.titleLabel.text = so.DisplayName(mgr.IsBloomEnabled(_skillId));
            if (_view.stateLabel != null)
            {
                _view.stateLabel.text = mgr.IsEquipped(_skillId) ? "장착 중" : mgr.IsOwned(_skillId) ? "보유" : "미보유";
                _view.stateLabel.color = MageSkillPresentation.Accent;
            }
            if (_view.descriptionLabel != null) _view.descriptionLabel.text = MageSkillPresentation.Description(so, mgr.IsBloomEnabled(_skillId));

            if (_view.icon != null)
            {
                if (so.icon != null)
                {
                    _view.icon.sprite = so.DisplayIcon(mgr.IsBloomEnabled(_skillId));
                    _view.icon.enabled = true;
                }
                else
                {
                    _view.icon.sprite = null;
                    _view.icon.enabled = false;
                }
            }

            int eLv = mgr.GetEnhanceLevel(_skillId);
            int aLv = mgr.GetAwakeningLevel(_skillId);
            long effDmg = mgr.GetEffectiveDamage(_skillId);
            float effCd = mgr.GetEffectiveCooldown(_skillId);

            string powerLabel = so.IsHealing ? "회복" : "피해";
            if (_view.lblBaseDmg != null) _view.lblBaseDmg.text = $"기본 1회 {powerLabel}: {NumberNotation.Format(so.BaseDamage)}";
            if (_view.lblBaseCd != null) _view.lblBaseCd.text = $"기본 쿨타임: {so.baseCooldown:F1}s";
            if (_view.lblEffDmg != null) _view.lblEffDmg.text = MageSkillPresentation.EffectivePower(so, effDmg, mgr.IsBloomEnabled(_skillId)) +
                $"\n왕국군 공격력 {BalanceMath.MageAttackCoefficient((long)so.BaseDamage, eLv, aLv):P1} 반영";
            if (_view.lblEffCd != null) _view.lblEffCd.text = $"최종 쿨타임: {effCd:F1}s";

            // enhance
            if (_view.lblEnhLevel != null) _view.lblEnhLevel.text = $"강화 레벨: {eLv} / {so.maxEnhanceLevel}";
            int enhCost = mgr.GetEnhanceCost(_skillId);
            EconomyBridge.TryGetAmount(eCurrency.ArcaneKnowledge, out long ak);
            if (_view.lblEnhCost != null) _view.lblEnhCost.text = enhCost < 0 ? "MAX · 다음 강화 없음" : $"지식 {NumberNotation.Format(enhCost)} 필요 · 보유 {NumberNotation.Format(ak)}";
            if (_view.btnEnhance != null) _view.btnEnhance.interactable = mgr.CanEnhance(_skillId);
            if (_view.btnEnhanceLabel != null) _view.btnEnhanceLabel.text = eLv >= so.maxEnhanceLevel ? "최대 레벨" : "강화하기";

            if (_view.awakeningEffects != null) _view.awakeningEffects.text = MageSkillPresentation.AwakeningEffects(so, aLv, mgr.IsBloomEnabled(_skillId));
            if (_view.nextAwakening != null) _view.nextAwakening.text = MageSkillPresentation.NextAwakening(so, eLv, aLv, mgr.IsBloomEnabled(_skillId));
            // awaken
            if (_view.lblAwkLevel != null) _view.lblAwkLevel.text = $"각성 레벨: {aLv} / {so.maxAwakeningLevel}";
            int awkCost = mgr.GetAwakeningCost(_skillId);
            int frags = mgr.GetFragments(_skillId);
            if (_view.lblAwkCost != null) _view.lblAwkCost.text = aLv >= 10 ? "MAX · 다음 각성 없음" : $"비용: 파편 {awkCost}개 (보유: {frags})";
            if (_view.btnAwaken != null) _view.btnAwaken.interactable = mgr.CanAwaken(_skillId);
            if (_view.btnAwakenLabel != null) _view.btnAwakenLabel.text = aLv >= so.maxAwakeningLevel ? "최대 각성" : "각성하기";

            bool bloomUnlocked = mgr.IsBloomUnlocked(_skillId);
            bool bloomEnabled = mgr.IsBloomEnabled(_skillId);
            if (_view.bloomTitle != null) _view.bloomTitle.text = $"개화: {so.bloomName}";
            if (_view.bloomDescription != null) _view.bloomDescription.text = so.bloomDescription;
            if (_view.bloomStatus != null) _view.bloomStatus.text = bloomUnlocked
                ? "다음 시전부터 적용됩니다."
                : $"각성 {MageSkillRules.BloomAwakening}레벨에 개방 · 현재 {aLv}레벨";
            if (_view.btnBloom != null) _view.btnBloom.interactable = bloomUnlocked;
            if (_view.btnBloomLabel != null) _view.btnBloomLabel.text = !bloomUnlocked ? "각성 10레벨 필요" : bloomEnabled ? "개화 사용 중 · 끄기" : "개화 켜기";

            // reset
            long refund = mgr.GetResetRefund(_skillId);
            if (_view.lblResetRefund != null) _view.lblResetRefund.text = mgr.CanReset(_skillId)
                ? $"강화 초기화 시 지식 {NumberNotation.Format(refund)} 반환 (80%)" : "강화한 스킬만 초기화할 수 있습니다.";
            if (_view.btnReset != null) _view.btnReset.interactable = mgr.CanReset(_skillId);
            if (mgr.IsBusy) SetPending();
        }

        // 네 버튼은 서버 요청(OnEnChantSkill·OnAwakeningSkill·OnResetSkillEnhancement·OnSetSkillBloom)을 보낸다. 결과는 완료 이벤트가 그린다.
        private static void OnEnhanceClicked() => Request(mgr => mgr.TryEnhanceEx(_skillId));
        private static void OnAwakenClicked() => Request(mgr => mgr.TryAwakenEx(_skillId));
        private static void OnResetClicked() => Request(mgr => mgr.TryResetEx(_skillId));
        private static void OnBloomClicked() => Request(mgr => mgr.TrySetBloomEx(_skillId, !mgr.IsBloomEnabled(_skillId)));

        /// <summary>Pending이면 응답까지 버튼을 잠그고, Busy는 조용히, 그 외 안내용 가드 결과는 토스트만 띄운다(최종 판정은 서버).</summary>
        private static void Request(System.Func<MageTowerManager, MageTowerManager.MageActionResult> start)
        {
            var mgr = MageTowerManager.Instance;
            if (mgr == null) return;
            var result = start(mgr);
            if (result == MageTowerManager.MageActionResult.Pending) { SetPending(); return; }
            if (result == MageTowerManager.MageActionResult.Busy) return;
            UIManager.Instance?.ShowToast(DescribeGate(result));
            RefreshContent();
        }

        /// <summary>서버 응답을 기다리는 동안 네 버튼을 잠근다. 완료 이벤트가 RefreshContent로 다시 그린다.</summary>
        private static void SetPending()
        {
            if (_view == null) return;
            if (_view.btnEnhance != null) _view.btnEnhance.interactable = false;
            if (_view.btnAwaken != null) _view.btnAwaken.interactable = false;
            if (_view.btnReset != null) _view.btnReset.interactable = false;
            if (_view.btnBloom != null) _view.btnBloom.interactable = false;
            if (_view.btnEnhanceLabel != null) _view.btnEnhanceLabel.text = "요청 중";
            if (_view.btnAwakenLabel != null) _view.btnAwakenLabel.text = "요청 중";
            if (_view.btnBloomLabel != null) _view.btnBloomLabel.text = "요청 중";
        }

        /// <summary>요청 전 안내용 가드의 문구. 서버 거절 문구는 NetworkManager.DescribeFailure가 맡는다.</summary>
        internal static string DescribeGate(MageTowerManager.MageActionResult result)
        {
            switch (result)
            {
                case MageTowerManager.MageActionResult.NetworkNotReady: return "서버 로그인이 필요합니다.";
                case MageTowerManager.MageActionResult.NotOwned: return "보유하지 않은 스킬입니다.";
                case MageTowerManager.MageActionResult.MaxLevel: return "최대 레벨입니다.";
                case MageTowerManager.MageActionResult.NotEnough: return "지식 또는 파편이 부족합니다.";
                case MageTowerManager.MageActionResult.NotUnlocked: return "각성 10레벨에 개방됩니다.";
                case MageTowerManager.MageActionResult.NoChange: return "변경할 내용이 없습니다.";
                default: return "요청할 수 없는 스킬입니다.";
            }
        }

        private static void Subscribe()
        {
            var mgr = MageTowerManager.Instance;
            if (mgr == null || _subscribed) return;
            mgr.OnActionCompleted += OnActionCompleted;
            _subscribed = true;
        }

        private static void Unsubscribe()
        {
            var mgr = MageTowerManager.Instance;
            if (mgr != null && _subscribed) mgr.OnActionCompleted -= OnActionCompleted;
            _subscribed = false;
        }

        /// <summary>서버 응답 적용 뒤 다시 그린다. 편성 결과는 장착 팝업이 알리므로 여기서는 토스트를 띄우지 않는다.</summary>
        private static void OnActionCompleted(MageTowerManager.MageActionInfo info)
        {
            if (_view == null || !_view.gameObject.activeSelf) return;
            RefreshContent();
            MageTowerPopupController.RefreshIfOpen();
            if (info != null && info.Kind != MageTowerManager.MageActionKind.Loadout) UIManager.Instance?.ShowToast(MageTowerManager.Describe(info));
        }
    }
}
