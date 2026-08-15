using System.Collections.Generic;
using UnityEngine;

namespace KingdomIdle.Divine
{
    /// <summary>
    /// 신 스킬 VFX 프리팹에 붙는 수명/배치 컨트롤러 + 프리팹별 풀.
    ///
    /// 외부 이펙트 팩(PixelArtRPGVFX / StateEffect 등)의 클립은 대부분 무한 루프이고
    /// 자기 파괴 기능이 없다. 이 컴포넌트가 그 두 가지를 대신한다.
    ///  - lifetime 초 뒤 자동 반환 (0 이하이면 수동 Release 전까지 유지)
    ///  - followTarget 이 있으면 매 프레임 그 위치(+offset)로 따라간다
    ///  - fitToCamera 면 메인 카메라 화면을 덮도록 스케일을 맞춘다 (전장 전체 연출용)
    ///  - 마지막 fadeOut 초 동안 알파를 낮춰 툭 끊기지 않게 한다
    ///
    /// 풀링: 광역 궁극기는 한 프레임에 대상 수만큼 임팩트를 만든다(30마리 웨이브 = 30개).
    /// Instantiate/Destroy 를 반복하는 대신 프리팹별 스택에 반납해 재사용한다.
    /// 씬 언로드로 파괴된 잔여 인스턴스는 꺼낼 때 null 체크로 걸러진다(풀 자가 치유).
    ///
    /// 마탑 스킬과 동일하게 Addressables/eVFXType 파이프라인을 타지 않고 프리팹 직접 참조로 쓴다.
    /// (eVFXType 은 xlsx 로 자동 생성되는 열거형이라 손으로 늘리면 다음 생성 때 지워진다)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DivineVfxInstance : MonoBehaviour
    {
        [Tooltip("생성 후 자동 반환까지의 시간(초). 0 이하이면 자동 반환하지 않는다.")]
        public float lifetime = 1f;

        [Tooltip("사라지기 직전 알파 페이드 아웃 시간(초).")]
        public float fadeOut = 0.15f;

        [Tooltip("메인 카메라 화면 전체를 덮도록 스케일 보정 (전장 전체 연출).")]
        public bool fitToCamera;

        [Tooltip("화면 덮기 스케일에 곱할 여유 배수.")]
        public float fitPadding = 1.1f;

        [Tooltip("따라갈 대상. null 이면 생성 위치에 고정.")]
        public Transform followTarget;

        [Tooltip("followTarget 기준 오프셋.")]
        public Vector3 followOffset;

        // ── 풀 ──
        private static readonly Dictionary<GameObject, Stack<DivineVfxInstance>> _pools = new();

        private SpriteRenderer[] _renderers;
        private Animator[] _animators;
        private GameObject _sourcePrefab;   // 반납할 풀의 키. null 이면 풀 미사용(직접 배치 인스턴스)
        private Vector3 _baseScale = Vector3.one;
        private float _elapsed;
        private bool _released;
        private int _spawnGen;

        /// <summary>
        /// 이 인스턴스의 현재 스폰 세대. 외부에서 참조를 오래 들고 있다가 Release 할 때
        /// (예: MonsterCCState 의 상태이상 연출) 이 값을 함께 캡처해 두면,
        /// 그 사이 풀에 반납→재사용된 인스턴스를 잘못 죽이는 사고를 막을 수 있다.
        /// </summary>
        public int SpawnGen => _spawnGen;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<SpriteRenderer>(true);
            _animators = GetComponentsInChildren<Animator>(true);
            _baseScale = transform.localScale;
        }

        /// <summary>
        /// 애니메이터를 첫 프레임으로 되감고 화면 덮기 스케일을 갱신한다.
        /// OnEnable 이 아니라 Spawn 이 설정 완료 후 명시적으로 호출한다 —
        /// 신규 Instantiate 는 이미 활성 상태라 OnEnable 타이밍이 설정보다 앞서기 때문.
        /// (되감기가 없으면 재사용된 one-shot 클립이 마지막 프레임에 얼어붙은 채 나타난다)
        /// </summary>
        private void ResetPlayback()
        {
            if (_animators != null)
            {
                for (int i = 0; i < _animators.Length; i++)
                {
                    var an = _animators[i];
                    if (an == null) continue;
                    an.Rebind();
                    an.Update(0f);
                }
            }

            if (fitToCamera) FitToCamera();
        }

        /// <summary>fitToCamera 를 스폰 후에 켠 경우 스케일을 다시 맞출 때 호출.</summary>
        public void RefreshFit()
        {
            if (fitToCamera) FitToCamera();
        }

        private void LateUpdate()
        {
            if (followTarget != null)
                transform.position = followTarget.position + followOffset;

            if (lifetime <= 0f) return;

            _elapsed += Time.deltaTime;

            if (fadeOut > 0f && _elapsed > lifetime - fadeOut)
            {
                float t = Mathf.InverseLerp(lifetime - fadeOut, lifetime, _elapsed);
                SetAlpha(1f - t);
            }

            if (_elapsed >= lifetime)
                Release();
        }

        /// <summary>수동 종료. 풀에서 나온 인스턴스는 반납되고, 아니면 파괴된다.</summary>
        public void Release()
        {
            if (_released) return;
            _released = true;
            followTarget = null;

            if (_sourcePrefab == null)
            {
                Destroy(gameObject);
                return;
            }

            gameObject.SetActive(false);
            if (!_pools.TryGetValue(_sourcePrefab, out var stack))
            {
                stack = new Stack<DivineVfxInstance>();
                _pools[_sourcePrefab] = stack;
            }
            stack.Push(this);
        }

        /// <summary>세대 토큰이 일치할 때만 Release. 오래 들고 있던 참조의 안전 반납용.</summary>
        public void Release(int spawnGen)
        {
            if (spawnGen != _spawnGen) return;
            Release();
        }

        private void SetAlpha(float a)
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                if (r == null) continue;
                var c = r.color;
                c.a = a;
                r.color = c;
            }
        }

        /// <summary>스프라이트 원본 크기를 기준으로 카메라 화면을 덮도록 스케일을 맞춘다.</summary>
        private void FitToCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;

            var sr = GetComponentInChildren<SpriteRenderer>();
            if (sr == null || sr.sprite == null) return;

            float worldHeight = cam.orthographic
                ? cam.orthographicSize * 2f
                : 2f * Mathf.Abs(transform.position.z - cam.transform.position.z)
                      * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float worldWidth = worldHeight * cam.aspect;

            Vector2 spriteSize = sr.sprite.bounds.size;
            if (spriteSize.x <= 0.0001f || spriteSize.y <= 0.0001f) return;

            // 가로/세로 중 더 큰 배율을 써서 화면을 완전히 덮는다 (letterbox 방지)
            float scale = Mathf.Max(worldWidth / spriteSize.x, worldHeight / spriteSize.y) * fitPadding;
            transform.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>
        /// 프리팹을 풀에서 꺼내거나 생성하고 수명/추적을 설정한다. VFX 생성의 단일 진입점.
        /// prefab 이 null 이면 아무 것도 하지 않고 null 을 반환한다(아트 미배정 상태에서도 안전).
        /// </summary>
        public static DivineVfxInstance Spawn(GameObject prefab, Vector3 position,
                                              float lifetimeSeconds,
                                              Transform follow = null,
                                              Vector3 followOffset = default,
                                              float scaleMultiplier = 1f)
        {
            if (prefab == null) return null;

            // 풀에서 꺼내기 — 씬 언로드로 파괴된 항목은 버린다
            DivineVfxInstance inst = null;
            if (_pools.TryGetValue(prefab, out var stack))
            {
                while (stack.Count > 0)
                {
                    var candidate = stack.Pop();
                    if (candidate != null) { inst = candidate; break; }
                }
            }

            var prefabDefaults = prefab.GetComponent<DivineVfxInstance>();

            if (inst == null)
            {
                var go = Instantiate(prefab, position, Quaternion.identity);
                inst = go.GetComponent<DivineVfxInstance>();
                if (inst == null) inst = go.AddComponent<DivineVfxInstance>();
                inst._sourcePrefab = prefab;
            }
            else
            {
                inst.transform.SetPositionAndRotation(position, Quaternion.identity);
            }

            // 상태 초기화 — 이전 스폰의 오버라이드/페이드 잔여물이 새 스폰으로 새지 않게
            // 프리팹 기본값에서 다시 시작한 뒤 인자를 얹는다.
            if (prefabDefaults != null)
            {
                inst.lifetime = prefabDefaults.lifetime;
                inst.fadeOut = prefabDefaults.fadeOut;
                inst.fitToCamera = prefabDefaults.fitToCamera;
                inst.fitPadding = prefabDefaults.fitPadding;
            }
            if (lifetimeSeconds > 0f) inst.lifetime = lifetimeSeconds;
            inst.followTarget = follow;
            inst.followOffset = followOffset;

            inst._elapsed = 0f;
            inst._released = false;
            inst._spawnGen++;
            inst.SetAlpha(1f);
            inst.transform.localScale = inst._baseScale * scaleMultiplier;

            inst.gameObject.SetActive(true);
            inst.ResetPlayback(); // 설정 완료 후 명시 호출 (신규/재사용 공통 경로)

            return inst;
        }
    }
}
