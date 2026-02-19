using UnityEngine;

namespace Scripts.Core.inteface
{
    public interface IPoolable
    {
        // NOTE: ObjectPool / Entity들에서 런타임에도 사용(중복 해제 방지 등)하므로 항상 포함.
        bool IsActive { get; set; }

        void OnAlloc();
        void OnRelease();
    }

    /// <summary>
    /// 공격을 할 수 있는 개체
    /// </summary>
    public interface IAttackable
    {
        public int damage { get;}
        public Vector3 attackerPos { get; }
    }
    /// <summary>
    /// 데미지를 입을 수 있는 개체(피격이 가능한 개체)
    /// </summary>
    public interface IDamageable
    {
        public void TakeDamage(IAttackable attacker);
    }
}
