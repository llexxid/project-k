using System.Collections.Generic;
using UnityEngine;
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
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            EnsureHudBuilt();
            UpdateHudVisibilityAndPosition();
            SyncFromPlayers();
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
                if (member?.portrait != null)
                    member.portrait.onClick.AddListener(OpenKingdomArmyPanel);

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
                    if (GetPortraitSprite(i) != null) continue;

                    var player = _players[i];
                    if (player == null) continue;

                    Sprite idleSprite = null;
                    var jobDB = mgr.JobDB;
                    if (jobDB != null && player.playerStatus != null)
                    {
                        var jobData = jobDB.GetJob(player.playerStatus.JobName);
                        if (jobData != null && jobData.jobSprite != null)
                            idleSprite = jobData.jobSprite;
                    }

                    if (idleSprite == null)
                    {
                        var sr = player.GetComponent<SpriteRenderer>();
                        if (sr != null && sr.sprite != null)
                            idleSprite = sr.sprite;
                    }

                    if (idleSprite != null)
                        SetPortraitSprite(i, idleSprite);
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

                var ps = player.playerStatus;
                SetMemberHealth(i, ps.HP, ps.MaxHP);

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

                if (slot.nameLabel != null)
                    slot.nameLabel.text = displaySlot.Name ?? "";

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
                        if (slot.cooldownMask != null) slot.cooldownMask.gameObject.SetActive(true);
                        if (slot.cooldownLabel != null)
                        {
                            slot.cooldownLabel.text = Mathf.CeilToInt(cd).ToString();
                            slot.cooldownLabel.gameObject.SetActive(true);
                            slot.cooldownLabel.color = Color.white;
                        }
                    }
                    else
                    {
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

        private void UpdateHudVisibilityAndPosition()
        {
            if (_view == null || _view.rect == null) return;

            var mgr = UIManager.Instance;
            bool onMain = mgr != null && mgr.ActiveScreenId == UIScreenId.Main;
            bool shouldShow = onMain && !mgr.HasBlockingPanel;

            if (_view.gameObject.activeSelf != shouldShow)
                _view.gameObject.SetActive(shouldShow);
            if (!shouldShow) return;

            float sheetH = mgr.GetTopSheetHeight();
            float bottom = fallbackBottomBarPx + baseGapPx + (sheetH > 1f ? sheetH + sheetGapPx : 0f);

            // 프리팹은 bottom-center 앵커/피벗 — y 오프셋만 조정
            var pos = _view.rect.anchoredPosition;
            if (!Mathf.Approximately(pos.y, bottom))
                _view.rect.anchoredPosition = new Vector2(pos.x, bottom);
        }

        private void OpenKingdomArmyPanel()
        {
            if (UIManager.Instance == null) return;
            UIManager.Instance.ClearPanels();
            UIManager.Instance.PushPanel(UIPanelId.KingdomArmy, "kingdomArmyPanel", clearBefore: false, isTabPanel: true);
        }
    }
}
