using KingdomIdle.Ads;
using TMPro;
using UnityEngine;

namespace KingdomIdle.UGUI
{
    public sealed class AdMobTestController : MonoBehaviour
    {
        [SerializeField] internal UnityEngine.UI.Button showButton, closeButton;
        [SerializeField] internal TMP_Text buttonLabel, popupLabel;
        [SerializeField] internal GameObject popup;

        private void Awake()
        {
            showButton.onClick.AddListener(ShowAd);
            closeButton.onClick.AddListener(() => popup.SetActive(false));
            popup.SetActive(false);
        }

        public void ShowAd()
        {
            if (!showButton.interactable) return;
            showButton.interactable = false;
            buttonLabel.text = "광고 불러오는 중…";
            // Fixed Google sample ID: this scene never requests production ads.
            RewardedAdService.Show("ca-app-pub-3940256099942544/5224354917", (result, error) =>
            {
                if (this == null) return;
                showButton.interactable = true;
                buttonLabel.text = "광고 보기";
                if (result == RewardedAdResult.Skipped) return;
                popupLabel.text = result == RewardedAdResult.Completed
                    ? "광고 시청이 완료되었습니다."
                    : "광고를 표시하지 못했습니다.\n잠시 후 다시 시도해 주세요.";
                popup.SetActive(true);
            });
        }
    }
}
