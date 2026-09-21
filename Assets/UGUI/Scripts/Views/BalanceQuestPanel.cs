using System.Collections.Generic;
using System.Linq;
using KingdomIdle.Balance;
using UnityEngine;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 네 퀘스트 탭과 하나의 목록에 표시할 내용을 연결하는 패널 컨트롤러다.
    /// QuestManager의 불변 snapshot을 읽고 표시 당시 token으로 수령을 요청한다.
    /// 집계·기간·업적 단계 판정은 매니저에 남기고, 구독은 이 패널의 활성 생명주기를 따른다.
    /// </summary>
    public sealed class BalanceQuestPanel : MonoBehaviour
    {
        // 버튼 표시 순서와 조회할 범주를 함께 고정한다. 저장용 enum 순서는 바꾸지 않는다.
        private static readonly eQuestCategory[] Categories =
            { eQuestCategory.Guide, eQuestCategory.Daily, eQuestCategory.Weekly, eQuestCategory.Achievement };
        private static readonly string[] CategoryLabels = { "가이드", "일일", "주간", "업적" };

        // 이 인스턴스가 바인딩한 셸과 현재 구독 중인 매니저다.
        private GuidePanelView _view;
        private QuestManager _manager;

        // 처음 열 때는 가이드다. 다른 패널에 덮였다 돌아와도 같은 인스턴스의 선택은 유지한다.
        private eQuestCategory _selectedCategory = eQuestCategory.Guide;
        private readonly List<NavTabButtonView> _tabs = new();

        // 목록 생성 여부, 표시 중인 행, 다음에 반영할 snapshot을 구분해 불필요한 재생성을 줄인다.
        private bool _built;
        private readonly List<Row> _rows = new();
        private readonly List<QuestRowSnapshot> _visible = new();
        private GuideEmptyHintView _emptyHint;
        private string _emptyText;
        private QuestScrollDragRelay _dragRelay;
        private bool _dragging, _deferredRebind;
        private SheetSizeFitter _sizeFitter;
        private RectTransform _hudTop;

        /// <summary>화면 입력과 검증에서 읽는 현재 선택 범주다. 변경은 SelectCategory로만 한다.</summary>
        public eQuestCategory SelectedCategory => _selectedCategory;

        /// <summary>스택 복귀 시 기존 탭 선택을 유지하며 최신 매니저에 다시 연결한다.</summary>
        private void OnEnable() { Connect(); }

        /// <summary>UI 또는 매니저가 늦게 준비된 경우에만 최초 바인딩을 재시도한다.</summary>
        private void Update()
        {
            if (_view == null) return;
            if (_hudTop == null) BindHeightBoundary();
            if (_tabs.Count == 0)
            {
                BuildTabs();
                // UIManager가 뒤늦게 준비되었다면 탭뿐 아니라 보류했던 행·빈 안내도 한 번 복구한다.
                if (_tabs.Count != 0) Rebind(true);
            }
            if (_manager == null) Connect();
        }

        /// <summary>닫기 또는 스택 가림 시 구독만 해제한다. 같은 패널의 버튼과 선택 상태는 보존한다.</summary>
        private void OnDisable()
        {
            if (_manager != null) _manager.QuestsChanged -= OnQuestsChanged;
            _manager = null;
            _dragging = _deferredRebind = false;
        }

        /// <summary>셸을 연결한다. 같은 View로 반복 호출해도 탭 버튼과 클릭 콜백을 추가하지 않는다.</summary>
        /// <param name="view">UIManager가 생성한 퀘스트 패널의 직렬화 참조다.</param>
        public void Bind(GuidePanelView view)
        {
            if (view == null) return;

            // 다른 셸로 옮겨 바인딩할 때만 이전 인스턴스가 만든 표시 객체를 정리한다.
            if (_view != view)
            {
                if (_dragRelay != null) _dragRelay.DragChanged = null;
                foreach (var tab in _tabs)
                    if (tab != null) { tab.gameObject.SetActive(false); Destroy(tab.gameObject); }
                _tabs.Clear();
                ClearRows();
                _built = false;
            }
            _view = view;
            view.SetTitle("퀘스트");
            _sizeFitter = view.Sheet != null ? view.Sheet.GetComponent<SheetSizeFitter>() : null;
            BindHeightBoundary();
            if (view.scroll != null)
            {
                _dragRelay = view.scroll.GetComponent<QuestScrollDragRelay>() ?? view.scroll.gameObject.AddComponent<QuestScrollDragRelay>();
                _dragRelay.DragChanged = OnDragChanged;
            }

            // 이전 문구 영역은 탭 행으로 대체한다. 기존 필드는 직렬화 호환성을 위해 유지한다.
            if (view.progressFill != null) view.progressFill.transform.parent.gameObject.SetActive(false);
            if (view.progressLabel != null) view.progressLabel.gameObject.SetActive(false);
            NumberNotationBinding.Bind(view, Refresh);
            BuildTabs();
            UpdateSelection();
            Connect();
            Rebind(true);
        }

        /// <summary>탭 클릭 또는 명시적 화면 입력으로 범주를 선택하고 목록을 맨 위로 이동한다.</summary>
        /// <param name="category">표시할 가이드·일일·주간·업적 범주다. 같은 범주나 잘못된 값은 무시한다.</param>
        public void SelectCategory(eQuestCategory category)
        {
            if ((uint)category >= (uint)Categories.Length || category == _selectedCategory) return;
            _selectedCategory = category;
            _dragging = _deferredRebind = false;

            // 카드의 활성 여부와 목록이 먼저 바뀌어야 실제 스크롤 높이를 계산할 수 있다.
            UpdateSelection();
            Rebind(true);
            if (_view == null || _view.scroll == null) return;

            // 입력 시 한 번만 레이아웃을 확정하고 관성을 끈다. 짧은 탭에 옛 스크롤 위치가 남지 않는다.
            Canvas.ForceUpdateCanvases();
            _view.scroll.StopMovement();
            _view.scroll.verticalNormalizedPosition = 1f;
        }

        /// <summary>공용 프리팹으로 텍스트 탭 네 개를 한 번 만든다. 공용 높이·선택 연출은 그대로 쓴다.</summary>
        private void BuildTabs()
        {
            if (_view == null || _view.tabBar == null || _tabs.Count != 0) return;
            // UI 초기화가 늦으면 Update에서 다시 시도한다. 별도의 임시 버튼은 생성하지 않는다.
            var prefab = UIManager.Instance != null ? UIManager.Instance.Catalog?.itemNavTabButton : null;
            var template = prefab != null ? prefab.GetComponent<NavTabButtonView>() : null;
            if (template == null || template.Button == null) return;

            for (int i = 0; i < Categories.Length; i++)
            {
                // 콜백마다 자기 범주를 보관하여 다른 버튼의 선택으로 바뀌지 않도록 한다.
                var category = Categories[i];
                var tab = Instantiate(prefab, _view.tabBar, false).GetComponent<NavTabButtonView>();
                tab.SetLabel(CategoryLabels[i]);
                tab.SetIcon(null);

                // 글자 길이와 무관하게 네 버튼이 같은 폭을 나눠 가진다. 공용 프리팹 자산은 수정하지 않는다.
                var layout = tab.GetComponent<UnityEngine.UI.LayoutElement>();
                if (layout != null) { layout.minWidth = 0; layout.preferredWidth = 0; layout.flexibleWidth = 1; }
                tab.Button.onClick.AddListener(() => SelectCategory(category));
                _tabs.Add(tab);
            }
            UpdateSelection();
        }

        /// <summary>선택 탭만 강조한다. 중복 요약 카드는 팝업에서만 숨기고 인게임 HUD는 건드리지 않는다.</summary>
        private void UpdateSelection()
        {
            for (int i = 0; i < _tabs.Count; i++)
                _tabs[i].SetSelected(Categories[i] == _selectedCategory, UguiTheme.AccentGold);
            if (_view == null || _view.currentQuestRoot == null) return;
            if (_view.currentQuestRoot.activeSelf) _view.currentQuestRoot.SetActive(false);
        }

        private void BindHeightBoundary()
        {
            _hudTop = UIManager.Instance != null ? UIManager.Instance.HudTop : null;
            if (_sizeFitter != null) _sizeFitter.SetTopBoundary(_hudTop);
        }

        private void OnDragChanged(bool dragging)
        {
            _dragging = dragging;
            if (!dragging && _deferredRebind && isActiveAndEnabled)
            {
                _deferredRebind = false;
                Rebind(false);
            }
        }

        /// <summary>패널이 먼저 켜졌어도 매니저가 생기는 시점에 한 번 구독하고 초기 화면을 읽는다.</summary>
        private void Connect()
        {
            if (_manager != null || QuestManager.Instance == null || !isActiveAndEnabled) return;
            _manager = QuestManager.Instance;
            _manager.QuestsChanged += OnQuestsChanged;
            Rebind(true);
        }

        /// <summary>계정 또는 현재 탭의 내용이 변했을 때만 다시 읽는다. 다른 탭의 변경은 그 탭을 열 때 읽는다.</summary>
        /// <param name="changes">매니저가 발행한 변경 범주와 계정 전환 정보다.</param>
        private void OnQuestsChanged(QuestChangeSet changes)
        {
            if (changes.AccountChanged) { _dragging = _deferredRebind = false; Rebind(false); return; }
            foreach (var category in changes.ChangedCategories)
                if (category == _selectedCategory) { Rebind(false); return; }
        }

        /// <summary>숫자 표기 설정이 바뀌면 현재 탭의 같은 데이터를 새 표기로 그린다.</summary>
        private void Refresh() { Rebind(true); }

        /// <summary>표시용 목록만 안정 정렬한다. 토큰이 같은 카드는 재사용하고 진행 숫자는 제자리에서 갱신한다.</summary>
        private void Rebind(bool force)
        {
            if (_view == null || _view.listContent == null) return;
            // 매니저의 데이터가 먼저 준비되어도 UI 카탈로그 없이 행을 만들지 않는다.
            // 최초 탭 생성에 성공한 Update가 이 조회를 다시 수행한다.
            var catalog = UIManager.Instance != null ? UIManager.Instance.Catalog : null;
            if (catalog == null || catalog.itemGuideStepRow == null || catalog.itemGuideEmptyHint == null) return;
            _visible.Clear();
            // 조회는 선택한 범주 하나로 제한한다. 준비 중에는 완료와 다른 빈 안내를 사용한다.
            var board = _manager != null ? _manager.GetSnapshot(_selectedCategory) : null;
            if (board != null)
                foreach (var row in board.Rows.OrderBy(x => StateOrder(x.State))) _visible.Add(row);

            if (_dragging)
            {
                // 손가락 아래의 카드 위치는 유지하지만 수령 상태는 즉시 확정값으로 갱신한다.
                _deferredRebind = true;
                foreach (var row in _rows)
                {
                    var snapshot = _visible.Find(x => x.Token == row.Snapshot.Token);
                    if (snapshot == null) { row.View.actionButton.interactable = false; continue; }
                    bool changed = !row.Snapshot.ContentEquals(snapshot);
                    row.SkipScrollAnchor |= snapshot.State == QuestRowState.Claimed && row.Snapshot.State != QuestRowState.Claimed;
                    row.Snapshot = snapshot;
                    if (force || changed) Render(row);
                }
                return;
            }

            bool reorder = !_built || _visible.Count != _rows.Count;
            for (int i = 0; !reorder && i < _visible.Count; i++)
                reorder = _visible[i].Token != _rows[i].Snapshot.Token;
            if (reorder) ReconcileRows();
            _built = true;

            // 완료 행도 남으므로 빈 목록은 준비 중 또는 등록된 항목이 없는 경우다.
            if (_visible.Count == 0) ShowEmpty(board != null && board.IsReady);
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                bool changed = !row.Snapshot.ContentEquals(_visible[i]);
                row.Snapshot = _visible[i];
                if (force || changed) Render(row);
                row.SkipScrollAnchor = false;
            }
        }

        private static int StateOrder(QuestRowState state) => state switch
        {
            QuestRowState.Claimable => 0,
            QuestRowState.InProgress => 1,
            QuestRowState.Locked => 2,
            _ => 3
        };

        private float RowTop(Row row) => _view.scroll.viewport.InverseTransformPoint(
            row.View.transform.TransformPoint(new Vector3(0, ((RectTransform)row.View.transform).rect.yMax, 0))).y;

        /// <summary>재정렬 전 보이던 카드를 기준점으로 보존한다. 방금 수령한 카드를 따라 맨 아래로 내려가지 않는다.</summary>
        private void ReconcileRows()
        {
            var scroll = _view.scroll;
            bool atTop = !_built || scroll == null || scroll.verticalNormalizedPosition >= .999f;
            Row anchor = null;
            float anchorTop = 0;
            if (!atTop)
            {
                Canvas.ForceUpdateCanvases();
                foreach (var row in _rows)
                {
                    var next = _visible.Find(x => x.Token == row.Snapshot.Token);
                    float top = RowTop(row);
                    float bottom = top - ((RectTransform)row.View.transform).rect.height;
                    if (next == null || row.SkipScrollAnchor || (next.State == QuestRowState.Claimed && row.Snapshot.State != QuestRowState.Claimed) ||
                        bottom >= scroll.viewport.rect.yMax || top <= scroll.viewport.rect.yMin) continue;
                    anchor = row; anchorTop = top; break;
                }
            }
            var existing = _rows.ToDictionary(x => x.Snapshot.Token);
            _rows.Clear();
            if (_emptyHint != null) { _emptyHint.gameObject.SetActive(false); Destroy(_emptyHint.gameObject); _emptyHint = null; _emptyText = null; }
            foreach (var snapshot in _visible)
            {
                if (existing.TryGetValue(snapshot.Token, out var row))
                {
                    existing.Remove(snapshot.Token);
                    _rows.Add(row);
                }
                else Add(snapshot);
                _rows[_rows.Count - 1].View.transform.SetSiblingIndex(_rows.Count - 1);
            }
            foreach (var row in existing.Values) { row.View.gameObject.SetActive(false); Destroy(row.View.gameObject); }
            if (scroll == null) return;
            scroll.StopMovement();
            Canvas.ForceUpdateCanvases();
            if (atTop) scroll.verticalNormalizedPosition = 1;
            else
            {
                if (anchor != null && _rows.Contains(anchor))
                {
                    var offset = _view.listContent.anchoredPosition;
                    offset.y += anchorTop - RowTop(anchor);
                    _view.listContent.anchoredPosition = offset;
                }
                scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition);
            }
        }

        /// <summary>행 구성이 바뀔 때만 목록을 비운다. 지연 파괴 전 비활성화하여 레이아웃 중복을 막는다.</summary>
        private void ClearRows()
        {
            if (_view != null && _view.listContent != null)
                foreach (Transform child in _view.listContent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            _rows.Clear();
            _emptyHint = null;
            _emptyText = null;
        }

        /// <summary>기존 빈 안내 프리팹을 재사용하고, 준비 상태 또는 선택한 범주에 맞는 문구만 갱신한다.</summary>
        /// <param name="isReady">저장된 퀘스트 상태가 매니저에 준비되었는지 나타낸다.</param>
        private void ShowEmpty(bool isReady)
        {
            // 빈 목록끼리 탭을 바꾸어도 안내 객체는 하나만 유지한다.
            if (_emptyHint == null)
            {
                var prefab = UIManager.Instance != null ? UIManager.Instance.Catalog?.itemGuideEmptyHint : null;
                if (prefab == null) return;
                _emptyHint = Instantiate(prefab, _view.listContent, false).GetComponent<GuideEmptyHintView>();
                if (_emptyHint == null) return;
            }
            string text = !isReady ? "퀘스트 정보를 준비하고 있습니다." : _selectedCategory switch
            {
                eQuestCategory.Guide => "등록된 가이드 퀘스트가 없습니다.",
                eQuestCategory.Daily => "등록된 일일 퀘스트가 없습니다.",
                eQuestCategory.Weekly => "등록된 주간 퀘스트가 없습니다.",
                _ => "등록된 업적이 없습니다."
            };
            if (_emptyText == text) return;
            _emptyText = text;
            _emptyHint.SetText(text);
        }

        private void Add(QuestRowSnapshot snapshot)
        {
            var catalog = UIManager.Instance.Catalog;
            var view = Instantiate(catalog.itemGuideStepRow, _view.listContent, false).GetComponent<GuideStepRowView>();
            var row = new Row { Snapshot = snapshot, View = view };
            _rows.Add(row);
            // 보상 아이콘과 카드 전체는 표시 전용이다. 동작은 우측 버튼 한 곳에서만 시작한다.
            view.actionButton.onClick.AddListener(() => Act(row));
            Render(row);
        }

        /// <summary>화면에 바인딩한 상태에 맞춰 이동 또는 수령한다. 완료·잠금 행은 입력을 무시한다.</summary>
        private void Act(Row row)
        {
            if (_manager == null) return;
            // 숨겨진/이전 계정의 행에서 남은 입력은 이동과 수령 모두 실행하지 않는다.
            if (row.Snapshot.Token.AccountGeneration != LocalProgression.AccountGeneration) { Rebind(false); return; }
            if (row.Snapshot.CanClaim) Claim(row);
            else if (row.Snapshot.State == QuestRowState.InProgress)
                QuestNavigation.Navigate(row.Snapshot.ObjectiveType, row.Snapshot.TargetId);
        }

        /// <summary>화면에 표시했던 기간 토큰 그대로 요청한다. 저장 실패 시 UI가 보상이나 진행도를 추측해 바꾸지 않는다.</summary>
        private void Claim(Row row)
        {
            if (_manager == null) return;
            var result = _manager.TryClaim(row.Snapshot.Token);
            if (result.Status == QuestClaimStatus.SaveFailed)
                UIManager.Instance?.ShowToast("보상 저장에 실패했습니다. 잠시 후 다시 시도해 주세요.");
            else if (result.Succeeded && row.Snapshot.Category == eQuestCategory.Guide)
                UIManager.Instance?.ShowToast("가이드 완료");
            Rebind(false);
        }

        private static void Render(Row row)
        {
            row.View.SetQuest(row.Snapshot, UIManager.Instance.Catalog);
        }

        private sealed class Row
        {
            public QuestRowSnapshot Snapshot;
            public GuideStepRowView View;
            public bool SkipScrollAnchor;
        }
    }
}
