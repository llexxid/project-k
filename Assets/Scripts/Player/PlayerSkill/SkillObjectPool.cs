using UnityEngine;
using System.Collections.Generic;

public class SkillObjectPool : MonoBehaviour
{
    public static SkillObjectPool Instance; // 싱글톤
    private Dictionary<string, Queue<GameObject>> poolDict = new Dictionary<string, Queue<GameObject>>();

    private void Awake() => Instance = this;

    // 풀에서 스킬 오브젝트 가져오기
    public GameObject GetSkillObject(SkillData data)
    {
        string key = data.skillName;

        if (!poolDict.ContainsKey(key))
            poolDict.Add(key, new Queue<GameObject>());

        if (poolDict[key].Count > 0)
        {
            GameObject obj = poolDict[key].Dequeue();
            obj.SetActive(true);
            return obj;
        }
        else
        {
            // 풀이 비어있으면 새로 생성 (skillPrefab이 없으면 null 반환)
            if (data.skillPrefab == null)
            {
                Debug.LogWarning($"[SkillObjectPool] '{key}'의 skillPrefab이 없습니다. Inspector에서 할당해 주세요.");
                return null;
            }
            GameObject newObj = Object.Instantiate(data.skillPrefab);
            return newObj;
        }
    }

    // 사용 후 풀로 반환
    public void ReturnToPool(string skillName, GameObject obj)
    {
        obj.SetActive(false);
        poolDict[skillName].Enqueue(obj);
    }
}