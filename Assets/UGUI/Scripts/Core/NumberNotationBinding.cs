using System;
using UnityEngine;

namespace KingdomIdle.UGUI
{
    /// <summary>Refreshes an existing view only when notation changes; subscriptions follow the view lifetime.</summary>
    public sealed class NumberNotationBinding : MonoBehaviour
    {
        Action _refresh;
        public static void Bind(Component view, Action refresh)
        {
            var binding = view.GetComponent<NumberNotationBinding>() ?? view.gameObject.AddComponent<NumberNotationBinding>();
            binding._refresh = refresh;
        }
        void OnEnable() => NumberNotation.Changed += Refresh;
        void OnDisable() => NumberNotation.Changed -= Refresh;
        void Refresh() => _refresh?.Invoke();
    }
}
