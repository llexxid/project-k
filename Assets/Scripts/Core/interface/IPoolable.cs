namespace Scripts.Core.inteface
{
    public interface IPoolable
    {
        // NOTE: ObjectPool / Entity들에서 런타임에도 사용(중복 해제 방지 등)하므로 항상 포함.
        bool IsActive { get; set; }

        void OnAlloc();
        void OnRelease();
    }
}
