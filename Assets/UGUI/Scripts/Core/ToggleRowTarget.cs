using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace KingdomIdle.UGUI
{
    public sealed class ToggleRowTarget : MonoBehaviour, IPointerClickHandler
    {
        public Toggle toggle;
        public void OnPointerClick(PointerEventData eventData)
        {
            if(eventData.button==PointerEventData.InputButton.Left && toggle!=null && toggle.IsInteractable())
                toggle.isOn=!toggle.isOn;
        }
    }
}
