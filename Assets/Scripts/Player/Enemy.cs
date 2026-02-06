using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    public Enemy enemy;

    void Start()
    {         
        enemy = GetComponent<Enemy>();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Player¿Í Ãæµ¹!");
            enemy.gameObject.SetActive(false);
        }
    }
}
