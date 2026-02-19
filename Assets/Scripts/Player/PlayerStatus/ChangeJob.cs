using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChangeJob : MonoBehaviour
{
    public PlayerStatus playerStatus;

    private void Start()
    {
        playerStatus = GetComponent<PlayerStatus>();
    }

    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.J))
        {
            if(playerStatus.JobName == "Warrior")
            {
                playerStatus.JobName = "Mage";
                playerStatus.HP = 80;
                playerStatus.Atk = 15;
                playerStatus.MovSpeed = 4;
                playerStatus.AtkSpeed = 1.5f;
            }
            else
            {
                playerStatus.JobName = "Warrior";
                playerStatus.HP = 100;
                playerStatus.Atk = 10;
                playerStatus.MovSpeed = 5;
                playerStatus.AtkSpeed = 1f;
            }
        }
    }
}
