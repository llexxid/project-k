# 환경 프리셋 목록

2026-10-01 두 번째 개정. 물을 모두 제거하고, 흙길·풀길에서 넓어지는 공터와 다양한 외곽 나무로 90개 배경을 조성한다. 던전의 독립된 문·매달린 천은 제거하고 기둥을 절제된 짝 또는 단독 배치로 구성한다. 기존 프리팹 ID, Addressables 연결, 풀 선택 방식, Ground / GroundDetails / Scenery 3개 타일맵을 유지한다.

[번호별 실제 Unity 미리보기](Validation/EnvironmentPolish20261001/revision2/after/index.html) · [이번 수정 전 미리보기](Validation/EnvironmentPolish20261001/after/index.html) · [이번 검증 기록](Validation/EnvironmentPolish20261001/revision2/README.md) · [첫 번째 개정 기록](Validation/EnvironmentPolish20261001/README.md)

번호는 피드백용 고정 식별자다. 실제 전투에서는 해당 지역 풀에서 직전 배경을 제외하고 선택한다. 일반·보스·특수는 프리셋의 배치 유형이며 특정 웨이브 번호에 고정 대응하지 않는다.

- 01–18 숲길, 19–36 초원 숲, 37–54 깊은 숲, 55–72 골드 던전, 73–90 루비 황무지. 각 그룹은 Main 10 / Boss 3 / Special 5 순이다.
- 전투 보호 영역은 일반·특수 x ±3.1875, 보스형 x ±3.25, y -3.125~4.5다. 실제 생성 범위 ±2.5와 활성 몬스터 몸체 폭, 4픽셀 여백으로 산출했다. 소품의 전체 bounds가 영역과 겹치지 않도록 한다. 상하 모서리는 전체 소품이 보호 영역 밖일 때만 안쪽 배치를 허용한다.
- 숲의 물 타일 사용은 생성 경로에서 제거하고 검증에서 금지한다. 흙길 일부와 모든 풀길은 4~5타일 진입로가 7~8타일 공터로 열리며, 원본 타일의 볼록·오목 경계로 연결한다. 공터 외곽에는 소나무·자작나무·둥근나무·버드나무 등을 섞는다.
- 던전 `Crates`는 실제 양문형 나무문, `Vines`는 매다는 천임을 원본에서 확인했다. 독립 배치를 금지하고 소품 이름과 실제 원본 사각형을 함께 검사한다. 기둥은 전체 프리셋당 0~2개이며, 2개는 같은 높이의 널찍한 양쪽 짝이다. 낮은 벤치는 상하 모서리에 놓는다.
- 던전의 회색 석재 바닥과 황무지의 조용한 모래 중앙·외곽 균열을 유지한다. 황무지 장식에도 넓어진 전투 보호 영역을 적용했다.
- 황무지 고목·마른 풀·두개골 4종의 잘못된 스프라이트 범위를 수정했다. 작은 소품 28종은 완전 투명 여백만 정리해 외곽 가시성을 개선했다. 구매 원본 PNG와 ExternalAssets는 변경하지 않았다.
- 추가 런타임 장식 오브젝트·충돌체·텍스처는 없다. 세 타일맵으로 배치하며 새 아이콘도 기존 아틀라스 경로를 쓴다.

재생성: `EnvironmentRichnessPreparation.Prepare`. 검증과 135장 렌더를 함께 실행하려면 `EnvironmentPolishValidation.PrepareAndCaptureAfter`를 사용한다. 실제 플레이 모드 검사 진입점은 `EnvironmentGameplayValidation.Run`이다. 기준 배치·타일 수·시드는 [manifest](Manifests/environment-presets.json)에 기록한다.

이전 2026-09-20 구성과 기기 b52 캡처는 [기존 갤러리](Previews/foundation-environments-b52.png) 및 과거 검증 기록에 보존한다.

| 번호 | 풀 | 유형 | 구도 | 프리팹 |
|---|---|---|---|---|
| 01 | Stage01_ForestDirt | Main | QuietGrove | [Stage01_ForestDirt_Main_01](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage01_ForestDirt/Main/Stage01_ForestDirt_Main_01.prefab>) |
| 02 | Stage01_ForestDirt | Main | TimberVerge | [Stage01_ForestDirt_Main_02](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage01_ForestDirt/Main/Stage01_ForestDirt_Main_02.prefab>) |
| 03 | Stage01_ForestDirt | Main | MossyStones | [Stage01_ForestDirt_Main_03](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage01_ForestDirt/Main/Stage01_ForestDirt_Main_03.prefab>) |
| 04 | Stage01_ForestDirt | Main | BirchClearing | [Stage01_ForestDirt_Main_04](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage01_ForestDirt/Main/Stage01_ForestDirt_Main_04.prefab>) |
| 05 | Stage01_ForestDirt | Main | LowStoneGarden | [Stage01_ForestDirt_Main_05](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage01_ForestDirt/Main/Stage01_ForestDirt_Main_05.prefab>) |
| 06 | Stage01_ForestDirt | Main | MushroomMargin | [Stage01_ForestDirt_Main_06](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage01_ForestDirt/Main/Stage01_ForestDirt_Main_06.prefab>) |
| 07 | Stage01_ForestDirt | Main | OldWall | [Stage01_ForestDirt_Main_07](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage01_ForestDirt/Main/Stage01_ForestDirt_Main_07.prefab>) |
| 08 | Stage01_ForestDirt | Main | TreeShade | [Stage01_ForestDirt_Main_08](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage01_ForestDirt/Main/Stage01_ForestDirt_Main_08.prefab>) |
| 09 | Stage01_ForestDirt | Main | MushroomGrove | [Stage01_ForestDirt_Main_09](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage01_ForestDirt/Main/Stage01_ForestDirt_Main_09.prefab>) |
| 10 | Stage01_ForestDirt | Main | QuietBank | [Stage01_ForestDirt_Main_10](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage01_ForestDirt/Main/Stage01_ForestDirt_Main_10.prefab>) |
| 11 | Stage01_ForestDirt | Boss | QuietGrove | [Stage01_ForestDirt_Boss_01](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage01_ForestDirt/Boss/Stage01_ForestDirt_Boss_01.prefab>) |
| 12 | Stage01_ForestDirt | Boss | TimberVerge | [Stage01_ForestDirt_Boss_02](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage01_ForestDirt/Boss/Stage01_ForestDirt_Boss_02.prefab>) |
| 13 | Stage01_ForestDirt | Boss | MossyStones | [Stage01_ForestDirt_Boss_03](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage01_ForestDirt/Boss/Stage01_ForestDirt_Boss_03.prefab>) |
| 14 | Stage01_ForestDirt | Special | BirchClearing | [Stage01_ForestDirt_Special_01](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage01_ForestDirt/Special/Stage01_ForestDirt_Special_01.prefab>) |
| 15 | Stage01_ForestDirt | Special | LowStoneGarden | [Stage01_ForestDirt_Special_02](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage01_ForestDirt/Special/Stage01_ForestDirt_Special_02.prefab>) |
| 16 | Stage01_ForestDirt | Special | MushroomMargin | [Stage01_ForestDirt_Special_03](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage01_ForestDirt/Special/Stage01_ForestDirt_Special_03.prefab>) |
| 17 | Stage01_ForestDirt | Special | OldWall | [Stage01_ForestDirt_Special_04](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage01_ForestDirt/Special/Stage01_ForestDirt_Special_04.prefab>) |
| 18 | Stage01_ForestDirt | Special | TreeShade | [Stage01_ForestDirt_Special_05](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage01_ForestDirt/Special/Stage01_ForestDirt_Special_05.prefab>) |
| 19 | Stage02_ForestGrass | Main | QuietGrove | [Stage02_ForestGrass_Main_01](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage02_ForestGrass/Main/Stage02_ForestGrass_Main_01.prefab>) |
| 20 | Stage02_ForestGrass | Main | TimberVerge | [Stage02_ForestGrass_Main_02](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage02_ForestGrass/Main/Stage02_ForestGrass_Main_02.prefab>) |
| 21 | Stage02_ForestGrass | Main | MossyStones | [Stage02_ForestGrass_Main_03](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage02_ForestGrass/Main/Stage02_ForestGrass_Main_03.prefab>) |
| 22 | Stage02_ForestGrass | Main | BirchClearing | [Stage02_ForestGrass_Main_04](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage02_ForestGrass/Main/Stage02_ForestGrass_Main_04.prefab>) |
| 23 | Stage02_ForestGrass | Main | LowStoneGarden | [Stage02_ForestGrass_Main_05](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage02_ForestGrass/Main/Stage02_ForestGrass_Main_05.prefab>) |
| 24 | Stage02_ForestGrass | Main | MushroomMargin | [Stage02_ForestGrass_Main_06](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage02_ForestGrass/Main/Stage02_ForestGrass_Main_06.prefab>) |
| 25 | Stage02_ForestGrass | Main | OldWall | [Stage02_ForestGrass_Main_07](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage02_ForestGrass/Main/Stage02_ForestGrass_Main_07.prefab>) |
| 26 | Stage02_ForestGrass | Main | TreeShade | [Stage02_ForestGrass_Main_08](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage02_ForestGrass/Main/Stage02_ForestGrass_Main_08.prefab>) |
| 27 | Stage02_ForestGrass | Main | MushroomGrove | [Stage02_ForestGrass_Main_09](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage02_ForestGrass/Main/Stage02_ForestGrass_Main_09.prefab>) |
| 28 | Stage02_ForestGrass | Main | QuietBank | [Stage02_ForestGrass_Main_10](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage02_ForestGrass/Main/Stage02_ForestGrass_Main_10.prefab>) |
| 29 | Stage02_ForestGrass | Boss | QuietGrove | [Stage02_ForestGrass_Boss_01](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage02_ForestGrass/Boss/Stage02_ForestGrass_Boss_01.prefab>) |
| 30 | Stage02_ForestGrass | Boss | TimberVerge | [Stage02_ForestGrass_Boss_02](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage02_ForestGrass/Boss/Stage02_ForestGrass_Boss_02.prefab>) |
| 31 | Stage02_ForestGrass | Boss | MossyStones | [Stage02_ForestGrass_Boss_03](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage02_ForestGrass/Boss/Stage02_ForestGrass_Boss_03.prefab>) |
| 32 | Stage02_ForestGrass | Special | BirchClearing | [Stage02_ForestGrass_Special_01](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage02_ForestGrass/Special/Stage02_ForestGrass_Special_01.prefab>) |
| 33 | Stage02_ForestGrass | Special | LowStoneGarden | [Stage02_ForestGrass_Special_02](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage02_ForestGrass/Special/Stage02_ForestGrass_Special_02.prefab>) |
| 34 | Stage02_ForestGrass | Special | MushroomMargin | [Stage02_ForestGrass_Special_03](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage02_ForestGrass/Special/Stage02_ForestGrass_Special_03.prefab>) |
| 35 | Stage02_ForestGrass | Special | OldWall | [Stage02_ForestGrass_Special_04](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage02_ForestGrass/Special/Stage02_ForestGrass_Special_04.prefab>) |
| 36 | Stage02_ForestGrass | Special | TreeShade | [Stage02_ForestGrass_Special_05](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage02_ForestGrass/Special/Stage02_ForestGrass_Special_05.prefab>) |
| 37 | Stage03_DeepForest | Main | QuietGrove | [Stage03_DeepForest_Main_01](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage03_DeepForest/Main/Stage03_DeepForest_Main_01.prefab>) |
| 38 | Stage03_DeepForest | Main | TimberVerge | [Stage03_DeepForest_Main_02](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage03_DeepForest/Main/Stage03_DeepForest_Main_02.prefab>) |
| 39 | Stage03_DeepForest | Main | MossyStones | [Stage03_DeepForest_Main_03](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage03_DeepForest/Main/Stage03_DeepForest_Main_03.prefab>) |
| 40 | Stage03_DeepForest | Main | BirchClearing | [Stage03_DeepForest_Main_04](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage03_DeepForest/Main/Stage03_DeepForest_Main_04.prefab>) |
| 41 | Stage03_DeepForest | Main | LowStoneGarden | [Stage03_DeepForest_Main_05](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage03_DeepForest/Main/Stage03_DeepForest_Main_05.prefab>) |
| 42 | Stage03_DeepForest | Main | MushroomMargin | [Stage03_DeepForest_Main_06](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage03_DeepForest/Main/Stage03_DeepForest_Main_06.prefab>) |
| 43 | Stage03_DeepForest | Main | OldWall | [Stage03_DeepForest_Main_07](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage03_DeepForest/Main/Stage03_DeepForest_Main_07.prefab>) |
| 44 | Stage03_DeepForest | Main | TreeShade | [Stage03_DeepForest_Main_08](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage03_DeepForest/Main/Stage03_DeepForest_Main_08.prefab>) |
| 45 | Stage03_DeepForest | Main | MushroomGrove | [Stage03_DeepForest_Main_09](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage03_DeepForest/Main/Stage03_DeepForest_Main_09.prefab>) |
| 46 | Stage03_DeepForest | Main | QuietBank | [Stage03_DeepForest_Main_10](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage03_DeepForest/Main/Stage03_DeepForest_Main_10.prefab>) |
| 47 | Stage03_DeepForest | Boss | QuietGrove | [Stage03_DeepForest_Boss_01](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage03_DeepForest/Boss/Stage03_DeepForest_Boss_01.prefab>) |
| 48 | Stage03_DeepForest | Boss | TimberVerge | [Stage03_DeepForest_Boss_02](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage03_DeepForest/Boss/Stage03_DeepForest_Boss_02.prefab>) |
| 49 | Stage03_DeepForest | Boss | MossyStones | [Stage03_DeepForest_Boss_03](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage03_DeepForest/Boss/Stage03_DeepForest_Boss_03.prefab>) |
| 50 | Stage03_DeepForest | Special | BirchClearing | [Stage03_DeepForest_Special_01](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage03_DeepForest/Special/Stage03_DeepForest_Special_01.prefab>) |
| 51 | Stage03_DeepForest | Special | LowStoneGarden | [Stage03_DeepForest_Special_02](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage03_DeepForest/Special/Stage03_DeepForest_Special_02.prefab>) |
| 52 | Stage03_DeepForest | Special | MushroomMargin | [Stage03_DeepForest_Special_03](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage03_DeepForest/Special/Stage03_DeepForest_Special_03.prefab>) |
| 53 | Stage03_DeepForest | Special | OldWall | [Stage03_DeepForest_Special_04](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage03_DeepForest/Special/Stage03_DeepForest_Special_04.prefab>) |
| 54 | Stage03_DeepForest | Special | TreeShade | [Stage03_DeepForest_Special_05](<../../Assets/_Project/Prefabs/Environment/Prepared/Stage03_DeepForest/Special/Stage03_DeepForest_Special_05.prefab>) |
| 55 | GoldDungeon | Main | PillarCourt | [GoldDungeon_Main_01](<../../Assets/_Project/Prefabs/Environment/Prepared/GoldDungeon/Main/GoldDungeon_Main_01.prefab>) |
| 56 | GoldDungeon | Main | BrokenGallery | [GoldDungeon_Main_02](<../../Assets/_Project/Prefabs/Environment/Prepared/GoldDungeon/Main/GoldDungeon_Main_02.prefab>) |
| 57 | GoldDungeon | Main | StoneAlcove | [GoldDungeon_Main_03](<../../Assets/_Project/Prefabs/Environment/Prepared/GoldDungeon/Main/GoldDungeon_Main_03.prefab>) |
| 58 | GoldDungeon | Main | QuietCrypt | [GoldDungeon_Main_04](<../../Assets/_Project/Prefabs/Environment/Prepared/GoldDungeon/Main/GoldDungeon_Main_04.prefab>) |
| 59 | GoldDungeon | Main | StoneRecess | [GoldDungeon_Main_05](<../../Assets/_Project/Prefabs/Environment/Prepared/GoldDungeon/Main/GoldDungeon_Main_05.prefab>) |
| 60 | GoldDungeon | Main | TwinPillars | [GoldDungeon_Main_06](<../../Assets/_Project/Prefabs/Environment/Prepared/GoldDungeon/Main/GoldDungeon_Main_06.prefab>) |
| 61 | GoldDungeon | Main | StoneGrate | [GoldDungeon_Main_07](<../../Assets/_Project/Prefabs/Environment/Prepared/GoldDungeon/Main/GoldDungeon_Main_07.prefab>) |
| 62 | GoldDungeon | Main | QuietGallery | [GoldDungeon_Main_08](<../../Assets/_Project/Prefabs/Environment/Prepared/GoldDungeon/Main/GoldDungeon_Main_08.prefab>) |
| 63 | GoldDungeon | Main | CandleRecess | [GoldDungeon_Main_09](<../../Assets/_Project/Prefabs/Environment/Prepared/GoldDungeon/Main/GoldDungeon_Main_09.prefab>) |
| 64 | GoldDungeon | Main | FallenColumn | [GoldDungeon_Main_10](<../../Assets/_Project/Prefabs/Environment/Prepared/GoldDungeon/Main/GoldDungeon_Main_10.prefab>) |
| 65 | GoldDungeon | Boss | PillarCourt | [GoldDungeon_Boss_01](<../../Assets/_Project/Prefabs/Environment/Prepared/GoldDungeon/Boss/GoldDungeon_Boss_01.prefab>) |
| 66 | GoldDungeon | Boss | BrokenGallery | [GoldDungeon_Boss_02](<../../Assets/_Project/Prefabs/Environment/Prepared/GoldDungeon/Boss/GoldDungeon_Boss_02.prefab>) |
| 67 | GoldDungeon | Boss | StoneAlcove | [GoldDungeon_Boss_03](<../../Assets/_Project/Prefabs/Environment/Prepared/GoldDungeon/Boss/GoldDungeon_Boss_03.prefab>) |
| 68 | GoldDungeon | Special | QuietCrypt | [GoldDungeon_Special_01](<../../Assets/_Project/Prefabs/Environment/Prepared/GoldDungeon/Special/GoldDungeon_Special_01.prefab>) |
| 69 | GoldDungeon | Special | StoneRecess | [GoldDungeon_Special_02](<../../Assets/_Project/Prefabs/Environment/Prepared/GoldDungeon/Special/GoldDungeon_Special_02.prefab>) |
| 70 | GoldDungeon | Special | TwinPillars | [GoldDungeon_Special_03](<../../Assets/_Project/Prefabs/Environment/Prepared/GoldDungeon/Special/GoldDungeon_Special_03.prefab>) |
| 71 | GoldDungeon | Special | StoneGrate | [GoldDungeon_Special_04](<../../Assets/_Project/Prefabs/Environment/Prepared/GoldDungeon/Special/GoldDungeon_Special_04.prefab>) |
| 72 | GoldDungeon | Special | QuietGallery | [GoldDungeon_Special_05](<../../Assets/_Project/Prefabs/Environment/Prepared/GoldDungeon/Special/GoldDungeon_Special_05.prefab>) |
| 73 | RubyWasteland | Main | DryRiverbank | [RubyWasteland_Main_01](<../../Assets/_Project/Prefabs/Environment/Prepared/RubyWasteland/Main/RubyWasteland_Main_01.prefab>) |
| 74 | RubyWasteland | Main | BoneField | [RubyWasteland_Main_02](<../../Assets/_Project/Prefabs/Environment/Prepared/RubyWasteland/Main/RubyWasteland_Main_02.prefab>) |
| 75 | RubyWasteland | Main | AncientStumps | [RubyWasteland_Main_03](<../../Assets/_Project/Prefabs/Environment/Prepared/RubyWasteland/Main/RubyWasteland_Main_03.prefab>) |
| 76 | RubyWasteland | Main | RockPass | [RubyWasteland_Main_04](<../../Assets/_Project/Prefabs/Environment/Prepared/RubyWasteland/Main/RubyWasteland_Main_04.prefab>) |
| 77 | RubyWasteland | Main | RuinedOutpost | [RubyWasteland_Main_05](<../../Assets/_Project/Prefabs/Environment/Prepared/RubyWasteland/Main/RubyWasteland_Main_05.prefab>) |
| 78 | RubyWasteland | Main | BleachedGrove | [RubyWasteland_Main_06](<../../Assets/_Project/Prefabs/Environment/Prepared/RubyWasteland/Main/RubyWasteland_Main_06.prefab>) |
| 79 | RubyWasteland | Main | FallenTimber | [RubyWasteland_Main_07](<../../Assets/_Project/Prefabs/Environment/Prepared/RubyWasteland/Main/RubyWasteland_Main_07.prefab>) |
| 80 | RubyWasteland | Main | SmallOasis | [RubyWasteland_Main_08](<../../Assets/_Project/Prefabs/Environment/Prepared/RubyWasteland/Main/RubyWasteland_Main_08.prefab>) |
| 81 | RubyWasteland | Main | ScatteredBones | [RubyWasteland_Main_09](<../../Assets/_Project/Prefabs/Environment/Prepared/RubyWasteland/Main/RubyWasteland_Main_09.prefab>) |
| 82 | RubyWasteland | Main | StoneShelter | [RubyWasteland_Main_10](<../../Assets/_Project/Prefabs/Environment/Prepared/RubyWasteland/Main/RubyWasteland_Main_10.prefab>) |
| 83 | RubyWasteland | Boss | DryRiverbank | [RubyWasteland_Boss_01](<../../Assets/_Project/Prefabs/Environment/Prepared/RubyWasteland/Boss/RubyWasteland_Boss_01.prefab>) |
| 84 | RubyWasteland | Boss | BoneField | [RubyWasteland_Boss_02](<../../Assets/_Project/Prefabs/Environment/Prepared/RubyWasteland/Boss/RubyWasteland_Boss_02.prefab>) |
| 85 | RubyWasteland | Boss | AncientStumps | [RubyWasteland_Boss_03](<../../Assets/_Project/Prefabs/Environment/Prepared/RubyWasteland/Boss/RubyWasteland_Boss_03.prefab>) |
| 86 | RubyWasteland | Special | RockPass | [RubyWasteland_Special_01](<../../Assets/_Project/Prefabs/Environment/Prepared/RubyWasteland/Special/RubyWasteland_Special_01.prefab>) |
| 87 | RubyWasteland | Special | RuinedOutpost | [RubyWasteland_Special_02](<../../Assets/_Project/Prefabs/Environment/Prepared/RubyWasteland/Special/RubyWasteland_Special_02.prefab>) |
| 88 | RubyWasteland | Special | BleachedGrove | [RubyWasteland_Special_03](<../../Assets/_Project/Prefabs/Environment/Prepared/RubyWasteland/Special/RubyWasteland_Special_03.prefab>) |
| 89 | RubyWasteland | Special | FallenTimber | [RubyWasteland_Special_04](<../../Assets/_Project/Prefabs/Environment/Prepared/RubyWasteland/Special/RubyWasteland_Special_04.prefab>) |
| 90 | RubyWasteland | Special | SmallOasis | [RubyWasteland_Special_05](<../../Assets/_Project/Prefabs/Environment/Prepared/RubyWasteland/Special/RubyWasteland_Special_05.prefab>) |
