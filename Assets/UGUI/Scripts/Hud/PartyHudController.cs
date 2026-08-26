using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using KingdomIdle.UI;
using KingdomIdle.KingdomArmy;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 파티 HUD 컨트롤러 (UITKPartyHudController 이식).
    /// 매 프레임 플레이어 상태를 폴링해 HP/스킬 쿨다운을 반영하고,
    /// 하단바(190px) + 열린 시트 높이에 맞춰 위치를 조정한다.
    /// </summary>
    [DefaultExecutionOrder(-940)]
    public sealed class PartyHudController : MonoBehaviour
    {
        public static PartyHudController Instance { get; private set; }

        /// <summary>
        /// 전체 화면 모달(예: 던전 난이도 팝업)이 떠 있는 동안 파티 HUD를 잠시 숨기는 카운터.
        /// 파티 HUD는 LayerPopups에 있어 LayerPanels의 모달 딤 위에 그려지기 때문에,
        /// 모달 쪽(PartyHudSuppressor)이 활성화 동안 1 올려 겹침을 막는다.
        /// </summary>
        internal static int ModalSuppressCount;

        [Header("Portrait Sprites")]
        [SerializeField] private Sprite portraitSprite0;
        [SerializeField] private Sprite portraitSprite1;
        [SerializeField] private Sprite portraitSprite2;

        [Header("Layout")]
        [SerializeField] private float baseGapPx = 12f;
        [SerializeField] private float sheetGapPx = 10f;
        [SerializeField] private float fallbackBottomBarPx = 190f;

        private PartyHudView _view;
        private List<Player> _players;
        private bool _playersResolved;
        private readonly bool[] _autoPortrait = new bool[3];    // 잡 데이터에서 자동 배정된 초상화 (전직 시 재해석 대상)
        private readonly bool[] _reResolvePortrait = new bool[3]; // 재해석 예약 (기존 스프라이트는 교체 직전까지 유지)
        private KingdomArmyManager _subscribedMgr;
        private float _posVelY;                                 // 시트 연동 부드러운 상승용 SmoothDamp 속도
        private readonly float[,] _cdTotals = new float[3, 3];  // 관측된 총 쿨다운 (남은쿨 최대값 캡처 → 드레인 비율 표시)

        /// <summary>이 값 이상의 남은 쿨 = 스킬 쪽의 "효과 지속 중" 센티널(float.MaxValue)이지 실제 쿨이 아니다.</summary>
        private const float BusyCooldownSentinel = 9999f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (_subscribedMgr != null) _subscribedMgr.OnStateChanged -= OnArmyStateChanged;
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            EnsureHudBuilt();
            EnsureArmySubscription();
            UpdateHudVisibilityAndPosition();
            SyncFromPlayers();
        }

        /// <summary>전직/파티 변경 시 자동 배정 초상화를 다시 해석하도록 구독 (매니저 늦은 초기화 대응 재시도).</summary>
        private void EnsureArmySubscription()
        {
            if (_subscribedMgr != null) return;
            var mgr = KingdomArmyManager.Instance;
            if (mgr == null) return;
            _subscribedMgr = mgr;
            _subscribedMgr.OnStateChanged += OnArmyStateChanged;
        }

        private void OnArmyStateChanged()
        {
            // 자동 배정 슬롯만 재해석 대상으로 표시한다. 스프라이트를 지우면 재해석되는
            // 다음 프레임까지 초상화가 한 프레임 사라져 깜빡인다 (파편 획득 등으로도 발화하는 이벤트).
            for (int i = 0; i < 3; i++)
                if (_autoPortrait[i]) _reResolvePortrait[i] = true;

            // 전직하면 관측해 둔 총 쿨다운이 전부 무효 (새 스킬 세트)
            System.Array.Clear(_cdTotals, 0, _cdTotals.Length);
            _playersResolved = false;   // 다음 프레임 SyncFromPlayers → ResolvePlayers 재실행
        }

        // ═══ public API (기존 계약 유지) ═══

        public void SetPortraitSprite(int memberIndex, Sprite sprite)
        {
            if (memberIndex < 0 || memberIndex >= 3) return;

            switch (memberIndex)
            {
                case 0: portraitSprite0 = sprite; break;
                case 1: portraitSprite1 = sprite; break;
                case 2: portraitSprite2 = sprite; break;
            }

            ApplyPortraitSprite(memberIndex);
        }

        public void SetMemberHealth(int memberIndex, float current, float max)
        {
            if (memberIndex < 0 || memberIndex >= 3) return;
            float t = max <= 0f ? 0f : Mathf.Clamp01(current / max);
            SetMemberHealth01(memberIndex, t);
        }

        public void SetMemberHealth01(int memberIndex, float normalized01)
        {
            if (_view == null || memberIndex < 0 || memberIndex >= _view.members.Length) return;
            var fill = _view.members[memberIndex]?.hpFill;
            if (fill == null) return;
            fill.fillAmount = Mathf.Clamp01(normalized01);
        }

        public void SetMemberSkillCount(int memberIndex, int count)
        {
            if (_view == null || memberIndex < 0 || memberIndex >= _view.members.Length) return;
            var member = _view.members[memberIndex];
            if (member == null) return;

            count = Mathf.Clamp(count, 0, 3);
            for (int s = 0; s < member.skills.Length; s++)
            {
                var slot = member.skills[s];
                if (slot?.root == null) continue;
                slot.root.SetActive(s < count);
            }
        }

        // ═══ 내부 ═══

        private Sprite GetPortraitSprite(int index)
        {
            return index switch
            {
                0 => portraitSprite0,
                1 => portraitSprite1,
                2 => portraitSprite2,
                _ => null
            };
        }

        private void ApplyPortraitSprite(int index)
        {
            if (_view == null || index < 0 || index >= _view.members.Length) return;
            var img = _view.members[index]?.portraitImage;
            if (img == null) return;

            var sprite = GetPortraitSprite(index);
            img.sprite = sprite;
            img.enabled = sprite != null;
        }

        private void EnsureHudBuilt()
        {
            if (_view != null) return;

            var mgr = UIManager.Instance;
            if (mgr == null || mgr.LayerPopups == null || mgr.Catalog == null || mgr.Catalog.hudParty == null)
                return;

            var go = Instantiate(mgr.Catalog.hudParty, mgr.LayerPopups, false);
            _view = go.GetComponent<PartyHudView>();
            if (_view == null)
            {
                Debug.LogError("[PartyHud] PartyHudView 컴포넌트가 없습니다.");
                Destroy(go);
                return;
            }

            go.transform.SetAsLastSibling();

            for (int i = 0; i < 3; i++)
            {
                var member = _view.members[i];
                if (member == null) continue;
                if (member.portrait != null)
                {
                    int idx = i;   // 탭한 멤버의 왕국군 메뉴로 라우팅 (클로저 캡처)
                    member.portrait.onClick.AddListener(() => OpenKingdomArmyPanel(idx));
                }

                // 쿨다운 마스크를 세로 드레인 채움으로 (프리팹 재생성 없이 런타임 구성)
                for (int s = 0; s < member.skills.Length; s++)
                {
                    var mask = member.skills[s]?.cooldownMask;
                    if (mask == null) continue;
                    mask.type = Image.Type.Filled;
                    mask.fillMethod = Image.FillMethod.Vertical;
                    mask.fillOrigin = (int)Image.OriginVertical.Top;
                    mask.fillAmount = 1f;
                }

                ApplyPortraitSprite(i);
                SetMemberHealth01(i, 1f);
            }
        }

        private void ResolvePlayers()
        {
            if (_playersResolved) return;

            var mgr = KingdomArmyManager.Instance;
            if (mgr == null) return;

            _players = mgr.GetPlayers();
            if (_players != null && _players.Count > 0)
            {
                _playersResolved = true;

                for (int i = 0; i < 3 && i < _players.Count; i++)
                {
                    // 인스펙터 지정 초상화는 존중하고, 자동 배정분만 재해석 예약 시 덮어쓴다
                    if (GetPortraitSprite(i) != null && !_reResolvePortrait[i]) continue;
                    _reResolvePortrait[i] = false;

                    var player = _players[i];
                    if (player == null) continue;

                    Sprite idleSprite = null;
                    var jobDB = mgr.JobDB;
                    if (jobDB != null && player.playerStatus != null)
                    {
                        var jobData = jobDB.GetJob(player.playerStatus.JobName);
                        if (jobData != null && jobData.Portrait != null)
                            idleSprite = jobData.Portrait;   // 전용 초상화 우선, 없으면 jobSprite 폴백
                    }

                    if (idleSprite == null)
                    {
                        var sr = player.GetComponent<SpriteRenderer>();
                        if (sr != null && sr.sprite != null)
                            idleSprite = sr.sprite;
                    }

                    if (idleSprite != null)
                    {
                        SetPortraitSprite(i, idleSprite);
                        _autoPortrait[i] = true;
                    }
                }
            }
        }

        private void SyncFromPlayers()
        {
            if (_view == null) return;

            ResolvePlayers();
            if (_players == null) return;

            for (int i = 0; i < 3 && i < _players.Count; i++)
            {
                var player = _players[i];
                if (player == null || player.playerStatus == null) continue;

                // PlayerStatus.HP는 전투 중 갱신되지 않는 스냅샷 — 실제 체력은 Player.HPRatio가 진실
                bool dead = player.IsDead || !player.gameObject.activeInHierarchy;
                SetMemberHealth01(i, dead ? 0f : player.HPRatio);

                var img = _view.members[i]?.portraitImage;
                if (img != null)
                {
                    var tint = dead ? new Color(0.45f, 0.45f, 0.5f, 1f) : Color.white;
                    if (img.color != tint) img.color = tint;
                }

                var sys = player.skillSystem;
                if (sys == null) continue;

                SyncSkillSlots(i, sys);
            }
        }

        private void SyncSkillSlots(int memberIdx, SkillSystem sys)
        {
            var member = _view.members[memberIdx];
            if (member == null) return;

            for (int s = 0; s < member.skills.Length; s++)
            {
                var slot = member.skills[s];
                if (slot?.root == null) continue;

                var displaySlot = sys.GetSlot(s);
                if (!displaySlot.Active)
                {
                    slot.root.SetActive(false);
                    continue;
                }

                slot.root.SetActive(true);

                // 미니멀 아이콘 우선, 없으면 기존 한글 라벨로 폴백
                var sp = ResolveSkillIcon(memberIdx, s, displaySlot.Name);
                if (slot.icon != null)
                {
                    if (sp != null && slot.icon.sprite != sp) slot.icon.sprite = sp;
                    if (slot.icon.gameObject.activeSelf != (sp != null))
                        slot.icon.gameObject.SetActive(sp != null);
                }
                if (slot.nameLabel != null)
                {
                    bool useLabel = sp == null;
                    if (slot.nameLabel.gameObject.activeSelf != useLabel)
                        slot.nameLabel.gameObject.SetActive(useLabel);
                    if (useLabel) slot.nameLabel.text = displaySlot.Name ?? "";
                }

                if (displaySlot.IsPassive)
                {
                    if (slot.cooldownMask != null) slot.cooldownMask.gameObject.SetActive(false);
                    if (slot.cooldownLabel != null)
                    {
                        slot.cooldownLabel.text = "상시";
                        slot.cooldownLabel.gameObject.SetActive(true);
                        slot.cooldownLabel.color = new Color(0.4f, 1f, 0.4f, 1f);
                    }
                }
                else
                {
                    float cd = sys.GetSlotCooldown(s);
                    if (cd > 0f)
                    {
                        // IronWill/ChargeShot 은 효과 지속 동안 _nextAvailableTime 을 float.MaxValue 로 두는
                        // "사용 중" 센티널을 쓴다 — 이 값을 총 쿨로 캡처하면 이후 드레인이 0으로 눌린다.
                        bool busy = cd >= BusyCooldownSentinel;
                        if (!busy && cd > _cdTotals[memberIdx, s]) _cdTotals[memberIdx, s] = cd;
                        float total = _cdTotals[memberIdx, s];

                        if (slot.cooldownMask != null)
                        {
                            slot.cooldownMask.gameObject.SetActive(true);
                            // 사용 중엔 가득 찬 마스크 (남은 시간을 알 수 없다)
                            slot.cooldownMask.fillAmount = busy || total <= 0f ? 1f : Mathf.Clamp01(cd / total);
                        }
                        if (slot.cooldownLabel != null)
                        {
                            // 센티널을 CeilToInt 하면 int 오버플로로 "-2147483648" 이 찍힌다
                            slot.cooldownLabel.text = busy ? "" : Mathf.CeilToInt(cd).ToString();
                            slot.cooldownLabel.gameObject.SetActive(!busy);
                            slot.cooldownLabel.color = Color.white;
                        }
                    }
                    else
                    {
                        _cdTotals[memberIdx, s] = 0f;
                        if (slot.cooldownMask != null) slot.cooldownMask.gameObject.SetActive(false);
                        if (slot.cooldownLabel != null)
                        {
                            slot.cooldownLabel.text = "";
                            slot.cooldownLabel.gameObject.SetActive(false);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 스킬 슬롯 아이콘 결정. 슬롯 0=기본공격(직업 무기), 1=오라(방패), 2=특수(스킬별).
        /// 스킬 한글명으로 특수를 구분한다 — SkillSystem 이 종류 enum 을 노출하지 않기 때문.
        /// </summary>
        private Sprite ResolveSkillIcon(int memberIdx, int slotIdx, string skillName)
        {
            var cat = UIManager.Instance != null ? UIManager.Instance.Catalog : null;
            if (cat == null) return null;

            if (slotIdx == 1) return cat.iconSkillShield;   // 오라 = 심플한 방패 하나

            if (slotIdx == 2)
            {
                if (!string.IsNullOrEmpty(skillName))
                {
                    if (skillName.Contains("강철")) return cat.iconSkillPotion;   // 강철의지 = 자가 회복
                    if (skillName.Contains("사격")) return cat.iconSkillArrows;   // 집중사격
                    if (skillName.Contains("파동")) return cat.iconSkillStar;     // 에너지 파동
                }
                return cat.iconSkillStar;
            }

            // 기본공격 — 직업의 무기 계열
            string job = null;
            if (_players != null && memberIdx < _players.Count && _players[memberIdx] != null)
                job = _players[memberIdx].playerStatus != null ? _players[memberIdx].playerStatus.JobName : null;
            if (!string.IsNullOrEmpty(job))
            {
                if (job.Contains("Archer") || job.Contains("Arbalest")) return cat.iconSkillBow;
                if (job.Contains("Mage")) return cat.iconSkillWand;
            }
            return cat.iconSkillSword;
        }

        private void UpdateHudVisibilityAndPosition()
        {
            if (_view == null || _view.rect == null) return;

            var mgr = UIManager.Instance;
            bool onMain = mgr != null && mgr.ActiveScreenId == UIScreenId.Main;
            bool shouldShow = onMain && !mgr.HasBlockingPanel && ModalSuppressCount <= 0;

            if (_view.gameObject.activeSelf != shouldShow)
                _view.gameObject.SetActive(shouldShow);
            if (!shouldShow) return;

            float sheetH = mgr.GetTopSheetHeight();
            float bottom = fallbackBottomBarPx + baseGapPx + (sheetH > 1f ? sheetH + sheetGapPx : 0f);

            // 프리팹은 bottom-center 앵커/피벗 — 시트 슬라이드에 맞춰 y를 부드럽게 추종
            var pos = _view.rect.anchoredPosition;
            if (!Mathf.Approximately(pos.y, bottom))
            {
                float newY = Mathf.SmoothDamp(pos.y, bottom, ref _posVelY, 0.14f, Mathf.Infinity, Time.unscaledDeltaTime);
                if (Mathf.Abs(newY - bottom) < 0.5f) { newY = bottom; _posVelY = 0f; }
                _view.rect.anchoredPosition = new Vector2(pos.x, newY);
            }
        }

        private void OpenKingdomArmyPanel(int memberIndex)
        {
            if (UIManager.Instance == null) return;
            KingdomArmyPanelController.SetPendingMemberIndex(memberIndex);
            UIManager.Instance.ClearPanels();
            UIManager.Instance.PushPanel(UIPanelId.KingdomArmy, "kingdomArmyPanel", clearBefore: false, isTabPanel: true);
        }
    }
}
