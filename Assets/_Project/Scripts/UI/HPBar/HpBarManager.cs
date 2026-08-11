using System;
using System.Collections.Generic;
using Scripts.Monster;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 몬스터와 Screen Space HP바를 연결하고 위치, 체력 표시, 풀 반환을 통합 관리합니다.
/// </summary>
[RequireComponent(typeof(HpBarPool))]
public sealed class HpBarManager : MonoBehaviour
{
    public static HpBarManager Instance { get; private set; }

    [Header("World Position")]
    [SerializeField] private float worldGap = 0.15f;
    [SerializeField] private Vector3 fallbackOffset = new(0f, 1.2f, 0f);

    private readonly Dictionary<Monster, Binding> _bindings = new();
    private readonly List<Monster> _pendingRemoval = new();

    private HpBarPool _pool;
    private RectTransform _layerRect;
    private Camera _worldCamera;

    private sealed class Binding
    {
        public Monster Monster;
        public SpriteRenderer Renderer;
        public GameObject HpBar;
        public RectTransform RectTransform;
        public Slider Slider;
        public Action<float> HpChangedHandler;
    }

    /// <summary>
    /// 전역 접근점을 등록하고 같은 오브젝트의 HP바 풀과 레이어 RectTransform을 캐시합니다.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        _pool = GetComponent<HpBarPool>();
        _layerRect = transform as RectTransform;
    }

    /// <summary>
    /// 매 프레임 모든 활성 HP바의 월드 위치를 Screen Space 좌표로 일괄 변환합니다.
    /// </summary>
    private void LateUpdate()
    {
        if (_bindings.Count == 0)
            return;

        RefreshWorldCamera();
        if (_worldCamera == null || _layerRect == null)
        {
            SetAllBarsVisible(false);
            return;
        }

        _pendingRemoval.Clear();

        foreach (KeyValuePair<Monster, Binding> pair in _bindings)
        {
            Binding binding = pair.Value;
            if (binding.Monster == null || binding.HpBar == null || binding.RectTransform == null)
            {
                // 순회 중 Dictionary를 수정할 수 없으므로 제거 대상만 모아 루프가 끝난 뒤 처리합니다.
                _pendingRemoval.Add(pair.Key);
                continue;
            }

            UpdateScreenPosition(binding);
        }

        for (int i = 0; i < _pendingRemoval.Count; i++)
            RemoveBinding(_pendingRemoval[i]);
    }

    /// <summary>
    /// 몬스터에 풀링된 HP바를 연결하고 체력 변경 이벤트를 구독합니다.
    /// 이미 연결된 몬스터라면 현재 체력만 다시 반영합니다.
    /// </summary>
    /// <param name="monster">표시할 대상 몬스터.</param>
    public void Bind(Monster monster)
    {
        if (monster == null || _pool == null)
            return;

        if (_bindings.TryGetValue(monster, out Binding existing))
        {
            existing.Slider.SetValueWithoutNotify(monster.GetHpRatio());
            return;
        }

        GameObject hpBar = _pool.UsePool();
        if (hpBar == null)
        {
            Debug.LogError("[HpBarManager] 풀에서 HP바를 대여하지 못했습니다.", this);
            return;
        }

        Slider slider = hpBar.GetComponent<Slider>();
        RectTransform rectTransform = hpBar.transform as RectTransform;
        if (slider == null || rectTransform == null)
        {
            Debug.LogError("[HpBarManager] HP바 루트에 Slider 또는 RectTransform이 없습니다.", hpBar);
            _pool.ReturnPool(hpBar);
            return;
        }

        Binding binding = new Binding
        {
            Monster = monster,
            Renderer = monster.GetComponentInChildren<SpriteRenderer>(),
            HpBar = hpBar,
            RectTransform = rectTransform,
            Slider = slider
        };

        // 람다를 필드에 보관해야 Unbind 시 같은 델리게이트 인스턴스를 정확히 구독 해제할 수 있습니다.
        binding.HpChangedHandler = ratio =>
            binding.Slider.SetValueWithoutNotify(Mathf.Clamp01(ratio));

        monster.OnHpChanged += binding.HpChangedHandler;
        slider.SetValueWithoutNotify(monster.GetHpRatio());
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.localScale = Vector3.one;

        _bindings.Add(monster, binding);
    }

    /// <summary>
    /// 몬스터와 HP바 연결을 해제하고 이벤트 구독을 제거한 뒤 HP바를 풀에 반환합니다.
    /// </summary>
    /// <param name="monster">연결을 해제할 몬스터.</param>
    public void Unbind(Monster monster)
    {
        if (monster == null)
            return;

        RemoveBinding(monster);
    }

    /// <summary>
    /// 현재 연결된 모든 몬스터의 이벤트를 해제하고 HP바를 풀에 반환합니다.
    /// </summary>
    public void ReleaseAll()
    {
        foreach (Binding binding in _bindings.Values)
            ReleaseBinding(binding);

        _bindings.Clear();
        _pendingRemoval.Clear();
    }

    /// <summary>
    /// 활성 전투 카메라가 교체되거나 파괴된 경우 Camera.main을 다시 캐시합니다.
    /// </summary>
    private void RefreshWorldCamera()
    {
        if (_worldCamera == null || !_worldCamera.isActiveAndEnabled)
            _worldCamera = Camera.main;
    }

    /// <summary>
    /// 몬스터 머리 위 월드 좌표를 현재 Overlay Canvas의 로컬 좌표로 변환합니다.
    /// </summary>
    /// <param name="binding">갱신할 몬스터와 HP바 연결 정보.</param>
    private void UpdateScreenPosition(Binding binding)
    {
        Vector3 worldPosition;
        if (binding.Renderer != null)
        {
            Bounds bounds = binding.Renderer.bounds;
            worldPosition = new Vector3(bounds.center.x, bounds.max.y + worldGap, bounds.center.z);
        }
        else
        {
            worldPosition = binding.Monster.transform.position + fallbackOffset;
        }

        Vector3 screenPoint = _worldCamera.WorldToScreenPoint(worldPosition);
        bool isVisible = screenPoint.z > 0f &&
                         _worldCamera.pixelRect.Contains(new Vector2(screenPoint.x, screenPoint.y));

        if (!isVisible ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _layerRect,
                screenPoint,
                null,
                out Vector2 localPoint))
        {
            SetBarVisible(binding, false);
            return;
        }

        binding.RectTransform.anchoredPosition = localPoint;
        SetBarVisible(binding, true);
    }

    /// <summary>
    /// 연결 하나를 Dictionary에서 제거하고 관련 리소스를 정리합니다.
    /// </summary>
    /// <param name="monster">Dictionary에 등록된 몬스터 키.</param>
    private void RemoveBinding(Monster monster)
    {
        if (ReferenceEquals(monster, null) || !_bindings.TryGetValue(monster, out Binding binding))
            return;

        _bindings.Remove(monster);
        ReleaseBinding(binding);
    }

    /// <summary>
    /// 체력 이벤트 구독을 해제하고 HP바 표시 상태를 초기화해 풀에 반납합니다.
    /// </summary>
    /// <param name="binding">정리할 연결 정보.</param>
    private void ReleaseBinding(Binding binding)
    {
        if (binding == null)
            return;

        if (binding.Monster != null && binding.HpChangedHandler != null)
            binding.Monster.OnHpChanged -= binding.HpChangedHandler;

        if (binding.Slider != null)
            binding.Slider.SetValueWithoutNotify(1f);

        if (binding.RectTransform != null)
        {
            binding.RectTransform.anchoredPosition = Vector2.zero;
            binding.RectTransform.localScale = Vector3.one;
        }

        if (binding.HpBar != null && _pool != null)
            _pool.ReturnPool(binding.HpBar);
    }

    /// <summary>
    /// 불필요한 SetActive 호출을 피하면서 개별 HP바 표시 상태를 변경합니다.
    /// </summary>
    /// <param name="binding">표시 상태를 변경할 연결 정보.</param>
    /// <param name="visible">표시 여부.</param>
    private static void SetBarVisible(Binding binding, bool visible)
    {
        if (binding.HpBar != null && binding.HpBar.activeSelf != visible)
            binding.HpBar.SetActive(visible);
    }

    /// <summary>
    /// 유효한 카메라가 없을 때 모든 대여 중 HP바를 숨깁니다.
    /// </summary>
    /// <param name="visible">표시 여부.</param>
    private void SetAllBarsVisible(bool visible)
    {
        foreach (Binding binding in _bindings.Values)
            SetBarVisible(binding, visible);
    }

    /// <summary>
    /// 매니저가 파괴될 때 몬스터 이벤트에 죽은 참조가 남지 않도록 구독만 정리합니다.
    /// </summary>
    private void OnDestroy()
    {
        foreach (Binding binding in _bindings.Values)
        {
            if (binding.Monster != null && binding.HpChangedHandler != null)
                binding.Monster.OnHpChanged -= binding.HpChangedHandler;
        }

        _bindings.Clear();

        if (Instance == this)
            Instance = null;
    }
}
