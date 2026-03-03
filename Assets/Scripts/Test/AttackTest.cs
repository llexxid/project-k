using Scripts.Core.inteface;
using Scripts.Monster;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttackTest : MonoBehaviour
{
    [SerializeField]
    Monster monster;

	DummyPlayer player;
	class DummyPlayer : IAttackable
	{
		public ulong damage
		{
			get { return 10; }
		}

		public Vector3 attackerPos
		{
			get { return Vector3.zero; }
		}

		public bool Attack(IDamageable target)
		{
			return true;
		}
	}
	// Start is called before the first frame update
	void Start()
    {
		player = new DummyPlayer();

	}

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            monster.TakeDamage(player);
        }
    }
}
