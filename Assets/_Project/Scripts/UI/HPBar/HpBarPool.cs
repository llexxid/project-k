using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Screen Space HP바 프리팹을 미리 생성하고 재사용하는 내부 풀입니다.
/// 외부에서는 HpBarManager를 통해서만 HP바를 대여하고 반납합니다.
/// </summary>
public sealed class HpBarPool : MonoBehaviour
{
    [SerializeField] private GameObject hpBar;

    private readonly Stack<GameObject> _pool = new();
    private readonly HashSet<GameObject> _rented = new();
    private const int InitialSize = 15;

    /// <summary>
    /// 프리팹 구성을 검증하고 초기 HP바 15개를 비활성 상태로 예열합니다.
    /// </summary>
    private void Awake()
    {
        if (hpBar == null)
        {
            Debug.LogError("[HpBarPool] HP바 프리팹이 지정되지 않았습니다.", this);
            enabled = false;
            return;
        }

        if (hpBar.GetComponent<Slider>() == null)
        {
            Debug.LogError("[HpBarPool] HP바 프리팹 루트에 Slider가 없습니다.", hpBar);
            enabled = false;
            return;
        }

        AddPool(InitialSize);
    }

    /// <summary>
    /// 비활성 HP바 하나를 대여합니다. 풀이 비어 있으면 초기 크기의 1/3만큼 확장합니다.
    /// </summary>
    /// <returns>활성화된 HP바. 프리팹 설정이 잘못된 경우 null.</returns>
    public GameObject UsePool()
    {
        if (hpBar == null)
            return null;

        GameObject result = null;
        while (_pool.Count > 0 && result == null)
            result = _pool.Pop();

        if (result == null)
        {
            AddPool(InitialSize / 3);
            if (_pool.Count == 0)
                return null;

            result = _pool.Pop();
        }

        if (!_rented.Add(result))
        {
            Debug.LogError("[HpBarPool] 이미 대여 중인 HP바가 풀에서 발견되었습니다.", result);
            return null;
        }

        // HP바는 Screen Space Canvas 계층을 벗어나지 않고 HpbarLayer의 자식으로 유지합니다.
        result.transform.SetParent(transform, false);
        result.transform.localScale = Vector3.one;
        result.SetActive(true);

        return result;
    }

    /// <summary>
    /// 사용이 끝난 HP바를 초기 부모 아래로 되돌리고 비활성 상태로 보관합니다.
    /// </summary>
    /// <param name="returnObj">UsePool로 대여한 HP바.</param>
    public void ReturnPool(GameObject returnObj)
    {
        if (returnObj == null || !_rented.Remove(returnObj))
        {
            Debug.LogWarning("[HpBarPool] 잘못된 반납 또는 중복 반납입니다.", returnObj);
            return;
        }

        returnObj.SetActive(false);
        returnObj.transform.SetParent(transform, false);
        returnObj.transform.localScale = Vector3.one;

        _pool.Push(returnObj);
    }

    /// <summary>
    /// 지정된 수만큼 HP바를 생성해 비활성 풀에 추가합니다.
    /// </summary>
    /// <param name="size">새로 생성할 개수.</param>
    private void AddPool(int size)
    {
        if (hpBar == null || size <= 0)
            return;

        for (int i = 0; i < size; i++)
        {
            GameObject newObj = Instantiate(hpBar, transform, false);
            newObj.SetActive(false);
            _pool.Push(newObj);
        }
    }
}
