# 게임용 아트 안내

## 빠른 찾기

| 용도 | 작업본 위치 |
|---|---|
| 왕국군 병과·왕족·색상 변형 | `Sprites/RoyalGuard/<병과>` |
| 밴딧 | `Sprites/Bandit/<캐릭터>` |
| 고블린·오크·미믹·드래곤 | `Sprites/Goblins`, `Orcs`, `Mimics`, `Dragons` |
| 이전 스테이지용 TinyRPG 보존본 | `Sprites/TinyRPG` |
| 장비 아이콘 시트 | `Sprites/Equipment/SourceSheets` |
| 애니메이션·컨트롤러 | `Animations/<분류>/<캐릭터>` |
| 몬스터 행동별 애니메이션 길이 데이터 | `AnimationData/<분류>` |
| 타일·환경 오브젝트 | `Environment/<Forest 또는 Dungeon 또는 Wasteland>` |
| 아틀라스 | `Atlases`, 신규 준비본은 `Atlases/Prepared` |
| 몬스터 기본 피격 머티리얼 | `Materials/Monsters/MonsterHitFlash.mat` |
| 공급자의 가이드·라이선스 | `SourceNotes` |

프리팹은 `Assets/_Project/Prefabs` 아래에 있습니다.

- `Monster/{Bandit,TinyRPG,Goblins,Orcs,Mimics,Dragons}`: 몬스터 본체.
- `RoyalGuard`: 기존 병사 프리팹.
- `VFX/Prepared/<분류>/<캐릭터>`: 투사체·폭발·토템 등 시각 프리팹.
- `Environment/Prepared/<환경>/{Main,Boss,Special}`: 사전 배치한 환경 풀.
- `Environment/Prepared/Environment/Wasteland/Ambient`: 선택적으로 사용하는 환경 애니메이션.
- `Environment/Legacy`: 이전 배경 보존본. 새 배경을 로드하면 비활성화됩니다.

## 준비본 사용

몬스터에는 기존 `Monster`, `MonsterHitFlash`, 충돌체, Animator와 `MonsterAnimationSO`가 연결되어 있습니다. 카탈로그의 활성 15종은 `Stage Monsters` Addressables 그룹에서 기존 생성·풀링 절차로 사용합니다. 나머지는 후속 콘텐츠용 준비본입니다. [카탈로그 연결 안내](../../../Docs/ArtPreparation/INTEGRATION.md)를 확인하세요.

Animator의 `Idle`, `Walk`, `Attack`, `Hurt`는 bool, `Dead`는 trigger입니다. 사망 상태에서는 피격 상태로 되돌아가지 않습니다. 추가 공격·변신·작업 동작은 같은 캐릭터의 `Prepared` 클립 폴더에서 선택할 수 있습니다.

투사체·토템·함정·환경 애니메이션 프리팹은 **시각 요소**입니다. 비행, 피해, 소환, 풀 반환 처리는 포함하지 않습니다. 단발 효과는 마지막에 숨기며, `Net_Deploy`는 펼친 그물 자세를 유지합니다.

RoyalGuard와 Bandit은 기존 본체 프리팹을 재사용하도록 유지했습니다. 새 색상·왕족·추가 직업 시트의 분할 스프라이트와 모든 준비 클립은 해당 병과 폴더에 있습니다. 새 직업·능력치·스킬 연결은 후속 적용 범위입니다.

몬스터 41종(신규 37 + 기존 Bandit 4)의 표시 크기는 [크기 검수 기록](../../../Docs/ArtPreparation/MONSTER_SIZES.md)에 정리했습니다. 프리팹의 실제 배율과 현재 왕국군 병과를 반영한 비교 이미지, 몸체 측정값, 보스·탑승물 예외를 함께 확인할 수 있습니다.

## 환경 프리셋

5개 풀 각각 일반 10개, 보스 3개, 특수 5개가 있습니다.

| 풀 | 의도 |
|---|---|
| `Stage01_ForestDirt` | 정비된 흙길, 길 끝의 흙 공터 |
| `Stage02_ForestGrass` | 밝은 풀길과 풀 공터 |
| `Stage03_DeepForest` | 2스테이지보다 촘촘한 큰 수관·하층 관목, 짙은 주변 녹색과 밝은 중앙 통로 |
| `GoldDungeon` | Dungeon 석재 바닥·기둥·유적 |
| `RubyWasteland` | Wasteland 메마른 땅·뼈·고목 |

각 프리팹은 Grid와 3개 Tilemap으로 구성됩니다. 중앙 `x=-2.1875..2.1875, y=-7..7`(보스는 폭 ±2.625)에는 장식물의 전체 사각 경계가 들어오지 않습니다. 배경은 `x=-16..16, y=-24..24`를 덮습니다. 장식은 전투 캐릭터 뒤에 표시되며 충돌체와 상시 Animator가 없습니다. `StageBackgroundController`가 카탈로그에서 풀과 가중치를 읽어 선택합니다.

기존 Forest는 31 PPU를 유지합니다. 준비본 `Forest-Prairie_32PPU.png`는 같은 픽셀을 별도 임포트한 32 PPU 작업본입니다. 두 버전의 타일을 한 격자에서 혼용하지 마세요.

## 유지보수 참고

EliteArcher의 `AnimationSheets`와 `UiSprites`는 픽셀이 같아도 슬라이싱·피벗·PPU와 참조가 다릅니다. 용도가 다른 두 작업본입니다.

준비 아틀라스는 Point, 회전 없음, 여백 4px, 최대 2048, 모바일 ASTC 4×4, mipmap/readable OFF입니다. `OptAtlases`는 Unity 6의 SpriteAtlasImporter API로 활성 스테이지·환경 의존성만 빌드에 포함합니다. 전체 56개 중 19개 포함, 예비 몬스터·중복 RoyalGuard 등 37개 제외입니다. 색상별 드래곤은 별도 아틀라스로 유지합니다.

캐릭터 아틀라스는 이동한 작업본 GUID를 참조합니다. `OptAtlases`와 `OptTextureImport`는 작업본 경로와 마탑 등록 데이터의 의존성을 사용합니다. 활성 마탑 VFX·뽑기 시트는 Android RGBA32 원본을 아틀라스에서 한 번만 압축하여 중복 압축 손상을 방지합니다. `Atlas_MageVFX`는 Point/ASTC 4×4, `Atlas_UIPixel`은 48px 스킬 아이콘, `Atlas_UI`는 스무스 UI용입니다.

마탑 10종 아이콘은 `Icons/MageTower`, 전투 프리팹 19개는 `../Prefabs/VFX/MageTower`, 클립·컨트롤러는 `Animations/VFX/MageTower`에 있습니다. 과거 테스트 에셋은 `../Tests/Fixtures/MageTowerLegacy`로 분류했습니다. [마탑 모듈 안내](../../MageTower/README.md)를 참고하세요.

[상세 카탈로그·검증 기록](../../../Docs/ArtPreparation/README.md)
