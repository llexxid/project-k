using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Core
{
    [DefaultExecutionOrder(-950)]
    public sealed class TutorialManager : MonoBehaviour
    {
        public static TutorialManager Instance { get; private set; }

        [SerializeField] private TutorialGuideListSO guideList;

        private const string PrefKey = "tut_done";
        private readonly HashSet<int> _completedIds = new HashSet<int>();

        public event Action OnProgressChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadProgress();
        }

        private void LoadProgress()
        {
            _completedIds.Clear();
            string raw = PlayerPrefs.GetString(PrefKey, "");
            if (string.IsNullOrEmpty(raw)) return;

            foreach (var token in raw.Split(','))
            {
                if (int.TryParse(token, out int id))
                    _completedIds.Add(id);
            }
        }

        private void SaveProgress()
        {
            PlayerPrefs.SetString(PrefKey, string.Join(",", _completedIds));
            PlayerPrefs.Save();
        }

        public IReadOnlyList<TutorialStepDataSO> GetSteps()
        {
            if (guideList == null || guideList.steps == null)
                return Array.Empty<TutorialStepDataSO>();
            return guideList.steps;
        }

        public bool IsStepCompleted(int id) => _completedIds.Contains(id);

        public void CompleteStep(int id)
        {
            if (_completedIds.Contains(id)) return;
            _completedIds.Add(id);
            SaveProgress();
            OnProgressChanged?.Invoke();
        }

        // 테스트용
        public void UncompleteStep(int id)
        {
            if (!_completedIds.Remove(id)) return;
            SaveProgress();
            OnProgressChanged?.Invoke();
        }

        // 테스트용
        public void ResetAll()
        {
            _completedIds.Clear();
            PlayerPrefs.DeleteKey(PrefKey);
            OnProgressChanged?.Invoke();
        }

        public int GetIncompleteCount()
        {
            var steps = GetSteps();
            int done = 0;
            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i] != null && _completedIds.Contains(steps[i].id))
                    done++;
            }
            return steps.Count - done;
        }

        public int GetCompletedCount()
        {
            var steps = GetSteps();
            int done = 0;
            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i] != null && _completedIds.Contains(steps[i].id))
                    done++;
            }
            return done;
        }

        public bool IsAllCompleted()
        {
            var steps = GetSteps();
            if (steps.Count == 0) return true;
            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i] != null && !_completedIds.Contains(steps[i].id))
                    return false;
            }
            return true;
        }
    }
}
