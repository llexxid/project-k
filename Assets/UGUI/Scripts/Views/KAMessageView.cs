using UnityEngine;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 단독 안내 메시지 화면 (매니저/플레이어 정보 없음 등). 프리팹: Panel_KAMessage.prefab
    /// </summary>
    public sealed class KAMessageView : MonoBehaviour
    {
        [SerializeField] internal TMP_Text label;

        public void Set(string message)
        {
            if (label != null) label.text = message;
        }
    }
}
