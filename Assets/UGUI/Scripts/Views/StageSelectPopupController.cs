using Scripts.Core;
using Scripts.Core.Manager;
using System.Linq;
using UnityEngine;

namespace KingdomIdle.UGUI
{
    /// <summary>기존 카탈로그와 StageManager만 읽고 명시적인 선택 이동을 요청한다.</summary>
    public static class StageSelectPopupController
    {
        private static StageSelectPopupView _view;
        private static int _chapter, _wave;
        public static bool IsOpen => _view != null && _view.gameObject.activeSelf;

        public static void Show()
        {
            var manager = StageManager.Instance;
            var host = UIManager.Instance;
            if (manager?.CurrentDefinition?.Type != eStageType.Main || host?.Catalog?.popupStageSelect == null) return;
            if (_view == null)
            {
                _view = Object.Instantiate(host.Catalog.popupStageSelect, host.LayerPopups, false).GetComponent<StageSelectPopupView>();
                _view.close.onClick.AddListener(Hide); _view.backdrop.onClick.AddListener(Hide);
                _view.previous.onClick.AddListener(() => Browse(-1)); _view.next.onClick.AddListener(() => Browse(1));
                _view.frontier.onClick.AddListener(() => Move(StageManager.Instance.CycleFrontierStage));
                _view.travel.onClick.AddListener(() => Move(StageParser.MakeStage(eStageType.Main, _chapter, _wave)));
                for (int i=0;i<_view.cells.Length;i++)
                {
                    int wave=i+1;
                    _view.cells[i].button.onClick.AddListener(() => { _wave=wave; Refresh(); });
                }
                ModalBackHandler.Bind(_view.gameObject, Hide);
            }
            _chapter=manager.CurrentStageNumber; _wave=manager.CurrentWaveNumber;
            _view.gameObject.SetActive(true); _view.transform.SetAsLastSibling();
            Refresh(); UITween.PopIn(_view.panel);
        }
        public static void Hide() { if (_view != null) _view.gameObject.SetActive(false); }
        public static void Dispose() { if (_view != null) Object.Destroy(_view.gameObject); _view=null; }
        private static void Browse(int offset) { _chapter+=offset; _wave=1; Refresh(); }
        private static void Move(eStage stage)
        {
            if (StageManager.Instance != null && StageManager.Instance.TryMoveToMainStage(stage)) Hide();
            else UIManager.Instance?.ShowToast("현재 이동할 수 없는 스테이지입니다. 전투 상태를 확인해 주세요.");
        }
        public static void Refresh()
        {
            if (!IsOpen) return;
            var manager=StageManager.Instance;
            if (manager?.CurrentDefinition?.Type != eStageType.Main) { Hide(); return; }
            var frontier=manager.CycleFrontierStage;
            int lastChapter=StageParser.GetStageNumber(frontier);
            _chapter=Mathf.Clamp(_chapter,1,lastChapter);
            int region=(_chapter-1)%3;
            _view.chapter.text=$"{_chapter}장 · {(region==0?"산적 야영지":region==1?"고블린 숲":"오크 황무지")}";
            _view.current.text=$"현재 스테이지 {manager.CurrentStageNumber}-{manager.CurrentWaveNumber}";
            _view.previous.interactable=_chapter>1; _view.next.interactable=_chapter<lastChapter;
            for(int i=0;i<_view.cells.Length;i++)
            {
                var cell=_view.cells[i]; var id=StageParser.MakeStage(eStageType.Main,_chapter,i+1);
                bool available=(long)id<=(long)frontier && manager.TryGetStageDefinition(id,out var definition) && definition.Enabled;
                cell.button.interactable=available;
                cell.button.image.color=i+1==_wave?UguiTheme.BtnConfirm:UguiTheme.RusticSurface;
                cell.label.text=i==10?$"{_chapter}장 보스":$"{_chapter}-{i+1}";
                cell.status.text=!available?"잠금":id==manager.CurrentStage?"현재":manager.IsCycleStageCleared(id)?"클리어":"도전";
            }
            var selected=StageParser.MakeStage(eStageType.Main,_chapter,_wave);
            bool exists=manager.TryGetStageDefinition(selected,out var selectedDefinition);
            bool valid=(long)selected<=(long)frontier && exists && selectedDefinition.Enabled;
            string enemies=exists ? string.Join(" · ", selectedDefinition.MonsterEntries.Select(entry =>
                StageCatalogRules.Database.CatalogMonsters.FirstOrDefault(info => info.Id==entry.MonsterType).DisplayName).Where(name=>!string.IsNullOrEmpty(name)).Distinct()) : "";
            _view.details.text=$"스테이지 {_chapter}-{_wave} · {(manager.IsCycleStageCleared(selected)?"클리어":valid?"도전 가능":"잠금")}\n등장 적  {enemies}\n보상  골드 · EXP · 장비";
            _view.frontierLabel.text=$"최고 스테이지 도전\n({StageParser.GetStageNumber(frontier)}-{StageParser.GetWaveNumber(frontier)})";
            _view.travelLabel.text=$"{_chapter}-{_wave}스테이지 이동";
            bool running=manager.CurrentRunState==eStageRunState.Running;
            _view.travel.interactable=valid && running;
            _view.frontier.interactable=running;
        }
    }
}
