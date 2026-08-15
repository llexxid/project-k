# 신 스킬 (Divine Skill) 시스템

기획서 v2.1 3.4 의 **초기 축소판**. 카드 수집 + 파티 공용 1슬롯 궁극기.
아트/UI 는 아직 붙어 있지 않으며, 이 모듈만으로 **수치·규칙·전투 적용**이 전부 동작한다.

## 1. 구성

```
Assets/DivineSkill/
├── Scripts/
│   ├── DivineSkillEnums.cs      등급 / 효과 종류 / 군중 제어 enum
│   ├── DivineSkillSO.cs         카드 1장 데이터 (ScriptableObject)
│   ├── DivineSkillRegistrySO.cs 카드 전체 목록
│   ├── DivineSkillCode.cs       64비트 카드 코드 (서버 전송용, 마탑과 동일 규약)
│   ├── DivineSkillManager.cs    ★ 단일 진입점 — 보유/레벨/장착/쿨타임/시전/컬렉션 보너스
│   ├── DivineSkillCaster.cs     전장 실행기 (대상 탐색·데미지·회복·버프)
│   ├── DivineBuffState.cs       파티 일시 버프 전역 상태 (피해감소/가속)
│   └── MonsterCCState.cs        몬스터 군중 제어(기절·둔화) 런타임 컴포넌트
├── Editor/DivineSkillAssetGen.cs  카드 8종 + 레지스트리 생성, bootstrap 설치, 디버그 메뉴
└── SO/                            생성된 카드 에셋
```

마탑 스킬(`Assets/MageTower`)과 **완전히 별개 시스템**이다.
마탑 = 최대 5슬롯 상시 회전 지속딜 / 신 스킬 = 1슬롯 긴 쿨타임 궁극기.

## 2. 최초 셋업 (Unity 에디터)

1. `KingdomIdle → Divine → Build All (cards + vfx + art + ui)` — 카드 SO·VFX·아트 배선·
   **bootstrap 매니저 설치**·UGUI 프리팹·배선 검증까지 전부 한 번에 (멱등)
2. 플레이 모드에서 `KingdomIdle → Divine → Debug → Unlock System + Grant All Cards` 로 테스트

> 개별 메뉴(`Generate Cards + Registry`, `Generate Astra VFX`, `Install Manager Into Bootstrap`,
> `Wire Generated Art`)도 있지만, 순서 의존성이 있으므로 가급적 Build All 을 쓴다.
> 매니저 설치가 빠지면 UI 만 살아 있고 기능 전체가 죽은 코드가 된다 — Build All 이 항상 함께 설치한다.

## 3. 수치 공식 (기획서 3.4.3)

```
DivineValue = PartyStat × SkillMult × (1 + 0.1 × (Lv - 1)) × (1 + DivineBuff%)
```

- `PartyStat` — 공격형: 살아있는 파티원 최종 ATK 합 / 회복형: 대상 MAXHP
- `SkillMult` — 카드 SO 의 `skillMult` (등급 계수가 이미 반영된 값)
- 레벨 — 중복 카드로 상승. `L → L+1` 에 중복 `L`장 필요 (유물과 동일 곡선, 상한 없음)
- `DivineBuff%` — `DivineSkillManager.DivineBuffPercent` (박사 과정·여신의 가호 등 외부 버프 합류점)

컬렉션 보너스: 카드 1종 최초 획득당 파티 공격력·체력 **+2%**, 전종 수집 시 **+5%** 추가.
`StatEnhanceManager.ApplyToAllPlayers()` 에서 강화 보너스와 같은 가산 그룹으로 합류한다.

## 4. 효과 종류

| `eDivineEffectKind` | 동작 | 초기 카드 |
|---|---|---|
| `AoeBurst` | 화면 전체 즉발 데미지 (+ 선택적 CC / 시전 지연) | 루멘, 호라, 아스트라, 녹스 |
| `SingleBurst` | 단일 대상 — 화면 내 최대 체력(=보스) 우선 | 페룸 |
| `Dot` | `duration` 동안 `hitCount` 회 광역 데미지 | 이그니스 |
| `HealAndGuard` | 파티 MAXHP 비율 회복 + 받는 피해 감소 | 가이엔 |
| `PartyHaste` | 기본 스킬 간격 단축 + 이동속도 증가 | 실피르 |

## 5. 다른 시스템에 낸 접점 (모두 가산·비파괴 수정)

| 파일 | 변경 |
|---|---|
| `Monster.cs` | `SpeedMultiplier`(둔화용) · `MaxHp`(보스 우선 타게팅용), `OnAlloc` 에서 배율 초기화 |
| `Player.cs` | `TakeDamage` 가 `DivineBuffState.ApplyDamageReduction` 을 거침 |
| `PlayerOrder.cs` | `SyncMoveSpeed` 가 가속 버프 배율을 곱함 |
| `ActiveSkill.cs` | `ScaledCooldown()` 추가 — 기본공격 3종 + 에너지 파동의 쿨타임에 가속 반영 |
| `StatEnhanceManager.cs` | `ApplyToAllPlayers` 에 컬렉션 보너스 가산 |

## 6. 완성된 것 / 아직 없는 것

**완성 (Astra 수직 슬라이스, 2026-08-15)**
- **HUD** — 우하단 궁극기 버튼 (`Hud_DivineSkill`): 방사형 쿨다운·준비 후광 맥동·탭 시전.
  자체 Canvas 로 리빌드 격리, 쿨다운 표기는 0.1초 단위로만 갱신
- **컷인** — `Overlay_DivineCutIn`: 스크림 → 일러스트 슬라이드 → 등급/이름 → 플래시 피크에서 발동 (~1.2초, 스킵형 아님)
- **VFX** — Astra 3종 (전장 버스트 / 대상별 임팩트 / 기절 상태 루프) + `DivineVfxInstance` 프리팹별 풀링
- **아트** — Astra 일러스트·아이콘 (ComfyUI NB2, `AI/comfyui/style_spec_character.md` 절차). 나머지 7종은 동일 절차로 생성
- **해금 트리거** — 메인 3-10 클리어 → 해금 + Astra 확정 지급 (`OnStageCleared` 배선)
- **경제 연결** — 신 스킬/마탑 처치가 골드·경험치·드롭을 정상 지급 (`IRewardable` 경로)

**아직 없는 것**
- **컬렉션북 UI** — 카드 목록/장착/레벨업 패널 (API 는 전부 준비됨: `GetAllCards`/`Equip`/`TryLevelUp`/`CollectionBonusRate`)
- **가챠 연동** — `Acquire(cardId)` API 만 제공. 서버 뽑기 라우팅 미연결
- 녹스의 '처치 골드 +100%', 컷인 중 전투 일시 정지(기획 3.4.1) 및 연출 스킵 옵션, 환생 계승 처리
- 나머지 7종 카드의 아트/전용 VFX (수치는 전부 동작 — 연출만 생략됨)
