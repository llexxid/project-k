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
            slider = GetComponentInChildren<Slider>();
        if (monster == null)
            monster = GetComponentInParent<Monster>();

        if (monsterRenderer == null && monster != null)
            monsterRenderer = monster.GetComponentInChildren<SpriteRenderer>();

        if (monster == null || monsterRenderer == null || slider == null)
        {
            Debug.LogWarning("[MonsterHPBar] 필수 참조가 없어 HP바를 제거합니다.", this);
            Destroy(gameObject);
            return;
        }

        monster.OnHpChanged += UpdateHP;
        UpdateHP(monster.GetHpRatio());
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
        
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
            transform.rotation = mainCamera.transform.rotation;
    }

    private void OnDestroy()
    {
        if (monster != null)
            monster.OnHpChanged -= UpdateHP;
    }

    private void UpdateHP(float ratio)
    {
        if (slider != null)
            slider.value = Mathf.Clamp01(ratio);
    }
}
