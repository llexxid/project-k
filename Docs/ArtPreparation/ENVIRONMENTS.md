# 환경 프리셋 목록

2026-10-01 개정. 중앙 전투 공간은 비우고, 외곽의 작은 군집과 나무·바위·폐허·연못으로 90개 배경을 구별한다. 기존 프리팹 ID, Addressables 연결, 풀 선택 방식, Ground / GroundDetails / Scenery 3개 타일맵을 유지한다.

[번호별 실제 Unity 미리보기](Validation/EnvironmentPolish20261001/after/index.html) · [수정 전 미리보기](Validation/EnvironmentPolish20261001/before/index.html) · [검증 기록](Validation/EnvironmentPolish20261001/README.md)

번호는 피드백용 고정 식별자다. 실제 전투에서는 해당 지역 풀에서 직전 배경을 제외하고 선택한다. 일반·보스·특수는 프리셋의 배치 유형이며 특정 웨이브 번호에 고정 대응하지 않는다.

- 01–18 숲길, 19–36 초원 숲, 37–54 깊은 숲, 55–72 골드 던전, 73–90 루비 황무지. 각 그룹은 Main 10 / Boss 3 / Special 5 순이다.
- 일반·특수 중앙 여백 x ±2.1875, 보스형 x ±2.625를 유지했다. 모든 소품에 간격 예약을 적용해 나무끼리 겹치거나 작은 소품을 덮지 않는다.
- 숲의 잘린 꽃밭 조각을 제거하고 완성된 소품을 사용한다. 연못 바깥의 밝은 잔디색은 같은 지면에 연결한다.
- 던전은 회색 석재 바닥을 유지하고, 황무지는 균열 전이 조각을 무작위로 섞는 대신 조용한 모래 중앙과 외곽 균열을 연결했다.
- 황무지 고목·마른 풀·두개골 4종의 잘못된 스프라이트 범위를 수정했다. 작은 소품 28종은 완전 투명 여백만 정리해 외곽 가시성을 개선했다. 구매 원본 PNG와 ExternalAssets는 변경하지 않았다.
- 추가 런타임 장식 오브젝트·충돌체·텍스처는 없다. 세 타일맵으로 배치하며 새 아이콘도 기존 아틀라스 경로를 쓴다.

재생성: `EnvironmentRichnessPreparation.Prepare`. 검증과 135장 렌더를 함께 실행하려면 `EnvironmentPolishValidation.PrepareAndCaptureAfter`를 사용한다. 기준 배치·타일 수·시드는 [manifest](Manifests/environment-presets.json)에 기록한다.

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
| 57 | GoldDungeon | Main | SupplyNiche | [GoldDungeon_Main_03](<../../Assets/_Project/Prefabs/Environment/Prepared/GoldDungeon/Main/GoldDungeon_Main_03.prefab>) |
| 58 | GoldDungeon | Main | QuietCrypt | [GoldDungeon_Main_04](<../../Assets/_Project/Prefabs/Environment/Prepared/GoldDungeon/Main/GoldDungeon_Main_04.prefab>) |
| 59 | GoldDungeon | Main | VineHall | [GoldDungeon_Main_05](<../../Assets/_Project/Prefabs/Environment/Prepared/GoldDungeon/Main/GoldDungeon_Main_05.prefab>) |
| 60 | GoldDungeon | Main | TwinPillars | [GoldDungeon_Main_06](<../../Assets/_Project/Prefabs/Environment/Prepared/GoldDungeon/Main/GoldDungeon_Main_06.prefab>) |
| 61 | GoldDungeon | Main | ForgottenBench | [GoldDungeon_Main_07](<../../Assets/_Project/Prefabs/Environment/Prepared/GoldDungeon/Main/GoldDungeon_Main_07.prefab>) |
| 62 | GoldDungeon | Main | StoreRoom | [GoldDungeon_Main_08](<../../Assets/_Project/Prefabs/Environment/Prepared/GoldDungeon/Main/GoldDungeon_Main_08.prefab>) |
| 63 | GoldDungeon | Main | CandleRecess | [GoldDungeon_Main_09](<../../Assets/_Project/Prefabs/Environment/Prepared/GoldDungeon/Main/GoldDungeon_Main_09.prefab>) |
| 64 | GoldDungeon | Main | FallenColumn | [GoldDungeon_Main_10](<../../Assets/_Project/Prefabs/Environment/Prepared/GoldDungeon/Main/GoldDungeon_Main_10.prefab>) |
| 65 | GoldDungeon | Boss | PillarCourt | [GoldDungeon_Boss_01](<../../Assets/_Project/Prefabs/Environment/Prepared/GoldDungeon/Boss/GoldDungeon_Boss_01.prefab>) |
| 66 | GoldDungeon | Boss | BrokenGallery | [GoldDungeon_Boss_02](<../../Assets/_Project/Prefabs/Environment/Prepared/GoldDungeon/Boss/GoldDungeon_Boss_02.prefab>) |
| 67 | GoldDungeon | Boss | SupplyNiche | [GoldDungeon_Boss_03](<../../Assets/_Project/Prefabs/Environment/Prepared/GoldDungeon/Boss/GoldDungeon_Boss_03.prefab>) |
| 68 | GoldDungeon | Special | QuietCrypt | [GoldDungeon_Special_01](<../../Assets/_Project/Prefabs/Environment/Prepared/GoldDungeon/Special/GoldDungeon_Special_01.prefab>) |
| 69 | GoldDungeon | Special | VineHall | [GoldDungeon_Special_02](<../../Assets/_Project/Prefabs/Environment/Prepared/GoldDungeon/Special/GoldDungeon_Special_02.prefab>) |
| 70 | GoldDungeon | Special | TwinPillars | [GoldDungeon_Special_03](<../../Assets/_Project/Prefabs/Environment/Prepared/GoldDungeon/Special/GoldDungeon_Special_03.prefab>) |
| 71 | GoldDungeon | Special | ForgottenBench | [GoldDungeon_Special_04](<../../Assets/_Project/Prefabs/Environment/Prepared/GoldDungeon/Special/GoldDungeon_Special_04.prefab>) |
| 72 | GoldDungeon | Special | StoreRoom | [GoldDungeon_Special_05](<../../Assets/_Project/Prefabs/Environment/Prepared/GoldDungeon/Special/GoldDungeon_Special_05.prefab>) |
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
