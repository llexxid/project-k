using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 하단 탭 버튼 (아이콘 스프라이트 + 라벨 + 상단 인디케이터).
    /// tab-btn-selected USS 클래스 대응 시각 상태를 코드로 적용한다.
    /// 아이콘은 픽셀 아트 키트 스프라이트 (폰트 글리프 ⚔♞✦ 는 Galmuri11에 없어 이미지로 대체).
    /// </summary>
    public sealed class MainTabButtonView : MonoBehaviour
    {
        [SerializeField] internal Button button;
        [SerializeField] internal Image background;
        [SerializeField] internal Image icon;
        [SerializeField] internal TMP_Text label;
        [SerializeField] internal Image indicator;

        /// <summary>선택 시 교체할 눌림 상태 스프라이트(픽셀 키트). 생성기가 채운다.</summary>
        [SerializeField] internal Sprite bgNormalSprite;
        [SerializeField] internal Sprite bgSelectedSprite;

        public Button Button => button;

        // 러스틱 하단 탭: 평소 어두운 다크 우드, 선택 시 밝은 청동/앰버
        // 픽셀 버튼 스프라이트가 중간 회색이라 틴트가 곱해진다 — 너무 어두우면 탭이 검게 죽는다
        private static readonly Color TabBgNormal = new Color(0.52f, 0.40f, 0.28f, 1f);
        private static readonly Color TabBgSelected = new Color(0.86f, 0.66f, 0.32f, 1f);
        // 아이콘 틴트: 평소는 바랜 청동, 선택 시 밝은 양피지색.
        // (PictoIcon/PixelArtGUI2 아이콘은 흰·회색 마스터라 틴트가 그대로 먹는다 —
        //  "풀컬러라 틴트 못 한다"는 이전 주석은 사실이 아니었다.)
        private static readonly Color IconNormal = new Color(0.80f, 0.70f, 0.55f, 1f);
        private static readonly Color IconSelected = new Color(1f, 0.96f, 0.86f, 1f);

        public void SetSelected(bool selected)
        {
            if (background != null)
            {
                background.color = selected ? TabBgSelected : TabBgNormal;
                var sp = selected ? bgSelectedSprite : bgNormalSprite;
                if (sp != null) background.sprite = sp;   // 베벨이 눌린 상태로 바뀐다
            }

            if (icon != null)
                icon.color = selected ? IconSelected : IconNormal;
            if (label != null)
                label.color = selected ? new Color(1f, 0.96f, 0.86f, 1f) : new Color(1f, 1f, 1f, 0.72f);
            if (indicator != null)
            {
                var c = UguiTheme.TabIndicator;
                indicator.color = selected ? c : new Color(c.r, c.g, c.b, 0f);
            }
            // 선택 탭이 판에서 살짝 '들리는' 느낌 (도트 UI 관례)
            transform.localScale = selected ? new Vector3(1.04f, 1.06f, 1f) : Vector3.one;
        }
    }
}
