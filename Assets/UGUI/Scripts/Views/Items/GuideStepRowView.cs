using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KingdomIdle.Balance;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 퀘스트 한 행의 표시 담당. 패널이 전달한 snapshot으로 보상·진행·행동을 그린다.
    /// 지급과 이동은 패널에서 실행하며, 기존 Set API/필드는 이전 호출부의 직렬화 호환성을 보존한다.
    /// </summary>
    public sealed class GuideStepRowView : MonoBehaviour
    {
        public CanvasGroup canvasGroup;   // 완료 단계 흐리게 (0.55)
        public Button checkButton;        // 체크 토글 버튼 (원형 테두리에 부착)
        public Image checkBorder;         // 원형 외곽 (버튼 타겟 그래픽)
        public Image checkIcon;           // 체크 아이콘 (iconCheck 존재 시 — done일 때만 표시)
        public TMP_Text checkLabel;       // 아이콘 폴백 "V" (iconCheck 없을 때)
        public TMP_Text titleLabel;       // 단계 제목 (완료 시 흐린 색)
        public TMP_Text descLabel;        // 단계 설명
        public TMP_Text hintLabel;        // 완료 힌트 (미완료 단계에만 표시)

        // QuestCardPrefabBuilder가 연결하는 실제 카드 구성요소다. 기존 체크 버튼은 행동 버튼으로 재사용한다.
        public Image rewardIcon, secondaryRewardIcon;
        public TMP_Text rewardAmountLabel, secondaryRewardAmountLabel;
        public Image progressFill;
        public TMP_Text progressLabel, actionLabel, periodLabel;
        public Button actionButton;
        public Image claimBorder;
        // 재활성화할 때도 같은 수령 가능 강조를 복구한다. 게임의 완료 상태를 별도 저장하지 않는다.
        private bool _claimable;
        private Image _infoSurface;
        private static readonly Color CompletedSurface = new Color32(221, 211, 186, 255);

        /// <summary>패널 스택에서 돌아온 경우 수령 가능한 셀의 테두리 빛을 복원한다.</summary>
        private void OnEnable() { RefreshEmphasis(); }

        /// <summary>숨긴 카드의 연출을 정리한다. 다음 활성화에 축소된 버튼이 남지 않도록 복구한다.</summary>
        private void OnDisable()
        {
            if (claimBorder != null) UITween.StopGraphicPulse(claimBorder);
        }

        /// <summary>데이터가 바뀌었을 때만 패널이 호출한다. 목표 달성과 보상 수령 완료를 구분한다.</summary>
        public void SetQuest(QuestRowSnapshot snapshot, UIViewCatalog catalog, bool claimBusy = false)
        {
            bool claimed = snapshot.State == QuestRowState.Claimed;
            bool locked = snapshot.State == QuestRowState.Locked;
            bool canMove = QuestNavigation.CanNavigate(snapshot.ObjectiveType);
            string category = snapshot.Category switch
            {
                eQuestCategory.Guide => "가이드", eQuestCategory.Daily => "일일",
                eQuestCategory.Weekly => "주간", _ => "업적"
            };
            // Description에는 실제 조건/대상이 들어 있으므로 숫자를 문자열에서 억지로 제거하지 않는다.
            titleLabel.text = $"<color=#62513C>{category} ·</color> {snapshot.Description}";
            titleLabel.color = claimed ? UguiTheme.InfoMuted : UguiTheme.InfoInk;
            // 완료 게이지는 항상 가득 채운다. 잠금 해제 전에는 실제 진행 수치와 잠금 버튼을 함께 표시한다.
            long count = snapshot.IsCompleted ? snapshot.RequiredCount : System.Math.Max(0, System.Math.Min(snapshot.Progress, snapshot.RequiredCount));
            progressFill.fillAmount = snapshot.RequiredCount > 0 ? Mathf.Clamp01((float)count / snapshot.RequiredCount) : 0;
            // 평면 Image의 실제 너비도 같은 비율로 연결한다. 텍스처의 둥근 모서리/투명 여백이 비율을 왜곡하지 않는다.
            progressFill.rectTransform.anchorMax = new Vector2(progressFill.fillAmount, 1);
            progressFill.color = claimed ? new Color32(71, 83, 58, 255) : new Color32(44, 115, 56, 255);
            progressLabel.text = $"{NumberNotation.Format(count)} / {NumberNotation.Format(snapshot.RequiredCount)}";
            // 전체 알파는 글자까지 배경에 섞어 대비를 잃게 한다. 완료는 정보면·라벨·게이지로 구분한다.
            canvasGroup.alpha = 1f;
            if (_infoSurface == null) _infoSurface = transform.Find("InfoSurface")?.GetComponent<Image>();
            if (_infoSurface != null) _infoSurface.color = claimed ? CompletedSurface : UguiTheme.InfoSurface;
            bool canClaim = snapshot.CanClaim && !claimBusy;
            actionButton.interactable = canClaim || (!snapshot.CanClaim && !locked && !claimed && canMove);
            actionLabel.text = claimed ? "완료" : snapshot.CanClaim ? (claimBusy ? "수령 중…" : "보상받기") : locked ? "잠김" : canMove ? "이동 ›" : "진행 중";
            if (actionButton.targetGraphic != null)
                actionButton.targetGraphic.color = snapshot.CanClaim ? UguiTheme.ActionGold : UguiTheme.RusticSurface;
            actionLabel.color = snapshot.CanClaim ? UguiTheme.InfoInk : UguiTheme.Parchment;
            // 이전 기간 보상은 원래 기간을 함께 표시해 현재 기간의 같은 퀘스트와 구분한다.
            periodLabel.gameObject.SetActive(snapshot.IsPending);
            periodLabel.text = snapshot.IsPending ? $"이전 기간 · {snapshot.Token.Period}" : "";
            SetRewards(snapshot, catalog);
            if (_claimable != canClaim) { _claimable = canClaim; RefreshEmphasis(); }
        }

        /// <summary>현재 카탈로그의 단일/복수 보상을 모두 표시한다. 동적 골드는 확정액을 추측하지 않는다.</summary>
        private void SetRewards(QuestRowSnapshot snapshot, UIViewCatalog catalog)
        {
            bool multiple = snapshot.Rewards.Count > 1;
            rewardIcon.transform.parent.gameObject.SetActive(snapshot.Rewards.Count > 0);
            secondaryRewardIcon.transform.parent.gameObject.SetActive(multiple);
            SetReward(0, rewardIcon, rewardAmountLabel);
            if (multiple) SetReward(1, secondaryRewardIcon, secondaryRewardAmountLabel);
            // 보상은 하단에 가로로 놓고 진행도와 개별 행동의 폭을 따로 보존한다.
            float rewardWidth = multiple ? 412f : 300f;
            float slotWidth = multiple ? (rewardWidth - 12f) * .5f : rewardWidth;
            LayoutReward(rewardIcon, rewardAmountLabel, 20f, slotWidth, multiple ? 48f : 64f,
                multiple && !snapshot.Rewards[0].IsDynamic);
            if (multiple) LayoutReward(secondaryRewardIcon, secondaryRewardAmountLabel, 32f + slotWidth, slotWidth, 48f,
                !snapshot.Rewards[1].IsDynamic);
            var track = (RectTransform)progressLabel.transform.parent;
            track.anchorMin = Vector2.up; track.anchorMax = Vector2.one; track.pivot = Vector2.up;
            track.anchoredPosition = new Vector2(rewardWidth + 16f, -136f);
            track.sizeDelta = new Vector2(-rewardWidth - 264f, 96f);
            var actionRect = (RectTransform)actionButton.transform;
            actionRect.anchoredPosition = new Vector2(0, snapshot.IsPending ? -108f : -48f);
            actionRect.sizeDelta = new Vector2(232f, 144f);
            var rowLayout = GetComponent<LayoutElement>();
            if (rowLayout != null) rowLayout.minHeight = rowLayout.preferredHeight = snapshot.IsPending ? 264f : 240f;

            void SetReward(int index, Image icon, TMP_Text amount)
            {
                if (index >= snapshot.Rewards.Count) return;
                var reward = snapshot.Rewards[index];
                icon.sprite = RewardPresentation.Icon(reward.Currency, catalog);
                amount.text = reward.IsDynamic ? "2분\n골드" : $"×{NumberNotation.Format(reward.Amount)}";
                amount.color = UguiTheme.InfoInk;
                amount.textWrappingMode = reward.IsDynamic ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
                amount.overflowMode = TextOverflowModes.Overflow;
                amount.maxVisibleLines = reward.IsDynamic ? 2 : 1;
                amount.alignment = TextAlignmentOptions.MidlineLeft;
                amount.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 96f);
            }
        }

        /// <summary>아이콘 개수에 따른 고정 영역만 배치한다. 새 레이아웃 그룹이나 매 프레임 갱신은 만들지 않는다.</summary>
        private static void LayoutReward(Image icon, TMP_Text amount, float left, float width, float size, bool stacked)
        {
            var root = (RectTransform)icon.transform.parent;
            root.anchorMin = root.anchorMax = root.pivot = Vector2.up;
            root.anchoredPosition = new Vector2(left, -136f);
            root.sizeDelta = new Vector2(width, 96f);
            icon.rectTransform.sizeDelta = new Vector2(size, size);
            if (stacked)
            {
                // 고정 수량은 아이콘 아래의 전체 폭을 사용해 숫자 토큰을 한 줄로 보존한다.
                icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = icon.rectTransform.pivot = new Vector2(.5f, 1);
                icon.rectTransform.anchoredPosition = Vector2.zero;
                amount.rectTransform.anchorMin = Vector2.zero; amount.rectTransform.anchorMax = Vector2.right;
                amount.rectTransform.pivot = new Vector2(.5f, 0);
                amount.rectTransform.anchoredPosition = Vector2.zero;
                amount.rectTransform.sizeDelta = new Vector2(0, 48f);
                amount.alignment = TextAlignmentOptions.Midline;
            }
            else
            {
                icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = icon.rectTransform.pivot = new Vector2(0, .5f);
                icon.rectTransform.anchoredPosition = Vector2.zero;
                amount.rectTransform.anchorMin = Vector2.zero; amount.rectTransform.anchorMax = Vector2.one;
                amount.rectTransform.offsetMin = new Vector2(size + 8f, 0);
                amount.rectTransform.offsetMax = Vector2.zero;
            }
        }

        /// <summary>셀의 위치와 글자를 움직이지 않고 테두리만 점등한다. 저사양 모드는 고정 점등한다.</summary>
        private void RefreshEmphasis()
        {
            if (claimBorder == null) return;
            if (_claimable && isActiveAndEnabled && Application.isPlaying)
                UITween.PulseGraphic(claimBorder, UguiTheme.Bronze, UguiTheme.ActionGold, 1.8f);
            else
            {
                UITween.StopGraphicPulse(claimBorder);
                claimBorder.color = _claimable ? UguiTheme.ActionGold : UguiTheme.Bronze;
            }
        }

        // .guide-step-title / .guide-step-title-done (GameUI.uss guide-* 토큰)
        private static readonly Color TitleColor = new Color(1f, 1f, 1f, 0.95f);
        private static readonly Color TitleDoneColor = new Color(1f, 1f, 1f, 0.45f);

        /// <summary>초기 값 세팅. 힌트는 미완료 단계에서만 노출(원본과 동일 — 토글 시 갱신하지 않음).</summary>
        public void Set(string title, string desc, string hint, bool done)
        {
            if (titleLabel != null) titleLabel.text = title;
            if (descLabel != null) descLabel.text = desc;
            if (hintLabel != null)
            {
                bool show = !string.IsNullOrEmpty(hint) && !done;
                hintLabel.gameObject.SetActive(show);
                if (show) hintLabel.text = hint;
            }
            SetDone(done);
        }

        /// <summary>완료 상태만 갱신 (체크 토글 시 호출). 힌트는 건드리지 않는다.</summary>
        public void SetDone(bool done)
        {
            if (canvasGroup != null) canvasGroup.alpha = done ? 0.55f : 1f;
            if (titleLabel != null) titleLabel.color = done ? TitleDoneColor : TitleColor;
            if (checkIcon != null) checkIcon.gameObject.SetActive(done);
            if (checkLabel != null) checkLabel.text = done ? "V" : "";
        }
    }
}
