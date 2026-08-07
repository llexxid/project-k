using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GuideQuestUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _context;
    [SerializeField] private TextMeshProUGUI _progress;
    private Button btn;

    private long _currentQuest;
    private void Awake()
    {
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
