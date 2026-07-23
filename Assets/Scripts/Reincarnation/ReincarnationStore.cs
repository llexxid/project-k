using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using PlayFab.ClientModels;
using UnityEngine;

namespace Reincarnation
{
    public enum ReincarnationLoadResult
    {
        Success,
        NotFound,
        Corrupted,
        UnsupportedVersion,
        StorageError
    }
    public interface IReincarnationProgressStore
    {
        ReincarnationLoadResponse Load();
        bool TrySave(ReincarnationState state);
        bool Exists();
        void Delete();   // 디버그 및 테스트용

    }
    [Serializable]
    public class ReincarnationSaveData
    {
        public int schemaVersion;
        public long level;
        public long count;
    }
    public readonly struct ReincarnationLoadResponse
    {
        public ReincarnationLoadResult Result { get; }
        public ReincarnationState State { get; }

        public ReincarnationLoadResponse(ReincarnationLoadResult result, ReincarnationState state = default)
        {
            Result = result;
            State = state;
        }
        
        //오류 발생시 State 사용하지 않기때문에 기본값 사용
        public static ReincarnationLoadResponse NotFound() =>
            new ReincarnationLoadResponse(ReincarnationLoadResult.NotFound);

        public static ReincarnationLoadResponse Corrupted() =>
            new ReincarnationLoadResponse(ReincarnationLoadResult.Corrupted);

        public static ReincarnationLoadResponse UnsupportedVersion() =>
            new ReincarnationLoadResponse(ReincarnationLoadResult.UnsupportedVersion);

        public static ReincarnationLoadResponse StorageError() =>
            new ReincarnationLoadResponse(ReincarnationLoadResult.StorageError);
    }

    public class ReincarnationStore : IReincarnationProgressStore
    {
        private const int CurrentSchemaVersion = 1;
        
        public ReincarnationSaveData Data => _data;
        private ReincarnationSaveData _data;
        private const string SaveKey = "reincarnation.progress.v1";
        
        public ReincarnationLoadResponse Load()
        {
            _data = new ReincarnationSaveData();
            if (!PlayerPrefs.HasKey(SaveKey))
            {
                return ReincarnationLoadResponse.NotFound();
            }

            try
            {
                string load = PlayerPrefs.GetString(SaveKey);
                _data = JsonUtility.FromJson<ReincarnationSaveData>(load);

                if (_data == null)
                {
                    return ReincarnationLoadResponse.Corrupted();
                }

                if (_data.level < 0 || _data.count < 0)
                {
                    return ReincarnationLoadResponse.Corrupted();
                }

                if (_data.schemaVersion != CurrentSchemaVersion)
                {
                    return ReincarnationLoadResponse.UnsupportedVersion();
                }

                ReincarnationState state = new ReincarnationState(
                    level: _data.level,
                    count: _data.count);
                ReincarnationLoadResponse response = new ReincarnationLoadResponse(
                    ReincarnationLoadResult.Success,
                    state
                );

                return response;
            }
            catch (ArgumentException) // JSON 형식오류
            {
                return ReincarnationLoadResponse.Corrupted();
            }
            catch (Exception exception) // 파일 or 저장소 접근 오류
            {
                Debug.LogException(exception);
                return ReincarnationLoadResponse.StorageError();
            }
        }

        public bool TrySave(ReincarnationState state)
        {
            try
            { 
                _data = new ReincarnationSaveData
                {
                    level = state.Level,
                    count = state.Count,
                    schemaVersion = 1
                };
            
                string json =
                    JsonUtility.ToJson(_data);
                PlayerPrefs.SetString(SaveKey, json);
                PlayerPrefs.Save();
                return true; 
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }

        public bool Exists()
        {
            return PlayerPrefs.HasKey(SaveKey);
        }

        public void Delete()
        {
            PlayerPrefs.DeleteKey(SaveKey);
        }
    }
}