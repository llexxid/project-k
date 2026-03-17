using System;

/// <summary>
/// 장비 착용 가능 직업 비트 플래그.
/// None(0)은 모든 직업 공용을 의미한다.
///
/// [명명 계약] 멤버 이름은 JobData.jobName 문자열과 정확히 일치해야 한다.
/// 새 직업이 추가되면 이 열거형에도 동시에 추가할 것.
/// </summary>
[Flags]
public enum eJobFlag
{
    None     = 0,
    Mage     = 1 << 0,   // 0000 0001
    Archer   = 1 << 1,   // 0000 0010
    Knight   = 1 << 2,   // 0000 0100
    Spearman = 1 << 3,   // 0000 1000
}
