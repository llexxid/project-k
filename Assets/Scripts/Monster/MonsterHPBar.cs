using System;
using System.Collections;
using System.Collections.Generic;
using Scripts.Monster;
using UnityEngine;
using UnityEngine.UI;

public class MonsterHPBar : MonoBehaviour
{
    [SerializeField] private SpriteRenderer monsterRenderer;
    [SerializeField] private Monster monster;
    [SerializeField] private Slider slider;
    [SerializeField] private float gap = 0.15f;

    private void Awake()
    {
        if (slider == null)
        {
            slider = GetComponentInChildren<Slider>();
        }
        if (monster == null)
        {
            monster = gameObject.GetComponentInParent<Monster>();
            monster.OnHpChanged += UpdateHP;
        }

        if (monsterRenderer == null)
        {
            monsterRenderer = monster.GetComponentInChildren<SpriteRenderer>();
        }

        //몬스터나 렌더러가 없으면 hp바 사용할 수 없으니까 제거
        if (monster == null || monsterRenderer == null)
        {
            Debug.LogWarning("[MonsterHPBar] null Monster");
            Destroy(gameObject);
        }
    }

    private void LateUpdate()
    {
        if (monsterRenderer == null || monsterRenderer.sprite == null)
            return;

        Bounds bounds = monsterRenderer.bounds;

        transform.position = new Vector3(
            bounds.center.x,
            bounds.max.y + gap,
            transform.position.z
        );
        
        transform.rotation = Camera.main.transform.rotation;
    }

    private void OnDestroy()
    {
        monster.OnHpChanged -= UpdateHP;
    }

    private void UpdateHP(float ratio)
    {
        slider.value = ratio;
    }
}
