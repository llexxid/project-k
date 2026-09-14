using UnityEngine;
using UnityEngine.EventSystems;

namespace KingdomIdle.UGUI
{
    public sealed class SettingsVolumeCommit : MonoBehaviour, IPointerUpHandler, IEndDragHandler
    {
        public void OnPointerUp(PointerEventData data) => PlayerPrefs.Save();
        public void OnEndDrag(PointerEventData data) => PlayerPrefs.Save();
    }
}
