using UnityEngine;

/// <summary>
/// 프로젝트 전체에서 사용하는 레이어 번호/마스크 상수 모음.
/// Physics 관련 레이어를 여러 파일에서 하드코딩하지 않고 이 파일 하나에서 관리한다.
/// </summary>
public static class GameLayers
{
    /// <summary>적(Enemy) 레이어 번호 (Unity Inspector Layer 6번)</summary>
    public const int Enemy = 6;

    /// <summary>Physics 탐지에 사용하는 LayerMask (Enemy 레이어만 포함)</summary>
    public static readonly LayerMask EnemyMask = 1 << Enemy;
}
