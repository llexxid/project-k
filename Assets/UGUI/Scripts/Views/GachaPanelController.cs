using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KingdomIdle.Gacha;
using KingdomIdle.MageTower;
using Scripts.Core;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 공통 시트의 본문·고정 버튼과 보상 팝업을 기존 뽑기 거래에 연결한다.
    /// </summary>
    public static class GachaPanelController
    {
        // 방치형 가챠 표준: x1 / x10
        private static readonly int[] PullCounts = { 1, 10 };

        private static readonly Color TabActive = UguiTheme.RusticSurface;
        private static bool _flaringPull;

        private static int _activeTabIndex;
        private static bool? _pendingSkillTab;
        /// <summary>안내 재개 때 필요한 뽑기 탭만 복원한다. 실제 뽑기는 사용자 버튼으로만 실행한다.</summary>
        internal static void PrepareGuide(bool skill)
        {
            if (_tables == null) return;
            for (int i = 0; i < _tables.Count; i++) if (IsSkillTable(_tables[i]) == skill) { OnTabClicked(i); return; }
        }
        internal static bool GuideReady(bool skill) => _view != null && _view.gameObject.activeInHierarchy && _content != null && IsSkillTable(_contentTable) == skill;
        public static void SetPendingSkillTab(bool skills) => _pendingSkillTab = skills;
        private static GachaPanelView _view;
        private static GachaTabContentView _content;
        private static GachaTableSO _contentTable;
        private static GameObject _rewardsPopup;
        private static IReadOnlyList<GachaTableSO> _tables;
        private static readonly List<NavTabButtonView> _tabButtons = new();

        // 뽑기 버튼 참조 (in-flight 중 비활성화)
        private static readonly List<Button> _activePullButtons = new();

        // GachaManager 이벤트 구독 상태 (중복 구독 방지)
        private static bool _subscribedToManager;

        /// <summary>다른 뽑기 인스턴스가 정적 바인딩을 차지한 뒤 복귀한 경우에만 다시 연결한다.</summary>
        internal static void Restore(GachaPanelView view)
        {
            if (_view != view) Populate(view);
        }

        public static void Populate(GachaPanelView view)
        {
            var pendingSkillTab = _pendingSkillTab;
            _pendingSkillTab = null;
            if (view == null) return;

            // 같은 테이블이어도 다른 패널의 콘텐츠를 재사용하면 새 화면이 비어 있게 된다.
            if (_view != view) { _content = null; _contentTable = null; }
            _view = view;
            NumberNotationBinding.Bind(view, UpdateWallet);
            if (_view.tabBar == null || _view.content == null) return;

            view.OnClosed = () =>
            {
                if (_view == view)
                {
                    CloseRewards();
                    _view = null;
                    _content = null;
                    _contentTable = null;
                    EconomyBridge.OnAmountChanged -= OnWalletChanged;
                    if (GachaManager.Instance != null) GachaManager.Instance.OnPullStateChanged -= OnPullStateChanged;
                    _subscribedToManager = false;
                    _tabButtons.Clear();
                    _activePullButtons.Clear();
                }
            };

            var mgr = GachaManager.Instance;
            if (mgr == null)
            {
                ShowMessage("GachaManager를 씬에 배치해주세요.");
                return;
            }

            SubscribeManagerEvents(mgr);

            _tables = mgr.GetAllTables();
            if (_tables == null || _tables.Count == 0)
            {
                ShowMessage("등록된 뽑기 테이블이 없습니다.");
                return;
            }

            EconomyBridge.OnAmountChanged -= OnWalletChanged;
            EconomyBridge.OnAmountChanged += OnWalletChanged;
            _activeTabIndex = Mathf.Clamp(_activeTabIndex,0,_tables.Count-1);
            if (pendingSkillTab.HasValue)
                for (int i = 0; i < _tables.Count; i++)
                    if (IsSkillTable(_tables[i]) == pendingSkillTab.Value) { _activeTabIndex = i; break; }
            BuildTabs();
            RefreshContent();
        }

        private static void SubscribeManagerEvents(GachaManager mgr)
        {
            if (_subscribedToManager) return;
            mgr.OnPullStateChanged += OnPullStateChanged;
            _subscribedToManager = true;
        }

        // ── 탭바 ──────────────────────────────────────────────────────

        /// <summary>뽑기 패널 구성 시 테이블별 원본 버튼과 안내 ID를 연결한다. 탭 전환은 기존 콜백을 사용하고 버튼 파괴 시 등록을 반환한다.</summary>
        private static void BuildTabs()
        {
            DestroyChildren(_view.tabBar);
            _tabButtons.Clear();

            var prefab = UIManager.Instance != null && UIManager.Instance.Catalog != null
                ? UIManager.Instance.Catalog.itemNavTabButton
                : null;
            if (prefab == null) return;

            var cat = UIManager.Instance != null ? UIManager.Instance.Catalog : null;

            for (int i = 0; i < _tables.Count; i++)
            {
                int idx = i;
                var go = Object.Instantiate(prefab, _view.tabBar, false);
                var tab = go.GetComponent<NavTabButtonView>();
                if (tab == null) continue;

                var table = _tables[i];
                tab.SetLabel(table != null ? table.nameKor : "?");

                // 비용 통화로 뽑기 종류를 구분해 아이콘 지정
                //   비전지식 = 마법탑 스킬 뽑기(지팡이), 그 외 = 장비 뽑기(보물상자)
                Sprite tabIcon = null;
                if (cat != null && table != null)
                {
                    tabIcon = IsSkillTable(table)
                        ? cat.iconSkillWand
                        : cat.iconChest;
                }
                tab.SetIcon(tabIcon);

                tab.Button.onClick.AddListener(() => OnTabClicked(idx));
                FeatureGuideAnchor.Bind(tab.Button, IsSkillTable(table) ? Direction.GameDirectTarget.SkillGachaTab : Direction.GameDirectTarget.EquipmentGachaTab);
                _tabButtons.Add(tab);
            }

            UpdateTabStyles();
        }

        private static void OnTabClicked(int index)
        {
            if (GachaManager.Instance != null && GachaManager.Instance.IsPulling) return;
            if (index == _activeTabIndex) return;
            _activeTabIndex = index;
            UpdateTabStyles();
            RefreshContent();
        }

        private static void UpdateTabStyles()
        {
            for (int i = 0; i < _tabButtons.Count; i++)
                _tabButtons[i].SetSelected(i == _activeTabIndex, TabActive);
        }

        // ── 콘텐츠 본문 ───────────────────────────────────────────────

        private static void RefreshContent()
        {
#if LOBBY_DEVICE_QA && DEVELOPMENT_BUILD
            var contentTimer = System.Diagnostics.Stopwatch.StartNew();
#endif
            if (_view == null || _view.content == null) return;
            if (_tables == null || _activeTabIndex >= _tables.Count) return;
            var table = _tables[_activeTabIndex];
            if (table == null) return;
            if (_content != null && _contentTable == table) { UpdateWallet(); return; }
            _activePullButtons.Clear();

            var c = SpawnContent();
#if LOBBY_DEVICE_QA && DEVELOPMENT_BUILD
            Debug.Log($"[GachaTiming] shell {contentTimer.Elapsed.TotalMilliseconds:F2} ms");
#endif
            if (c == null) return;

            _contentTable = table;

            if (c.heroTitle != null) c.heroTitle.text = IsSkillTable(table) ? "마탑 뽑기" : "장비 뽑기";
            if (c.heroIcon != null) c.heroIcon.sprite = IsSkillTable(table) ? c.mageArt : c.equipmentArt;
            if (c.ratesButton != null)
            {
                c.ratesButton.onClick.RemoveListener(OpenRates);
                c.ratesButton.onClick.AddListener(OpenRates);
                c.rewardsButton.onClick.RemoveListener(OpenRewards);
                c.rewardsButton.onClick.AddListener(OpenRewards);
            }

            if (c.messageLabel != null) c.messageLabel.gameObject.SetActive(false);

            // 보유/비용 바 (.gacha-cost: 26px gold)
            if (c.costLabel != null) c.costLabel.gameObject.SetActive(true);

            // 뽑기 버튼 행 — 크고 명확한 프리팹 버튼 (Item_GachaPullButton)
            BuildPullRow(c, table);
            UpdateWallet();
#if LOBBY_DEVICE_QA && DEVELOPMENT_BUILD
            Debug.Log($"[GachaTiming] controls {contentTimer.Elapsed.TotalMilliseconds:F2} ms");
#endif

        }

        private static void BuildPullRow(GachaTabContentView c, GachaTableSO table)
        {
            if (c.pullRow == null) return;
            c.pullRow.gameObject.SetActive(true);

            var manager = GachaManager.Instance;
            var cat = UIManager.Instance != null ? UIManager.Instance.Catalog : null;

            if (cat == null || cat.itemGachaPullButton == null) return;

            for (int i = 0; i < PullCounts.Length; i++)
            {
                int count = PullCounts[i];
                bool available = !_flaringPull && manager != null && manager.CanPullMulti(table, count);

                var capturedTable = table;

                var go = Object.Instantiate(cat.itemGachaPullButton, c.pullRow, false);
                var pull = go.GetComponent<GachaPullButtonView>();
                if (pull == null) continue;

                string title = count == 1 ? "1회 뽑기" : $"{count}연 뽑기";
                pull.Set(title, RewardPresentation.GachaCost(manager, table, count), available, cat.iconChest);
                pull.Button.onClick.AddListener(() => OnPullClicked(capturedTable, count, pull));
                if (count == 1) FeatureGuideAnchor.Bind(pull.Button, IsSkillTable(table) ? Direction.GameDirectTarget.SkillPullOnce : Direction.GameDirectTarget.EquipmentPullOnce);
                _activePullButtons.Add(pull.Button);
            }
        }

        private static bool IsSkillTable(GachaTableSO table) => table != null && table.gachaType == eGachaType.Skill;
        private static eCurrency TicketCurrency => IsSkillTable(_contentTable) ? eCurrency.MageTicket : eCurrency.EquipmentTicket;
        private static void OnWalletChanged(eCurrency currency, long amount)
        { if (_contentTable != null && (_contentTable.costCurrency == currency || TicketCurrency == currency)) UpdateWallet(); }
        private static void UpdateWallet()
        {
            if (_content == null || _contentTable == null) return;
            EconomyBridge.TryGetAmount(_contentTable.costCurrency,out long current);
            UpdateCostLabel(current);
            var ticketCurrency = TicketCurrency;
            EconomyBridge.TryGetAmount(ticketCurrency, out long tickets);
            string ticketName = RewardPresentation.Name(ticketCurrency);
            string ticketCount = $"보유 {NumberNotation.Format(tickets)}장";
            if (_content.ticketName != null && _content.ticketName.text != ticketName) _content.ticketName.text = ticketName;
            if (_content.ticketCount != null && _content.ticketCount.text != ticketCount) _content.ticketCount.text = ticketCount;
            UpdateDescription();
            bool pulling=_flaringPull || (GachaManager.Instance != null && GachaManager.Instance.IsPulling);
            for(int i=0;i<_activePullButtons.Count;i++)
            {
                var button=_activePullButtons[i];
                if(button==null)continue;
                int count=PullCounts[i];
                button.GetComponent<GachaPullButtonView>()?.Set(count==1?"1회 뽑기":$"{count}연 뽑기",RewardPresentation.GachaCost(GachaManager.Instance,_contentTable,count),!pulling && GachaManager.Instance != null && GachaManager.Instance.CanPullMulti(_contentTable,count));
            }
        }

        private static void UpdateCostLabel(long current)
        {
            if (_content.costLabel == null) return;
            string currency = RewardPresentation.Name(_contentTable.costCurrency);
            string label = $"보유 {currency}  {NumberNotation.Format(current)}";
            if (_content.costLabel.text != label) _content.costLabel.text = label;
        }

        private static void UpdateDescription()
        {
            if (_content == null || _contentTable == null) return;
            if (_content.pityRoot != null)
            {
                bool equipment = !IsSkillTable(_contentTable);
                _content.pityRoot.gameObject.SetActive(equipment);
                if (equipment && GachaManager.Instance != null)
                {
                    int count = GachaManager.Instance.EpicPityCount;
                    string progress = $"{count} / {GachaManager.EpicPityLimit}회";
                    string remaining = $"에픽 확정까지 {GachaManager.Instance.EpicPityRemaining}회";
                    if (_content.pityLabel.text != progress) _content.pityLabel.text = progress;
                    if (_content.pityRemaining.text != remaining) _content.pityRemaining.text = remaining;
                    _content.pityFill.fillAmount = Mathf.Clamp01((float)count / GachaManager.EpicPityLimit);
                }
            }
        }

        // 비용 통화 기준으로 해당 보상 항목이 유효한지 판정.
        // 스킬 가챠(ArcaneKnowledge): Skill + ArcaneKnowledge(비전지식) 만 허용
        //   — 서버 응답도 SkillCode 와 비전지식 누적 총량만 내려준다.
        // 장비 가챠(AncientCoin): Equipment + ClassFragment(전직 파편) 만 허용
        //   — 서버가 장비 ItemCode 에 10% 확률로 전직 파편을 섞어 내려준다.
        private static bool IsRewardValidForGacha(GachaRewardEntry r, bool isSkillGacha, bool isEquipGacha)
        {
            if (isSkillGacha)
                return r.rewardType == eGachaRewardType.Skill
                    || (r.rewardType == eGachaRewardType.Currency && r.currency == eCurrency.ArcaneKnowledge);
            if (isEquipGacha)
                return r.rewardType == eGachaRewardType.Equipment
                    || (r.rewardType == eGachaRewardType.Currency && r.currency == eCurrency.ClassFragment);
            return true;
        }

        private static void BuildRewardPreview(GachaTabContentView c, GachaTableSO table)
        {
            bool hasRewards = table.rewards != null && table.rewards.Count > 0;
            if (c.rewardGrid != null)
            {
                c.rewardGrid.gameObject.SetActive(hasRewards);
                DestroyChildren(c.rewardGrid);
            }
            if (!hasRewards) return;

            bool isSkillGacha = IsSkillTable(table);
            bool isEquipGacha = !isSkillGacha && table.costCurrency == eCurrency.AncientCoin;

            float totalWeight = 0f;
            for (int i = 0; i < table.rewards.Count; i++)
            {
                var r = table.rewards[i];
                if (r == null) continue;
                if (!IsRewardValidForGacha(r, isSkillGacha, isEquipGacha)) continue;
                totalWeight += Mathf.Max(0f, r.weight);
            }

            var sorted = new List<GachaRewardEntry>(table.rewards);
            sorted.Sort(CompareForPreview);

            var cardPrefab = UIManager.Instance != null && UIManager.Instance.Catalog != null
                ? UIManager.Instance.Catalog.itemGachaCard
                : null;
            if (cardPrefab == null || c.rewardGrid == null) return;

            for (int i = 0; i < sorted.Count; i++)
            {
                var entry = sorted[i];
                if (entry == null) continue;
                if (!IsRewardValidForGacha(entry, isSkillGacha, isEquipGacha)) continue;

                var cardGo = Object.Instantiate(cardPrefab, c.rewardGrid, false);
                var card = cardGo.GetComponent<GachaCardItemView>();
                if (card == null) continue;

                // 등급 테두리
                Color frameColor = new Color(1f, 1f, 1f, 0.20f);
                if (entry.rewardType == eGachaRewardType.Equipment && entry.equipmentData != null)
                    frameColor = UguiTheme.RarityColor(entry.equipmentData.rarity);
                else if (entry.rewardType == eGachaRewardType.Currency && entry.currency == eCurrency.ClassFragment)
                    frameColor = UguiTheme.RarityClassFragment;
                else if (entry.rewardType == eGachaRewardType.Currency && entry.currency == eCurrency.ArcaneKnowledge)
                    frameColor = UguiTheme.RarityArcane;
                else if (entry.rewardType == eGachaRewardType.Skill)
                    frameColor = MageSkillPresentation.Accent;
                card.SetRarityFrame(frameColor);

                // 아이콘
                Sprite displayIcon = entry.icon;
                if (displayIcon == null && entry.rewardType == eGachaRewardType.Currency)
                    displayIcon = RewardPresentation.Icon(entry.currency);
                if (entry.rewardType == eGachaRewardType.Equipment && entry.equipmentData != null && entry.equipmentData.icon != null)
                    displayIcon = entry.equipmentData.icon;
                else if (entry.rewardType == eGachaRewardType.Skill && displayIcon == null)
                {
                    var mtMgr = MageTowerManager.Instance;
                    var so = mtMgr != null ? mtMgr.GetSkillById(entry.skillId) : null;
                    if (so != null && so.icon != null) displayIcon = so.icon;
                }


                string iconFallback = null;
                Color iconFallbackColor = Color.white;

                card.SetIcon(displayIcon, iconFallback, iconFallbackColor);

                // 이름
                string displayName = entry.nameKor;
                if (entry.rewardType == eGachaRewardType.Equipment && entry.equipmentData != null)
                    displayName = entry.equipmentData.DisplayName;
                else if (entry.rewardType == eGachaRewardType.Skill && string.IsNullOrEmpty(displayName))
                {
                    var mtMgr = MageTowerManager.Instance;
                    var so = mtMgr != null ? mtMgr.GetSkillById(entry.skillId) : null;
                    if (so != null) displayName = !string.IsNullOrEmpty(so.nameKor) ? so.nameKor : so.nameEng;
                }
                if (card.nameLabel != null) card.nameLabel.text = displayName;

                // 하단: 등급/태그 + 확률 — subLabel에 두 줄로 표기
                string tag = null;
                Color tagColor = new Color(100f / 255f, 180f / 255f, 1f, 0.80f);
                if (entry.rewardType == eGachaRewardType.Equipment && entry.equipmentData != null)
                {
                    tag = GetRarityText(entry.equipmentData.rarity);
                    tagColor = UguiTheme.RarityColor(entry.equipmentData.rarity);
                }
                else if (entry.rewardType == eGachaRewardType.Currency && entry.currency == eCurrency.ClassFragment)
                {
                    tag = "전직 파편";
                    tagColor = UguiTheme.RarityClassFragment;
                }
                else if (entry.rewardType == eGachaRewardType.Currency && entry.currency == eCurrency.ArcaneKnowledge)
                {
                    tag = "비전지식";
                    tagColor = UguiTheme.RarityArcane;
                }
                else if (entry.rewardType == eGachaRewardType.Skill)
                {
                    var skill = MageTowerManager.Instance?.GetSkillById(entry.skillId);
                    tag = "스킬";
                    tagColor = frameColor;
                }


                float pct = totalWeight > 0f ? (entry.weight / totalWeight) * 100f : 0f;
                if (card.subLabel != null)
                {
                    card.subLabel.text = string.IsNullOrEmpty(tag) ? $"{pct:F2}%" : $"{tag}\n{pct:F2}%";
                    card.subLabel.color = tagColor;
                    card.subLabel.textWrappingMode = TMPro.TextWrappingModes.Normal;
                }
            }
        }

        private static int CompareForPreview(GachaRewardEntry a, GachaRewardEntry b)
        {
            int ra = GetSortRank(a);
            int rb = GetSortRank(b);
            return ra != rb ? ra - rb : (a?.skillId ?? -1).CompareTo(b?.skillId ?? -1);
        }

        private static int GetSortRank(GachaRewardEntry e)
        {
            if (e == null) return 999;
            if (e.rewardType == eGachaRewardType.Equipment && e.equipmentData != null)
            {
                switch (e.equipmentData.rarity)
                {
                    case eEquipmentRarity.Epic: return 0;
                    case eEquipmentRarity.Rare: return 1;
                    case eEquipmentRarity.Normal: return 2;
                }
            }

            if (e.rewardType == eGachaRewardType.Skill) return 3;
            if (e.rewardType == eGachaRewardType.Currency) return 4;
            return 5;
        }

        // ── 뽑기 실행 ──────────────────────────────────────────────────

        /// <summary>실제 버튼에서 받은 테이블·횟수로 뽑기를 요청한다. 마탑 연출 완료 전 취소·계정 전환·가림이 발생하면 세대를 검사해 소비를 취소한다.</summary>
        private static void OnPullClicked(GachaTableSO table, int count, GachaPullButtonView pull)
        {
            if (_flaringPull) return;
            if (!IsSkillTable(table)) { PullAndShowResult(table, count); return; }
            long guideEpoch = Direction.GameDirectInteraction.Epoch;
            bool guided = Direction.GameDirectInteraction.OwnsInput;
            long accountGeneration = KingdomIdle.Balance.LocalProgression.AccountGeneration;
            var network = Scripts.Core.Manager.NetworkManager.Instance;
            long networkGeneration = network != null ? network.AccountGeneration : -1;
            _flaringPull = true;
            UpdateWallet();
            pull.PlayMageFlare(
                () => {
                    _flaringPull = false;
                    // 연출 중 취소·계정 전환·로딩이 발생했으면 지연 콜백에서 새 계정 재화를 소비하지 않는다.
                    if (accountGeneration == KingdomIdle.Balance.LocalProgression.AccountGeneration &&
                        network == Scripts.Core.Manager.NetworkManager.Instance && (network == null || networkGeneration == network.AccountGeneration) &&
                        guideEpoch == Direction.GameDirectInteraction.Epoch &&
                        (!guided || (Direction.GameDirectInteraction.CurrentAccount && Direction.GameDirectInteraction.Armed))) PullAndShowResult(table, count);
                    else UpdateWallet();
                },
                () => { _flaringPull = false; UpdateWallet(); });
        }

        /// <summary>
        /// 뽑기를 실행하고 결과를 팝업으로 표시한다.
        /// 서버 가챠는 비동기이므로 콜백으로 결과를 받는다.
        /// 외부(가챠 결과 팝업의 다시뽑기 버튼)에서도 호출 가능.
        /// </summary>
        public static void PullAndShowResult(GachaTableSO table, int count)
        {
            var uiMgr = UIManager.Instance;
            var mgr = GachaManager.Instance;
            if (mgr == null) return;

            if (mgr.IsPulling)
            {
                if (uiMgr != null) uiMgr.ShowToast("이미 뽑기가 진행 중입니다.");
                return;
            }

            if (table == null || !table.isImplemented)
            {
                if (uiMgr != null) uiMgr.ShowToast("미구현 기능입니다.");
                return;
            }

            if (!mgr.CanPullMulti(table, count))
            {
                if (uiMgr != null) uiMgr.ShowToast(mgr.PullFailure(table, count));
                return;
            }

            mgr.TryPull(table, count,
                onSuccess: results =>
                {
                    if (results == null || results.Count == 0)
                    {
                        if (uiMgr != null) uiMgr.ShowToast("뽑기 결과가 없습니다.");
                        RefreshContent();
                        return;
                    }
                    if (uiMgr != null)
                        uiMgr.ShowGachaResultPopup(results, table, count);
                    RefreshContent();
                },
                onError: message =>
                {
                    if (uiMgr != null)
                        uiMgr.ShowToast(string.IsNullOrEmpty(message) ? "뽑기에 실패했습니다." : message);
                    RefreshContent();
                });
        }

        // ── GachaManager 이벤트 핸들러 ────────────────────────────────

        private static void OnPullStateChanged(bool isPulling)
        {
            // 뽑기 시작: 버튼 즉시 비활성화(서버 응답 대기 중 중복 클릭 차단)
            // 뽑기 종료: 콘텐츠는 PullAndShowResult 콜백에서 RefreshContent 로 재계산.
            if (!isPulling) return;

            for (int i = 0; i < _activePullButtons.Count; i++)
            {
                var btn = _activePullButtons[i];
                if (btn == null) continue;
                btn.interactable = false;
            }
        }

        // ── 헬퍼 ───────────────────────────────────────────────────────

        /// <summary>직렬화된 본문을 재사용하고 탭별 뽑기 버튼만 다시 연결한다.</summary>
        private static GachaTabContentView SpawnContent()
        {
            _content = null;
            if (_view == null) return null;
            _content = _view.authoredContent;
            if (_content == null) { Debug.LogError("[GachaPanel] 본문 참조가 없습니다."); return null; }
            CloseRewards();
            DestroyChildren(_content.pullRow);
            return _content;
        }

        /// <summary>전체 안내 메시지만 표시하고 나머지 섹션은 숨긴다.</summary>
        private static void ShowMessage(string text)
        {
            var c = SpawnContent();
            if (c == null) return;

            if (c.messageLabel != null)
            {
                c.messageLabel.gameObject.SetActive(true);
                c.messageLabel.text = text;
            }
            if (c.costLabel != null) c.costLabel.gameObject.SetActive(false);
            if (c.pullRow != null) c.pullRow.gameObject.SetActive(false);
            if (c.pityRoot != null) c.pityRoot.gameObject.SetActive(false);
        }

        private static void DestroyChildren(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                parent.GetChild(i).gameObject.SetActive(false);
                Object.Destroy(parent.GetChild(i).gameObject);
            }
        }

        private static void OpenRates()
        {
            var host = UIManager.Instance;
            string url = host.Catalog.gachaRatesUrl;
            if (!System.Uri.TryCreate(url, System.UriKind.Absolute, out var uri) || uri.Scheme != "https")
            { host.ShowToast("확률표 주소를 확인할 수 없습니다."); return; }
            Application.OpenURL(url.TrimEnd('#') + (IsSkillTable(_contentTable) ? "#mage-tower" : "#equipment"));
        }

        private static void OpenRewards()
        {
            if (_view == null || _contentTable == null) return;
            CloseRewards();
            var prefab = UIManager.Instance.Catalog.popupGachaRewards;
            if (prefab == null) { UIManager.Instance.ShowToast("보상 목록을 준비하지 못했습니다."); return; }
            // 패널이 가려지거나 제거될 때 상세 창도 함께 숨겨진다.
            _rewardsPopup = Object.Instantiate(prefab, _view.transform, false);
            var popup = _rewardsPopup.GetComponent<GachaRewardsPopupView>();
            popup.title.text = (IsSkillTable(_contentTable) ? "마탑" : "장비") + " 획득 가능 보상";
            popup.close.onClick.AddListener(CloseRewards);
            ModalBackHandler.Bind(_rewardsPopup, CloseRewards);
            BuildRewardPreview(popup.rewards, _contentTable);
            UITween.PopIn(popup.box);
        }

        private static void CloseRewards()
        {
            if (_rewardsPopup == null) return;
            _rewardsPopup.SetActive(false);
            Object.Destroy(_rewardsPopup);
            _rewardsPopup = null;
        }

        private static string GetRarityText(eEquipmentRarity rarity)
        {
            switch (rarity)
            {
                case eEquipmentRarity.Normal: return "일반";
                case eEquipmentRarity.Rare: return "레어";
                case eEquipmentRarity.Epic: return "에픽";
                default: return rarity.ToString();
            }
        }
    }
}
