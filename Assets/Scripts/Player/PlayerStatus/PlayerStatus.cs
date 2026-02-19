using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu (fileName = "PlayerStatus", menuName = "ScriptableObjects/PlayerStatus", order = 1)]
public class PlayerStatus : MonoBehaviour
{
    public int HP { get; set; } = 100;
    public int Atk { get; set; } = 10;
    public int MovSpeed { get; set; } = 5;
    public float AtkSpeed { get; set; } = 1;
    public string JobName { get; set; } = "Warrior";

}
