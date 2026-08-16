using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GuideQuestUI : MonoBehaviour
{
    /// <summary>
    /// [임시] 하단 중앙에 신 스킬(궁극기) 버튼이 들어오면서 자리가 겹쳐 가이드 퀘스트 창을 잠시 숨긴다.
    /// 퀘스트 로직(QuestManager)은 그대로 돌아가며(진행/보상 상태 유지), UI만 헤드리스가 된다.
    /// 되살리려면 false 로 바꾸면 된다. (const 대신 readonly — 도달 불가 코드 경고 방지)
    /// </summary>
    private static readonly bool GuideQuestTemporarilyHidden = true;

    [SerializeField] private TextMeshProUGUI _context;
    [SerializeField] private TextMeshProUGUI _progress;
    private Button btn;

    private long _currentQuest;
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
        QuestManager.Instance.OnGuideQuestChanged += RefreshUI;
        QuestManager.Instance.OnQuestProgressChanged += RefreshUI;
        QuestManager.Instance.ClaimUIRefresh();
    }

    private void OnDisable()
    {
        QuestManager.Instance.OnGuideQuestChanged -= RefreshUI;
        QuestManager.Instance.OnQuestProgressChanged -= RefreshUI;
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
        _currentQuest = state.QuestId;
        
        _context.text = definition.Title;
        string color = state.IsCompleted ? "blue" : "red";
        string countText = $"<color={color}>({state.CurrentProgress} / {definition.RequiredCount})</color>";
        _progress.text = definition.Description + countText;
        btn.interactable = state.IsCompleted;
    }

    private void ComplainReward()
    {
        QuestManager.Instance.ClaimQuestReward(_currentQuest);
    }
}
