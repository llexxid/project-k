using UnityEngine;
using KingdomIdle.MageTower;
using KingdomIdle.Balance;

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
        private static MageTowerManager _subscribedManager;

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
            if (_view != null && _view.awakening != null && _view.awakening.gameObject.activeInHierarchy) return;
            Unsubscribe();
            if (_view == null) return;
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

            if (_view.titleLabel != null) _view.titleLabel.text = so.DisplayName(mgr.IsBloomEnabled(_skillId)) + $"  ·  재사용 {mgr.GetEffectiveCooldown(_skillId):0.##}초";
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
            if (_view.lblBaseCd != null) _view.lblBaseCd.text = $"기본 재사용 대기시간: {so.baseCooldown:0.##}초";
            if (_view.lblEffDmg != null) _view.lblEffDmg.text = MageSkillPresentation.EffectivePower(so, effDmg, mgr.IsBloomEnabled(_skillId)) +
                $"\n왕국군 공격력 {BalanceMath.MageAttackCoefficient((long)so.BaseDamage, eLv, aLv):P1} 반영";
            if (_view.lblEffCd != null) _view.lblEffCd.text = $"적용 재사용 대기시간: {effCd:0.##}초";

            RefreshEnhancement(mgr, so);

            if (_view.awakeningEffects != null) _view.awakeningEffects.text = MageSkillPresentation.AwakeningEffects(so, aLv, mgr.IsBloomEnabled(_skillId));
            if (_view.nextAwakening != null) _view.nextAwakening.text = MageSkillPresentation.NextAwakening(so, eLv, aLv, mgr.IsBloomEnabled(_skillId));
            // awaken
            if (_view.lblAwkLevel != null) _view.lblAwkLevel.text = $"각성 레벨: {aLv} / {so.maxAwakeningLevel}";
            RefreshAwakening(mgr, so);

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
            if (mgr.IsBusy) SetPending();
        }

        private static void RefreshEnhancement(MageTowerManager mgr, MageTowerSkillSO so)
        {
            bool canEnhance = mgr.CanEnhance(_skillId, out int level, out int cost, out long knowledge);
            if (_view.lblEnhLevel != null) _view.lblEnhLevel.text = $"강화 레벨: {level} / {so.maxEnhanceLevel}";
            if (_view.lblEnhCost != null) _view.lblEnhCost.text = cost < 0 ? "MAX · 다음 강화 없음" :
                $"비전지식 · 보유 {NumberNotation.Format(knowledge)} · 필요 {NumberNotation.Format(cost)}";
            bool recover = EconomyBatchProgression.Available && !EconomyBatchProgression.RecoveryComplete ||
                EconomyBatchProgression.HasUnknownResult && !EconomyBatchProgression.IsStorageRetryWaiting;
            bool held = EconomyBatchProgression.HasHeldSpending;
            if (_view.btnEnhance != null) _view.btnEnhance.interactable = !mgr.IsBusy && (recover || held || canEnhance);
            if (_view.btnEnhanceLabel != null) _view.btnEnhanceLabel.text = recover ? "저장 결과 확인" : held ? "보류 입력 확인" :
                level >= so.maxEnhanceLevel ? "최대 레벨" : "강화하기";
            if (_view.btnReset != null) _view.btnReset.interactable = !mgr.IsBusy && mgr.CanReset(_skillId) && level == mgr.GetEnhanceLevel(_skillId);
        }

        private static void OnProjectionChanged()
        {
            if (!IsOpen) return;
            var mgr = MageTowerManager.Instance;
            if (mgr == null || mgr.IsBusy) return;
            var so = mgr.GetSkillById(_skillId);
            if (so == null) return;
            RefreshEnhancement(mgr, so);
            RefreshAwakening(mgr, so);
            if (EconomyBatchProgression.HasUnknownResult) _view.awakening?.Close();
        }

        private static void RefreshAwakening(MageTowerManager mgr, MageTowerSkillSO so)
        {
            bool canAwaken = mgr.CanAwaken(_skillId, out int level, out int cost, out int fragments);
            if (_view.lblAwkCost != null) _view.lblAwkCost.text = level >= so.maxAwakeningLevel ? "MAX · 다음 각성 없음" :
                $"스킬 파편 · 보유 {NumberNotation.Format(fragments)} · 필요 {NumberNotation.Format(cost)}";
            if (_view.btnAwaken != null) _view.btnAwaken.interactable = !mgr.IsBusy && canAwaken;
            if (_view.btnAwakenLabel != null) _view.btnAwakenLabel.text = level >= so.maxAwakeningLevel ? "최대 각성" : "각성하기";
        }

        private static void OnEnhanceClicked()
        {
            if (DevelopmentPanelController.TryHandlePendingGrowth()) return;
            Request(mgr => mgr.TryEnhanceEx(_skillId));
        }
        private static void OnAwakenClicked()
        {
            var mgr = MageTowerManager.Instance;
            if (mgr == null || _view == null || mgr.IsBusy) return;
            var effect = _view.awakening;
            var skill = mgr.GetSkillById(_skillId);
            if (skill == null || (effect != null && effect.gameObject.activeSelf)) return;
            if (effect != null) effect.Begin(skill, mgr.GetAwakeningLevel(_skillId));
            var result = mgr.TryAwakenEx(_skillId);
            if (result == MageTowerManager.MageActionResult.Queued) return;
            if (result == MageTowerManager.MageActionResult.Pending)
            {
                if (mgr.IsBusy) SetPending();
                return;
            }
            if (effect != null) effect.Close();
            UIManager.Instance?.ShowToast(DescribeGate(result));
        }
        private static void OnResetClicked() => Request(mgr => mgr.TryResetEx(_skillId));
        private static void OnBloomClicked() => Request(mgr => mgr.TrySetBloomEx(_skillId, !mgr.IsBloomEnabled(_skillId)));

        /// <summary>Pending이면 응답까지 버튼을 잠그고, Busy는 조용히, 그 외 안내용 가드 결과는 토스트만 띄운다(최종 판정은 서버).</summary>
        private static void Request(System.Func<MageTowerManager, MageTowerManager.MageActionResult> start)
        {
            var mgr = MageTowerManager.Instance;
            if (mgr == null) return;
            var result = start(mgr);
            if (result == MageTowerManager.MageActionResult.Pending) { if (mgr.IsBusy) SetPending(); return; }
            if (result is MageTowerManager.MageActionResult.Queued or MageTowerManager.MageActionResult.Busy) return;
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
                case MageTowerManager.MageActionResult.SaveFailed: return EconomyBatchProgression.FailureMessage;
                case MageTowerManager.MageActionResult.QueueFull: return "강화 입력이 가득 찼습니다. 잠시 후 다시 시도해 주세요.";
                default: return "요청할 수 없는 스킬입니다.";
            }
        }

        private static void Subscribe()
        {
            var mgr = MageTowerManager.Instance;
            Unsubscribe();
            if (mgr == null) return;
            _subscribedManager = mgr;
            mgr.OnActionCompleted += OnActionCompleted;
            mgr.OnStateChanged += RefreshContent;
            EconomyBatchProgression.Changed += OnProjectionChanged;
            EconomyBatchProgression.InputCompleted += OnBatchInputCompleted;
        }

        private static void Unsubscribe()
        {
            if (_subscribedManager != null)
            {
                _subscribedManager.OnActionCompleted -= OnActionCompleted;
                _subscribedManager.OnStateChanged -= RefreshContent;
            }
            _subscribedManager = null;
            EconomyBatchProgression.Changed -= OnProjectionChanged;
            EconomyBatchProgression.InputCompleted -= OnBatchInputCompleted;
        }

        private static void OnBatchInputCompleted(EconomyInputCompletion info)
        {
            if (!IsOpen || info.Kind != Protocol.Message.Dto.EconomyCommandKind.MageAwakening || info.SkillId != _skillId ||
                _view.awakening == null || !_view.awakening.gameObject.activeSelf) return;
            var mgr = MageTowerManager.Instance;
            if (info.Success && info.AwakeningLevel.HasValue && mgr != null)
                _view.awakening.Confirm(info.AwakeningLevel.Value, mgr.IsBloomEnabled(_skillId));
            else _view.awakening.Close();
        }

        /// <summary>서버 응답 적용 뒤 다시 그린다. 편성 결과는 장착 팝업이 알리므로 여기서는 토스트를 띄우지 않는다.</summary>
        private static void OnActionCompleted(MageTowerManager.MageActionInfo info)
        {
            if (_view == null || !_view.gameObject.activeSelf) return;
            if (info != null && info.Kind == MageTowerManager.MageActionKind.Awaken && info.SkillId == _skillId &&
                _view.awakening != null && _view.awakening.gameObject.activeSelf)
            {
                if (info.Ok)
                {
                    var mgr = MageTowerManager.Instance;
                    _view.awakening.Confirm(mgr.GetAwakeningLevel(_skillId), mgr.IsBloomEnabled(_skillId));
                    return;
                }
                _view.awakening.Close();
            }
            if (info?.Ok != true) RefreshContent();
            if (info != null && info.Kind != MageTowerManager.MageActionKind.Loadout) UIManager.Instance?.ShowToast(MageTowerManager.Describe(info));
        }
    }
}
