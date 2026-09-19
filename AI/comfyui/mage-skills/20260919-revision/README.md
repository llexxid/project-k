# 마탑 연출 개정 — 2026-09-19

기존 스프라이트를 우선 검토했다. 암석 봉인·회복 성역·공허 균열은 기존 작업본과 원본 애니메이션을 재사용한다. 원본 ExternalAssets는 수정하지 않았다.

운석의 기존 23px 원형 마스크·프레임별 회전은 균열이 흔들려 보였다. EarthRock의 큰 원본을 재사용한 첫 수정은 Android에서 돌기둥처럼 보여 기각했다(`Recordings/MageRevision/Device/revision-spell-8.png`). 운석 몸체 하나만 새로 만들고 이동과 불꽃은 Unity·기존 FireFlamme 클립이 담당한다. 프레임별 확산 생성은 없다.

`comfy_client.py`를 통해 실제 Comfy Cloud MCP 연결과 도구/노드 스키마를 확인했다. Gemini Nano Banana 2의 참조 입력으로 한 장을 생성했다. 짧은 공정과 기존 팔레트 참조로 형태를 통제할 수 있어 체크포인트·LoRA·ControlNet을 추가하지 않았다. 단순한 몸체에는 업스케일로 정보를 늘리는 대신 64px 마감이 적합했다.

- 작업: `7c845aeb-db5b-44bc-819b-197e1678c6f9`
- 모델: `gemini-3.1-flash-image`, 노드 `GeminiNanoBanana2V2` (core; 구체적 서버 버전은 제공되지 않음).
- [Cloud 편집 그래프](https://cloud.comfy.org/#1aa305cb-55f0-4454-83cc-c489de319fdd), 저장 버전 1.
- 실제 제출본: `meteor-v1/request.json`, `workflow.api.json`. 로컬 편집 그래프 `workflow.ui.json`은 같은 API 그래프에서 생성했다. Cloud 저장도 API → 편집 그래프 변환 성공을 확인했다.
- 참조: `meteor-v1/reference-icon48.png`. 프롬프트·적용 설정은 제출본에 전부 보존했다. 1K·1:1·MINIMAL, seed 91951. Gemini seed는 최선 노력이며 결정성을 보장하지 않는다.
- 원본 출력: `meteor-v1/raw.png` 1024×1024. `finish_meteor.py`의 경계 flood-fill → 닫힌 크로마 제거 → 인접 잔색 제거 → 16px mode-tile → 6색 스냅으로 `body64.png`를 만들었다.
- 게임 에셋: `Assets/_Project/Art/VFX/MageTower/MeteorBody64.png`, 64×64, 이진 알파, Point. 생성된 가장 어두운 외곽선은 몸체의 가장 어두운 갈색과 합쳐 별도의 검은 윤곽선을 없앴다.
- 추정: 사전 노드 견적 약 18 credits/image. 실행 후 billing activity는 **17.2 credits_used**를 반환했다. 이는 서비스의 크레딧 표시값이며 청구된 달러액과 구분한다.
- 실제 달러 지출: 최초 조회는 실행 전 11:00 UTC까지만 집계되어 미확인이었다. 최종 재조회에서 실행을 포함하는 **2026-09-19 11:00–12:00 UTC의 청구 기반 합계 $0.0815334**를 확인했다(출력 $0.08064 + 이미지 입력 $0.000672 + 텍스트 입력 $0.0002214). 이 구간에서 실행한 생성은 한 건이다. 서비스가 작업 ID별 달러액을 직접 제공하는 것은 아니므로 시간 구간의 실제 합계와 작업별 크레딧 표시를 구분한다. `usage-final.json`, `task-spend.json`, `billing-after.json`에 근거를 보존한다.

운석 몸체의 최종 채택은 Unity와 Android 화면에서 확인하며, 검사 기록은 `Docs/ArtPreparation/MAGE_REVISION_20260919.md`에 연결한다.
