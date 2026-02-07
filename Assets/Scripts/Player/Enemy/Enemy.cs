using UnityEngine;

public class Enemy : MonoBehaviour
{
    public Enemy enemy;
    public int hp = 100;
    public Transform player; // 추적할 플레이어 타겟
    public float moveSpeed = 3f; // 적 이동 속도
    public float detectionRange = 10f; // 추적 시작 거리

    void Start()
    {
        enemy = GetComponent<Enemy>();
        player = GameObject.FindWithTag("Player").transform;
    }

    void Update()
    {
        // 플레이어가 할당되어 있고 일정 거리 안에 있을 때 추적 수행
        if (player != null)
        {
            float distance = Vector2.Distance(transform.position, player.position);
            if (distance <= detectionRange)
            {
                // 플레이어 방향으로 이동
                transform.position = Vector2.MoveTowards(transform.position, player.position, moveSpeed * Time.deltaTime);
            }
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // 플레이어와 충돌했는지 확인
        if (other.CompareTag("Player"))
        {
            hp -= 50; // HP 10 감소
            Debug.Log($"Enemy HP: {hp}");

            // HP가 0 이하가 되면 오브젝트 파괴
            if (hp <= 0)
            {
                Destroy(gameObject);
            }
        }
    }
}