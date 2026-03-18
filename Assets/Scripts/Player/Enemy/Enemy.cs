using Scripts.Core.inteface;
using UnityEngine;

public class Enemy : MonoBehaviour, IDamageable
{
    public Enemy enemy;
    public int hp;
    public Transform player;
    public float moveSpeed = 3f;
    public float detectionRange = 10f;

    public Vector3 targetPos
    {
        get { return gameObject.transform.position; }
    }

    void Start()
    {
        enemy = GetComponent<Enemy>();
        player = GameObject.FindWithTag("Player").transform;
        hp = 100;
    }

    void Update()
    {
        if (player != null)
        {
            float distance = Vector2.Distance(transform.position, player.position);
            if (distance <= detectionRange)
            {
                transform.position = Vector2.MoveTowards(transform.position, player.position, moveSpeed * Time.deltaTime);
            }
        }
    }

    public bool TakeDamage(IAttackable attacker)
    {
        return true;
    }
}
