using System;
using System.Linq;
using KingdomIdle.Balance;
using KingdomIdle.CheckIn;
using KingdomIdle.Rewards;
using Scripts.Core;
using UnityEngine;

namespace KingdomIdle.UGUI
{
    [RequireComponent(typeof(CheckInPanelView))]
    public sealed class CheckInPanelController : MonoBehaviour
    {
        private void OnEnable()
        {
            var view = GetComponent<CheckInPanelView>();
            view.monthLabel.text = "출석 체크";
            view.countLabel.text = "";
            view.countdown.text = "서버 출석 기능을 준비 중입니다.";
            view.grid.gameObject.SetActive(false);
            foreach (var slot in view.slots) if (slot != null) slot.button.interactable = false;
        }
        public static string FormatCountdown(long seconds)
        {
            seconds = Math.Max(0, seconds);
            return $"다음 보상까지 {seconds / 3600:00} : {seconds / 60 % 60:00} : {seconds % 60:00}";
        }
    }
}
