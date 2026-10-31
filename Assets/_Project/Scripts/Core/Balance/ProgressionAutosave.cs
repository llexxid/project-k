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

        /// <summary>스킬 시전 승인. 시전 횟수 퀘스트(SkillCast)는 서버가 집계하지 않아 더 이상 로컬에 기록하지 않는다.</summary>
        /// <returns>집계가 승인되면 true. 호출자는 false면 시전과 쿨다운을 시작하지 않는다.</returns>
        public static bool RecordSkillCast(int skillId)
        {
            if (!MageTower.MageSkillRules.IsAvailable(skillId)) return false;
            Ensure();
            if (_busy) throw new InvalidOperationException("Cannot record a cast inside a transaction.");
            return true;
        }

        internal static void TickSkillCounters()
        {
            bool published = CompleteCounterSave(false);
            if (published) Notify(Changed);
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
            if (_busy) return false;
            bool published = CompleteCounterSave(true);
            try
            {
                if (!_countersDirty || _state == null) return true;
                // No Ensure here: an account switch must flush the OLD account/path first.
                var snapshot = _state.DeepClone();
                snapshot.Revision = checked(_state.Revision + 1);
                Validate(snapshot); WriteSnapshot(_path, snapshot);
                _state = snapshot; _countersDirty = false; LastError = null;
                published = true;
                return true;
            }
            catch (Exception ex) { LastError = "skill-counter-flush: " + ex.Message; Debug.LogWarning("[Progression] " + LastError); return false; }
            finally { if (published) Notify(Changed); }
        }
    }

    internal sealed class ProgressionAutosave : MonoBehaviour
    {
        private static ProgressionAutosave _instance;
        internal static void EnsureRunning()
        {
            // EditMode 인수 검사는 Tick/Flush를 직접 호출한다. 실행 전 씬에 런타임 객체를 만들지 않는다.
            if (!Application.isPlaying || _instance != null) return;
            _instance = new GameObject("ProgressionAutosave").AddComponent<ProgressionAutosave>();
            DontDestroyOnLoad(_instance.gameObject);
        }
        private void Update() => LocalProgression.TickSkillCounters();
        private void OnApplicationPause(bool paused) { if (paused) LocalProgression.FlushSkillCounters(); }
        private void OnApplicationQuit() => LocalProgression.FlushSkillCounters();
    }
}
