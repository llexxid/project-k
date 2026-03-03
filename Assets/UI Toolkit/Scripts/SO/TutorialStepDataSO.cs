using UnityEngine;

namespace Scripts.Core
{
    [CreateAssetMenu(menuName = "KingdomIdle/Tutorial/Step", fileName = "TutorialStep_New")]
    public class TutorialStepDataSO : ScriptableObject
    {
        public int id;
        public string title;
        [TextArea(2, 5)] public string description;
        public string completionHint;
    }
}
