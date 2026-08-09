using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Core
{
    [CreateAssetMenu(menuName = "KingdomIdle/Tutorial/Guide List", fileName = "TutorialGuideList")]
    public class TutorialGuideListSO : ScriptableObject
    {
        public List<TutorialStepDataSO> steps = new List<TutorialStepDataSO>();
    }
}
