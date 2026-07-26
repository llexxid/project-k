using System.Collections;
using System.Collections.Generic;
using KingdomIdle.UGUI;
using KingdomIdle.UI;
using UnityEngine;
using UnityEngine.UI;

public class DungeonButtonView : MonoBehaviour
{
    private Button dungeonButton;
    // Start is called before the first frame update
    void Start()
    {
        dungeonButton = gameObject.GetComponent<Button>();
        if (dungeonButton == null) return;
        dungeonButton.onClick.AddListener(() =>
            UIManager.Instance.PushPanel(
                UIPanelId.Dungeon,
                "dungeonPanel",
                clearBefore: false,
                isTabPanel: false));
    }
}
