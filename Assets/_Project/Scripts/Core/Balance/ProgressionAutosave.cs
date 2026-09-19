using System;
using System.Threading.Tasks;
using UnityEngine;

namespace KingdomIdle.Balance
{
    public static partial class LocalProgression
    {
        private static Task _counterSave;
        private static bool _countersDirty;
        private static long _counterSequence, _savingSequence, _savingRevision;
        private static float _nextCounterSave;

        /// <summary>Non-economic progress only. No disk IO or global UI rebuild on the cast frame.</summary>
        public static void RecordSkillCast(int skillId)
        {
            if (!MageTower.MageSkillRules.IsAvailable(skillId)) return;
            Ensure();
            if (_busy) throw new InvalidOperationException("Cannot record a cast inside a transaction.");
            var draft = _state.DeepClone();
            // Capture day/week eligibility at the event, not at the later disk flush.
            QuestEconomy.Before(draft);
            QuestEconomy.Count(draft, eQuestObjectiveType.SkillCast, skillId, 1);
            QuestEconomy.After(draft);
            _state = draft; _counterSequence++; _countersDirty = true;
            ProgressionAutosave.EnsureRunning();
        }

        internal static void TickSkillCounters()
        {
            bool published = CompleteCounterSave(false);
            if (published) NotifyObservers();
            if (_busy || !_countersDirty || _counterSave != null || Time.unscaledTime < _nextCounterSave) return;
            var snapshot = _state.DeepClone();
            snapshot.Revision = checked(_state.Revision + 1);
            _savingRevision = snapshot.Revision; _savingSequence = _counterSequence;
            string path = _path;
            _nextCounterSave = Time.unscaledTime + .75f;
            // This immutable snapshot is the only data touched by the worker. Unity/quest APIs stay on main.
            _counterSave = Task.Run(() => WriteSnapshot(path, snapshot));
        }

        private static bool CompleteCounterSave(bool wait)
        {
            if (_counterSave == null || (!wait && !_counterSave.IsCompleted)) return false;
            try
            {
                _counterSave.GetAwaiter().GetResult();
                _state.Revision = _savingRevision;
                if (_counterSequence == _savingSequence) _countersDirty = false;
                LastError = null;
                return true;
            }
            catch (Exception ex)
            {
                LastError = "skill-autosave: " + ex.Message;
                Debug.LogWarning("[Progression] " + LastError);
                return false;
            }
            finally { _counterSave = null; }
        }

        public static bool FlushSkillCounters()
        {
            CompleteCounterSave(true);
            if (!_countersDirty || _state == null) return true;
            if (_busy) return false;
            try
            {
                // No Ensure here: an account switch must flush the OLD account/path first.
                var snapshot = _state.DeepClone();
                snapshot.Revision = checked(_state.Revision + 1);
                Validate(snapshot); WriteSnapshot(_path, snapshot);
                _state = snapshot; _countersDirty = false; LastError = null;
                return true;
            }
            catch (Exception ex) { LastError = "skill-counter-flush: " + ex.Message; Debug.LogWarning("[Progression] " + LastError); return false; }
        }
    }

    internal sealed class ProgressionAutosave : MonoBehaviour
    {
        private static ProgressionAutosave _instance;
        internal static void EnsureRunning()
        {
            if (_instance != null) return;
            _instance = new GameObject("ProgressionAutosave").AddComponent<ProgressionAutosave>();
            DontDestroyOnLoad(_instance.gameObject);
        }
        private void Update() => LocalProgression.TickSkillCounters();
        private void OnApplicationPause(bool paused) { if (paused) LocalProgression.FlushSkillCounters(); }
        private void OnApplicationQuit() => LocalProgression.FlushSkillCounters();
    }
}
