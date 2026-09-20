# 마탑 스킬

현재 9종의 등록 원본은 `SO/MageTowerSkillList.asset`입니다. ID 0–5·7–9와 기존 GUID를 유지합니다. 질풍 칼날(ID 6)은 게임에서 제거했으며 ID를 재사용하지 않습니다. 기존 저장의 투자 기록은 보존하고 장착만 해제합니다. [Skill_Catalog.xlsx](../_Project/Scripts/Skill_Catalog.xlsx)는 같은 데이터를 개발자가 읽고 편집할 수 있게 정리한 문서이며, 현재 런타임 임포터는 없습니다.

| 요소 | 위치 |
|---|---|
| 런타임 데이터 | `SO/<Key>/MageTowerSkill_<Key>.asset` |
| 관리자·기존 직렬화 호환 코드 | `Scripts/` |
| 시전·성장·서버 내보내기 계약 | `../_Project/Scripts/MageTower/` |
| 최종 아이콘 | `../_Project/Art/Icons/MageTower/` |
| 전투 VFX 프리팹 | `../_Project/Prefabs/VFX/MageTower/` |
| VFX 시트·애니메이션 | `../_Project/Art/VFX/`, `../_Project/Art/Animations/VFX/MageTower/` |
| UI | `../UGUI/Prefabs/Popups/Panel_MageTowerDetail.prefab`, `../UGUI/Prefabs/Items/Item_MageSkillCell.prefab` |
| 이전 3종 실험 프리팹·씬 | `../_Project/Tests/Fixtures/MageTowerLegacy/`, `../_Project/Tests/Scenes/MageTowerLegacy.unity` |

에디터 `KingdomIdle/MageTower` 메뉴에서 스킬 데이터·아트 배선과 UI 준비 도구를 찾을 수 있습니다. 생성 도구는 데이터와 프리팹의 설정을 재작성하므로 기획 변경 시 도구와 카탈로그를 함께 갱신합니다. `SkillEffect_*`, `DamageEffect_*`와 구형 컴포넌트는 기존 씬·SO 직렬화 호환용으로 보존했습니다. 새 시전은 `MageTowerSpellCast`가 담당합니다.

스킬에는 희귀도가 없습니다. 뽑기 전체에서 스킬 총 50% 안에서 9종이 같은 확률(각 50%/9)이며 중복은 같은 스킬의 파편 30개입니다. 등급은 장비에만 적용합니다.

결과 창은 최초 획득과 중복 파편을 구별하고, 중복 횟수와 파편 수를 따로 표시합니다. 파편 120개는 동일 스킬 중복 4회의 합계입니다.

마탑을 길게 누르면 자동 시전이 전환됩니다. 자동 시전을 끄면 우측 스테이지 표시 위에 장착 스킬이 정렬되어 펼쳐지고, 자동 시전을 켜면 반대로 접힙니다. 준비 완료는 밝은 테두리, 쿨다운은 시계 방향으로 걷히는 음영과 남은 초로 표시합니다. 탭은 자동 조준, 드래그는 원하는 전투 지점에 시전합니다. 얼음 송곳(ID 1)과 유성우(ID 3)는 분산 타격이므로 드래그 시 아무 효과도 발생하지 않습니다. 조준 표시는 전장 도트 격자를 따르는 쿼터뷰 타원입니다. 회복의 성역은 지정 지점의 범위 안에서 체력 비율이 가장 낮은 살아 있는 왕국군을 회복합니다.

ID 4의 표시 이름은 **맹독 늪**입니다. 맹독 늪·암석 봉인·회복의 성역 아이콘은 실제 VFX 프레임에서 파생했습니다. 기본·개화의 실루엣은 동일합니다. 출처, 48px 작업 과정, 변경 비교와 실제 지출 조회는 `AI/comfyui/mage-skills/20260918-playability/`에 보존합니다. 기존 원본을 재조합했으며 유료 생성 호출은 없었습니다.

개화는 파편 각성 10에서 켜고 끕니다. 강화 초기화는 각성·개화 선택을 보존합니다. 현재 고유 개화는 천벌·만년빙정이며, 나머지 7종은 `개화: 미정`과 1.15배 피해/회복을 적용합니다. 개화 ON이면 같은 실루엣의 `_Bloom` 아이콘을 표시합니다. 시전 시작 시의 전투·각성·개화 값을 고정하여 도중 변경이 진행 중 시전에 섞이지 않습니다.

`MageSkillStateSnapshot.Capture`는 schema 1, catalog `mage-4`, progression revision, 전체 파편 수, 장착 ID를 손실 없이 내보냅니다. 서버 인증·중복 요청 방지·원격 상태 적용은 서버 연동 단계에서 구현해야 합니다.

`MagePolishPreparation.Prepare`는 현재 아트·UI·아틀라스와 한글 글리프를 준비합니다. 폰트 글리프는 빌드 전에 채워 첫 화면에서 런타임 생성으로 멈추는 현상을 방지합니다. 공용 풀·상태이상은 `_Project/Scripts/Combat`에 있습니다.

[통합 검증·밸런스·비용 기록](../../Docs/ArtPreparation/MAGE_INTEGRATION_VALIDATION.md)

각성 상세는 매 각성의 효과량 +5%·기본 쿨타임 -2%, 4·8각성 추가 횟수와 다음 단계의 실제 수치를 표시합니다. 운석의 1회 충돌·잔열 2회는 고정입니다. 성역은 한 번 등장해 회복 파동을 유지한 뒤 퇴장하며, 공허 균열의 흡입 반경은 1.95(피해 반경 1.3) 월드 단위입니다.

일반 라이트닝은 최초 `ThunderEffects` 5프레임·12fps 원화를 사용합니다. 시전 후 2/12초인 타격 시점(약 0.167초)마다 다음 낙뢰를 이어 기본 3회, 4각성 4회, 8각성 이상 5회 시전합니다. 첫 낙뢰 이후에는 원래처럼 중심에서 0.8 이내에 분산하며, 각 낙뢰의 판정 반경은 0.55입니다. 수동 조준은 두 반경을 합친 전체 도달 범위 1.35를 표시합니다. 천벌 개화의 뇌운·단일 강타 연출은 별도로 유지합니다.

화염폭풍은 원본 하단 회전 띠를 유지한 12프레임 불꽃 꼭대기를 사용합니다. 운석은 32 PPU의 48프레임·20fps 낙하체로, 회전하는 암석 표면·흐르는 불꽃·불티·파편을 미리 구운 시트 한 장으로 재생합니다. 낙하는 2.4초 동안 점차 가속하며, 충돌 뒤 0.09초에 피해를 적용합니다. 시전 마법진은 없습니다. 제작 출처와 변경 비교는 `AI/comfyui/mage-vfx/revision5/`에 보존합니다.

전투 표시 순서는 `CombatVfxOrder`에서 관리합니다. 지속 장판은 Default -40~-28, 회복·상태 발밑 파동은 -10~-9, 단발 타격은 CombatVFX 20 이상입니다. `CombatStatusVisuals`는 실제 기절·최강 감속·도발·보호막 상태를 읽어 표시하며, 캐릭터 풀 반환과 사망 시 표시를 해제합니다. 왕국군 발·머리 기준점은 JobData의 대기 몸체에서 측정하며 회복 파동은 발 위치를 추적합니다. 에셋 재생성과 레이어 검사는 `CombatPresentationPreparation.Prepare/Validate`, 실행 검사는 `PlayabilityLiveValidation.RunCombatPresentation`입니다.
