using UnityEngine;

namespace KingdomIdle.MageTower
{
    /// <summary>
    /// 마탑 스킬 공용 '화면 안' 판정.
    ///
    /// 배경: SearchMonstersOnScreen 의 OverlapCircle 반경은 화면 사각형의 **외접원**(중심~모서리
    /// + 2 유닛)이라, 화면 밖 상하좌우 띠에 있는 몬스터도 후보에 들어온다 — 마탑 스킬이
    /// 화면 밖 몬스터를 잡던 원인. 물리 쿼리는 저렴한 광역 필터로 그대로 두고,
    /// 후보 확정 단계에서 이 뷰포트 판정(PlayerDetection.IsInCameraBounds 와 동일 의미론)으로
    /// 걸러낸다. 시전 조건·체인 홉·생성 위치·지속 추적·피해 판정 전 단계가 이걸 쓴다.
    /// </summary>
    public static class MageTowerTargeting
    {
        // 화면 가장자리 살짝 안쪽까지만 유효 타깃 (PlayerDetection.CameraBoundsInset 과 동일 값)
        private const float ViewportInset = 0.02f;

        /// <summary>
        /// 판정에 쓸 카메라. 루프 밖에서 한 번 받아 후보마다 재사용할 것
        /// (Camera.main 은 내부 캐시가 있지만 호출당 널체크 비용은 있다).
        /// </summary>
        public static Camera ResolveCamera() => Camera.main;

        /// <summary>
        /// 월드 좌표가 카메라 뷰포트 안(약간의 인셋 포함)인지.
        /// 카메라가 없으면 false — 마탑 스킬은 '보이는 대상에만 발동'이 계약이므로
        /// 판정 불가 상황에서는 시전하지 않는 쪽으로 눕힌다.
        /// </summary>
        public static bool IsOnScreen(Camera cam, Vector3 worldPos)
        {
            if (cam == null) return false;
            Vector3 vp = cam.WorldToViewportPoint(worldPos);
            if (vp.z < 0f) return false;
            return vp.x >= ViewportInset && vp.x <= 1f - ViewportInset
                && vp.y >= ViewportInset && vp.y <= 1f - ViewportInset;
        }
    }
}
