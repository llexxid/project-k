# 스테이지 카탈로그와 인게임 연결

## 편집·생성

`Assets/_Project/Scripts/Stage_Catalog.xlsx`를 편집하고 Unity의 **MyTools/Stage/Generate Stage Data**를 실행합니다. 생성 결과는 `Assets/_Project/Resources/StageDatabaseSO.asset`입니다. 파일 이동 시 기존 `.meta` GUID를 유지했습니다.

1행의 열 이름, 이미 발급된 StageId·MonsterId는 유지합니다. 수치나 프리팹 경로를 수정하면 생성 메뉴를 다시 실행합니다. 생성기는 필수 값, 수량·능력치, 프리팹·애니메이터, 배경 풀·가중치와 ID 중복을 검사합니다. 카탈로그 SHA-256을 SO에 저장해 빌드·서버 데이터의 출처를 대조합니다.

| 시트 | 역할 |
|---|---|
| StageDefinitions / StageMonsters | 43개 스테이지와 몬스터 구성 |
| MainBalance / GoldBalance / RubyBalance | 전투 수치·시간·처치 보상 |
| RubyRewards / FirstClearRewards | 반복 클리어와 계정 최초 보상 |
| MonsterCatalog | 41종 고정 ID·역할·크기·프리팹 경로 |
| EnvironmentPresets | 90개 환경과 선택 가중치 |
| Inputs / Growth / Income / Guide | 공통 입력, 성장·수입 검토, 편집 안내 |

`StageCatalogRules`는 같은 SO를 전투·던전 보상 표시·오프라인 보상·퀘스트 수입 계산에서 재사용합니다. Growth 표 자체가 강화 코드를 생성하지는 않습니다. 기존 저장 데이터와 최초 보상 원장 키는 유지합니다. 향후 서버에서도 카탈로그 해시·스테이지 ID·전투 ID를 함께 사용하도록 구성했습니다.

## 적용 로스터

| 콘텐츠 | 일반 몬스터 | 보스 |
|---|---|---|
| 1스테이지 | 산적 전사·방패병·궁수 | 산적 왕 |
| 2스테이지 | 고블린·암살자·폭탄병·주술사 | 고블린 왕 |
| 3스테이지 | 오크 전사·감독관·사냥꾼·주술사 | 오크 거한 |
| 골드 던전 | Mimic_Animation_Pack의 HeresyMimic 20체 | — |
| 루비 던전 | — | 산적 왕 → 고블린 왕 → 오크 거한 |

활성 15종은 `Stage Monsters` Addressables 그룹에서 개별 번들로 관리합니다. 나머지는 다른 콘텐츠용으로 유지합니다. 기존 `Monster.OnAlloc`·풀링·상태·피해 경로를 사용하고 스폰 직후 카탈로그 수치를 적용합니다. HP바와 피해 숫자는 무기·투명 시트 여백 대신 프리팹의 실측 몸체 높이를 사용합니다. [크기 검수](MONSTER_SIZES.md)의 배율을 유지합니다.

## 환경 선택

`StageBackgroundController`는 풀의 가중치로 하나를 선택하며 같은 프리셋의 즉시 반복을 피합니다. 로드가 완료된 후 전투와 페이드인을 시작합니다. 이전 인스턴스·Addressables 핸들을 해제하고 늦게 끝난 이전 요청도 버립니다.

- 1스테이지는 흙길·흙 공터, 2스테이지는 밝은 풀길, 3스테이지는 어두운 풀길과 촘촘한 수관·관목입니다.
- 골드 던전은 Dungeon, 루비 던전은 Wasteland입니다. 각 타일셋 Main 10, Boss 3, Special 5개입니다.
- Ground·GroundDetails·Scenery 타일맵 3개, 32 PPU, 전체 범위 32×48 world units입니다. 기본 배경에는 상시 Animator가 없습니다.
- 중앙 장식 제외 범위는 일반/특수 x ±2.1875, 보스 x ±2.625, y ±7입니다. 가장자리 수목을 휴대폰 화면 안쪽으로 배치했습니다.
- 메인·던전 카메라의 크기를 일치시키고 `CombatViewport`가 화면 비율에 맞춰 스폰·왕국군 대열을 배치합니다. 몬스터 배율은 배경에 따라 바뀌지 않습니다.
- `Stage Environments` 그룹은 90개 프리팹의 공유 타일 의존성을 함께 패킹합니다. 추가 Ambient 효과는 필요할 때 선택해 연결합니다.

## 검증·후속 범위

[스테이지 통합 검증](STAGE_INTEGRATION_VALIDATION.md)에서 실제 기기 결과를 확인합니다. [초기 준비 검증](VALIDATION.md)은 통합 전 기록이며 당시의 미연결·빌드 제한을 설명합니다.

추가 몬스터 공격·토템·전용 투사체 행동은 별도 콘텐츠가 채택될 때 연결합니다. 모든 준비 몬스터를 메인에 넣지는 않았습니다. 원본 결손은 [몬스터 카탈로그](MONSTERS.md)에 기록했습니다. 외부 보관소 원본은 게임의 직접 참조 대상에서 제외되어 있습니다.
