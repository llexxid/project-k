using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 마탑 환경 오브젝트 셸 — 좌하단에서 하단바 뒤로부터 솟아오르는 인터랙티브 마탑.
    /// 기본 스프라이트 위에 점등(창문 발광) 변형을 겹쳐 알파 크로스페이드로 불을 켠다.
    /// </summary>
    public sealed class MageTowerEnvView : MonoBehaviour
    {
        [SerializeField] internal RectTransform root;      // 흔들림/호흡의 대상
        [SerializeField] internal Image towerImage;        // 기본 마탑
        [SerializeField] internal Image litImage;          // 점등 변형 (알파 0에서 시작)
        [SerializeField] internal Image baseGlow;          // 바닥 접합부 보라 광원 (장식)
        [SerializeField] internal Button button;
        [SerializeField] internal CanvasGroup litGroup;    // 점등 크로스페이드용
    }
}
