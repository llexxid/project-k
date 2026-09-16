using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
