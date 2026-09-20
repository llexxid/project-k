using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KingdomIdle.Balance;

public class GuideQuestUI : MonoBehaviour
{
    /// <summary>
    /// 구형 가이드 창은 숨긴다. 현재 가이드는 UGUI HUD에서 표시한다.
    /// 퀘스트 로직(QuestManager)은 그대로 돌아가며(진행/보상 상태 유지), UI만 헤드리스가 된다.
    /// 호환 씬에서만 사용하며 현재 HUD와 중복 표시하지 않는다.
    /// </summary>
    private static readonly bool GuideQuestTemporarilyHidden = true;

    [SerializeField] private TextMeshProUGUI _context;
    [SerializeField] private TextMeshProUGUI _progress;
    private Button btn;

    // 기존 프리팹을 다시 활성화해도 표시한 계정의 보상만 요청한다.
    private QuestManager _boundManager;
    private QuestClaimToken _claimToken;
    private bool _hasClaimToken;
    private void Awake()
    {
        if (GuideQuestTemporarilyHidden)
        {
            // OnEnable(이벤트 구독) 전에 꺼지므로 구독/해제 짝이 어긋나거나 NRE가 날 일이 없다
            gameObject.SetActive(false);
            return;
        }

        btn = GetComponent<Button>();
        btn.interactable = false;
        btn.onClick.AddListener(ComplainReward);
    }

    private void OnEnable()
    {
        _boundManager = QuestManager.Instance;
        if (_boundManager == null) return;
        _boundManager.OnGuideQuestChanged += RefreshUI;
        _boundManager.OnQuestProgressChanged += RefreshUI;
        _boundManager.ClaimUIRefresh();
    }

    private void OnDisable()
    {
        if (_boundManager != null)
        {
            _boundManager.OnGuideQuestChanged -= RefreshUI;
            _boundManager.OnQuestProgressChanged -= RefreshUI;
        }
        _boundManager = null;
        _hasClaimToken = false;
    }
    
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha9) && btn != null && btn.interactable)
        {
            btn.onClick.Invoke();
        }
    }

    private void RefreshUI(QuestRuntimeState state, QuestDefinition definition)
    {
        _hasClaimToken = false;
        if (state == null || definition == null || _boundManager == null)
        {
            if (btn != null) btn.interactable = false;
            return;
        }
        foreach (var row in _boundManager.GetSnapshot(eQuestCategory.Guide).Rows)
            if (row.Token.QuestId == state.QuestId && row.CanClaim)
            { _claimToken = row.Token; _hasClaimToken = true; break; }

        _context.text = definition.Title;
        string color = state.IsCompleted ? "blue" : "red";
        string countText = $"<color={color}>({state.CurrentProgress} / {definition.RequiredCount})</color>";
        _progress.text = definition.Description + countText;
        btn.interactable = state.IsCompleted && _hasClaimToken;
    }

    private void ComplainReward()
    {
        if (_boundManager == null || !_hasClaimToken) return;
        _boundManager.TryClaim(_claimToken);
        _boundManager.ClaimUIRefresh();
    }
}
