using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// User 스크립트에 지갑 정보, Player 3마리 연결, Player에서 User로 연결 로직 추가
public class User : MonoBehaviour
{
    public Wallet wallet;

    public Player first_Player;
    public Player second_Player;
    public Player third_Player;

    public List<Player> players; 

    // Wallet과 Player 3마리 연결
    void Start()
    {
        wallet = GetComponent<Wallet>();
        first_Player = GetComponent<Player>();
        second_Player = GetComponent<Player>();
        third_Player = GetComponent<Player>();

        // Player에서 User로 연결 로직 추가
        ConnectPlayerToUser(first_Player);
        ConnectPlayerToUser(second_Player);
        ConnectPlayerToUser(third_Player);
    }

    // Player에서 User로 연결 함수
    public void ConnectPlayerToUser(Player player)
    {
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i] == player)
            {
                Debug.Log(players[i] + " User에 연결 완료.");
            }
        }
    }
}
