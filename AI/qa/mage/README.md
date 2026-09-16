# 마탑 실기기 검증

Unity 6000.3.21f1의 `TitleLobbyDeviceBuild.Build`로 만든 개발 APK 전용입니다. `LOBBY_DEVICE_QA`가 없는 일반 빌드에는 진단 명령이 포함되지 않습니다. 기존 `AI/qa/hud`, `AI/qa/settings`의 실제 Android 터치·캡처·성능 측정을 사용합니다.

저장소 루트에서 실행합니다. `HUD_QA_OUTPUT`은 APK와 기록을 둘 디렉터리, `HUD_QA_SERIAL`은 실제 `adb devices`에서 확인한 기기 ID입니다. 기본 대상은 별도 `.lobbyqa` 패키지입니다.

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
