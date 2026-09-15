# 게임용 아트 안내

## 빠른 찾기

| 용도 | 작업본 위치 |
|---|---|
| 왕국군 병과·왕족·색상 변형 | `Sprites/RoyalGuard/<병과>` |
| 밴딧 | `Sprites/Bandit/<캐릭터>` |
| 고블린·오크·미믹·드래곤 | `Sprites/Goblins`, `Orcs`, `Mimics`, `Dragons` |
| 현재 사용 중인 TinyRPG 몬스터 | `Sprites/TinyRPG` |
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
- `Environment/Legacy`: 기존 메인 씬에서 사용하는 배경.

## 준비본 사용

새 몬스터에는 기존 `Monster`, `MonsterHitFlash`, 충돌체, Animator와 `MonsterAnimationSO`가 연결되어 있습니다. 현재 스테이지나 Addressables 로스터에는 등록하지 않았습니다. 채택 시 기존 생성·풀링·능력치 초기화 절차를 사용해야 합니다.

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

각 프리팹은 Grid와 3개 Tilemap으로 구성됩니다. 중앙 `x=-2.75..2.75, y=-7..7`(보스는 폭 ±3)에는 장식물의 전체 사각 경계가 들어오지 않습니다. 배경은 `x=-16..16, y=-24..24`를 덮습니다. 장식은 전투 캐릭터 뒤에 표시되며 충돌체와 상시 Animator가 없습니다. 더 넓게 보이는 카메라는 후속 통합 시 범위를 재검증해야 합니다.

기존 Forest는 31 PPU를 유지합니다. 준비본 `Forest-Prairie_32PPU.png`는 같은 픽셀을 별도 임포트한 32 PPU 작업본입니다. 두 버전의 타일을 한 격자에서 혼용하지 마세요.

## 유지보수 참고

EliteArcher의 `AnimationSheets`와 `UiSprites`는 픽셀이 같아도 슬라이싱·피벗·PPU와 참조가 다릅니다. 용도가 다른 두 작업본입니다.

신규 준비 아틀라스는 Point, 회전 없음, 여백 4px, 최대 2048, 모바일 ASTC 4×4, mipmap/readable OFF입니다. 준비본은 `Include in Build`를 꺼 두었습니다. 채택할 콘텐츠의 아틀라스를 기존 빌드/Addressables 정책에 맞게 포함해야 합니다. 색상별 드래곤은 별도 아틀라스로 나눴습니다.

현재 캐릭터 아틀라스는 이동한 원본 GUID를 직접 참조합니다. **기존 `OptAtlases` / `OptTextureImport` 에디터 도구의 문자열 경로는 아직 예전 위치를 사용합니다.** 스크립트 수정 제외 요청에 따라 그대로 두었으므로, 해당 도구를 다시 실행하기 전에 [후속 연결 안내](../../../Docs/ArtPreparation/INTEGRATION.md)의 경로를 갱신해야 합니다.

[상세 카탈로그·검증 기록](../../../Docs/ArtPreparation/README.md)
