# 마탑 실기기 검증

Unity 6000.3.21f1의 `TitleLobbyDeviceBuild.Build`로 만든 개발 APK 전용입니다. `LOBBY_DEVICE_QA`가 없는 일반 빌드에는 진단 명령이 포함되지 않습니다. 기존 `AI/qa/hud`, `AI/qa/settings`의 실제 Android 터치·캡처·성능 측정을 사용합니다.

저장소 루트에서 실행합니다. `HUD_QA_OUTPUT`은 APK와 기록을 둘 디렉터리, `HUD_QA_SERIAL`은 실제 `adb devices`에서 확인한 기기 ID입니다. 기본 대상은 `.lobbyqa` 패키지입니다. 빌드 시 같은 디렉터리를 `LOBBY_QA_OUTPUT`으로 지정하고 `DEVICE_BUILD_PURPOSE`에 테스트 용도를 넣습니다. `install`은 성공한 빌드의 `build.json`에서 버전·용도가 포함된 APK 경로를 읽으며, 진단 기능이 없는 수동 테스트 빌드는 거절합니다. 빌드 진입점은 [UI 개발 안내](../../../Assets/UGUI/README.md), 기기 정리 기준은 [프로젝트 지침](../../../AGENTS.md)을 참조합니다.

```powershell
$env:HUD_QA_OUTPUT = 'Recordings/CatalogIntegration/Mage/PolishFinal/Device'
python AI/qa/mage/mage-device.py install
python AI/qa/mage/mage-device.py mage-acceptance acceptance
python AI/qa/mage/mage-device.py mage-fixture fixture '{"enhance":10,"awaken":10,"bloom":false}'
python AI/qa/mage/mage-visual-review.py
python AI/qa/mage/mage-release-combat.py
python AI/qa/mage/mage-release-performance.py
python AI/qa/mage/mage-impact-review.py
```

`install`은 최초 실행 때 기존 QA 계정을 `.utmp/catalog-integration/mage-validation/device-original-profile.json`에 백업합니다. 기존 백업을 덮어쓰지 않습니다. fixture는 별도 QA 계정만 변경하며 장기 성장 테스트가 아닙니다. 검사 후 계정 원본을 복원하고, 화면 크기·DPI는 실행 전 기록한 값으로 복원합니다. 이번 세션의 복구 도구는 `mage-restore-device.py`이며 QA 계정 원본과 백업의 바이트 일치, 1080×2316/DPI 450, 원래 LowSpec/PowerSave/KeepAwake OFF를 검사합니다. 다른 세션에서는 해당 기기의 원래 값을 먼저 확인해야 합니다.

`mage-acceptance`는 카탈로그·10종 균등 추첨, 신규/중복 지급, 파편 각성 비용, 개화 잠금과 저장, 70,000파편 내보내기, 저장 실패 원자성을 검증합니다. `mage-configure`, `mage-equip`, `mage-slot-cast`, `mage-bloom`, `timescale`은 재현 fixture와 관찰용입니다. `mage-cast`의 `captureMs`는 지정된 게임 시간 뒤 프레임을 정지해 ADB 왕복 시간과 무관하게 적중 프레임을 캡처합니다. 일반 UI 흐름은 실제 터치로 따로 검사합니다.

회복 스킬은 실제 부상이 발생할 때까지 기다립니다. 건강한 파티의 시전 거절은 정상입니다. 쿨타임은 검사 명령으로 강제 초기화하지 않습니다. `mage-release-combat.py`는 단일 보스와 다중 적의 개화 피해 및 시전 스냅샷을 검사합니다. 군중 검사는 도중 사망에 따른 유효 타격 감소를 피하도록 E0/A10, 3-10에서 진행합니다. `mage-first-open-check.py`는 첫 마탑 진입 프레임, `mage-polish-ui.py`는 개화 아이콘·폐기 메뉴 제거·결제 취소·태블릿 스크롤을 검사합니다. 이전 `mage-spell-checks.py`, `mage-bloom-checks.py`는 최초 통합 기록의 재현용입니다.

화면 비율 에뮬레이션을 여러 실기기 결과로 표기하지 않습니다. 성능 수치는 동일 fixture·구간에서 측정하며 진단 도구의 자체 할당도 포함합니다. 최종 근거는 [마탑 검증 보고서](../../../Docs/ArtPreparation/MAGE_INTEGRATION_VALIDATION.md)입니다.

## 실제 시간 플레이 세션

`player_session.py`는 전경 앱, `timeScale == 1`, 진행 중인 게임 시각을 10초마다 기록합니다. 두 관찰점 사이의 게임 시간과 실제 시간 중 작은 값만 합산하고, 종료·재설치·백그라운드·정지·긴 관찰 공백을 제외합니다. 저장된 세션을 덮어쓰지 않으므로 회차마다 새 디렉터리를 사용합니다. 이 도구는 게임 재화나 진행 상태를 변경하지 않습니다.

```powershell
python AI/qa/mage/player_session.py Recordings/PlayerSimulation/Run1/Timing
$env:HUD_QA_OUTPUT = 'Recordings/PlayerSimulation/Run1'
python AI/qa/mage/player_actions.py view first-screen
python AI/qa/mage/player_actions.py tap army-open BtnKingdomArmy
python AI/qa/mage/player_actions.py balance after-army
```

터치 작업은 별도 프로세스에서 수행하며 화면·원시 상태·작업 시각을 남깁니다. 이동 중인 패널의 좌표를 읽은 경우 펼침 완료 후 다시 관찰합니다. `player_actions.py command`는 명시적으로 준비한 격리 fixture에만 사용하고, 자연 성장 회차와 분리해 기록합니다. 이번 세션의 원본 백업은 `Recordings/PlayerSimulation/Original`, 자연 성장 보존본은 `Run2/earned-profile.json`입니다. 이전 세션의 복원 스크립트와 백업을 이번 저장에 혼용하지 않습니다.

세 회차가 끝난 뒤 `summarize_player_sessions.py Recordings/PlayerSimulation <출력.json>`으로 각 회차의 1,800초 이상 완료와 시간 중복 여부를 검사할 수 있습니다. 미완료 회차는 성공 보고서를 만들지 않습니다.

`player_dungeon_routes.py`는 명시적인 후반 QA 세팅에서 던전 10경로를 실제 버튼으로 입장·클리어·복귀하고 보상과 티켓을 대조합니다. 기록의 `combatWallSeconds`는 입장 요청부터 결과 확인·메인 복귀까지의 시간으로, 전투만의 소요 시간이 아닙니다. 루비 5단계 최초 검사에는 전환 경계에서 수동 재시도한 대기 시간도 포함됩니다.

`player_transition_checks.py`는 2초 실제 터치를 유지하는 도중 QA 웨이브 전환을 시작해 던전 입장과 1회 티켓 소비를 세 번 확인합니다. 명령 왕복 시간은 정확한 손가락 해제 시각과 구별합니다. 전환 중 대기·중복 요청·닫기 취소의 결정론적 검사는 Unity `ValidateSessionRoutes`에 있습니다.

마지막에는 `restore_player_session.py Recordings/PlayerSimulation/Original Recordings/PlayerSimulation/PostQA`로 이번 원본 tar에서 QA 저장 두 개와 설정만 복원합니다. 세 회차 완료·백업 해시·복원 바이트·다른 계정 저장 보존을 검증하며, 진단 파일은 원본에 없었던 알려진 이름만 제거합니다. 최종 일반 빌드를 설치한 후에는 진단 명령을 사용하지 않고 실제 로그인과 캡처로 확인합니다.
