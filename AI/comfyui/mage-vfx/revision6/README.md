# 유성우·메테오 개화 아트 개정

2026-09-20. 기준 원본은 커밋 `c2a6a4b0d`이며 구매 원본은 수정하지 않았다.

`prepare.py`가 기존 도트의 실루엣을 재조합하고 유성우 색상을 변경한다. 새로 필요한 착탄 타원과 붉은 지면 균열만 논리 픽셀 격자에서 작성했다. 임의 확산 생성보다 기존 스프라이트의 도트·팔레트·구도를 정확히 보존할 수 있는 공정을 선택했다.

실제 Comfy Cloud에서 `LoadImage → ImageCrop → (천벌: RadianceGPUColorMatrix) → ImageQuantize(16색, dither none) → SaveImage`를 실행했다. API 요청은 `icons-submitted-request.json`, 편집 가능한 그래프는 `icons-ui.json`, 서버 저장 기록은 `icons-cloud-save.json`이다. 실행은 `ff2ac409-59f5-422c-ac2f-b53c8961f16b`, 저장 워크플로는 `c3342933-e660-4ed9-95fa-da9656b6e278` 버전 1이다. 노드 스키마는 실제 서버에서 조회했다. 확산 모델·텍스트 프롬프트·seed는 사용하지 않았다. 서버가 패키지 버전을 제공하지 않아 노드 패키지 버전은 고정 확인할 수 없다.

`cloud-icons/`는 Comfy 원본, `icons/`는 채택한 RGBA 파일이다. 양자화 노드가 RGB로 출력해 일부 아이콘의 배경이 검게 보이는 문제를 발견했다. `finish_cloud.py alpha`로 각 입력 도트의 이진 알파 마스크를 그대로 복구했다. 전후 비교는 `icons-before.png`, `icons-after.png`, 해시·팔레트·마스크는 `manifest.json`, `icons-output-provenance.json`에 있다. 양자화 후 알파를 복구하므로 검은색 테두리를 별도로 생성하지 않는다.

유성우는 기존 64px 6프레임의 형태·재생 속도를 보존하고 따뜻한 세 색으로 변경했다. 착탄 파동은 40×28px 8프레임, 20fps다. 메테오 낙하체는 revision5의 48프레임·32fps를 그대로 사용한다. 장판의 갈색 원본은 NEAREST로 확대해 보존하고, 112×76px 캔버스에 16프레임·16fps의 붉은 균열을 분리했다. 스프라이트별 설정은 `effect-settings.json`에 기록했다. 지속 장판과 균열은 전투원 뒤에 표시한다.

실제 과금 조회에서 이 작업의 GPU 사용량은 **5.06304초, RTX PRO 6000**으로 확인했다. 최초 달러 보고서의 종료 시각은 12:00 UTC이고 작업은 12:01 UTC에 실행되어 이 작업의 달러 비용은 아직 확인되지 않았다. 확인되지 않은 금액을 0원으로 기록하지 않는다. `usage-before.json`, `usage-after.json`, `billing-job.json`이 조회 근거다. 후속 청구 확인은 검증 기록에 반영한다.

실행 순서: `prepare.py` → `finish_cloud.py` 제출 → 완료 후 `finish_cloud.py collect` → `audit.py` → Unity `MageSkillAssetPreparation.Build`. 이미 성공한 Cloud 작업을 재제출할 필요 없이 보존한 최종 산출물을 사용할 수 있다. API 실패·미지원 노드를 다른 공정으로 실행한 것처럼 기록하지 않았다.
