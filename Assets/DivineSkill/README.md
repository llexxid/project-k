# 신 스킬 모듈

공통 작업 규칙은 [AGENTS.md](../../AGENTS.md)를 따른다. 신 스킬은 파티 공용 1슬롯 궁극기이며 마탑의 지속 스킬과 별개다.

## 진입점

| 파일 | 역할 |
|---|---|
| `Scripts/DivineSkillManager.cs` | 보유·레벨·장착·쿨타임·시전·컬렉션 보너스 |
| `Scripts/DivineSkillSO.cs`, `DivineSkillRegistrySO.cs` | 카드 데이터·목록 |
| `Scripts/DivineSkillCode.cs` | 64비트 카드 코드 |
| `Scripts/DivineSkillCaster.cs` | 대상·데미지·회복·버프 |
| `Scripts/DivineBuffState.cs`, `MonsterCCState.cs` | 파티 버프·몬스터 제어 |
| `Editor` | 데이터·VFX·아트 배선·bootstrap 설치 도구 |

현재 장면의 매니저 설치와 시스템 해금 여부를 확인한다. UGUI의 `Hud_DivineSkill`, `Overlay_DivineCutIn`, `Popup_DivineCollection`에 연결된다. 도감 진입점은 메인 햄버거 메뉴다.
`KingdomIdle/Divine/Build All (cards + vfx + art + ui)`는 데이터·bootstrap·프리팹을 갱신하는 넓은 작업이므로 기존 상태를 확인하고 필요한 범위만 실행한다.

## 기존 수치 설계

`DivineValue = PartyStat × SkillMult × (1 + 0.1 × (Lv - 1)) × (1 + DivineBuff%)`

공격형 PartyStat은 생존 파티원 ATK 합, 회복형은 대상 MAXHP. 카드별 계수는 SO에 있다.
기존 레벨업 비용은 L→L+1에 중복 L장, 컬렉션은 카드별 ATK·HP +2%, 전종 +5% 추가였다. 변경 시 현재 코드와 서버 계약을 우선 확인한다.

효과 종류: 광역 즉발, 단일 최대 체력 대상, 지속 광역, 회복/피해감소, 파티 가속.
전투 접점: `Monster`, `Player`, `PlayerOrder`, `ActiveSkill`, `StatEnhanceManager`.

## 아트·검증

아트 출처와 실행 기록은 [AI/comfyui](../../AI/comfyui/README.md)에 있다. 기존 카드 이름·초기 구현 상태를 신규 디자인 확정본으로 사용하지 않는다.
최초 구현 기록은 Git에 보존되어 있다. 해금·보상·뽑기·서버 저장 지원 여부는 현재 코드와 실행 결과로 판단한다.
