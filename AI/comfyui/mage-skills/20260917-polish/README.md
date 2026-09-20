# 운석 잔흔 제작 · 2026-09-17

목적: 운석이 떨어진 후 작은 불꽃 두 개만 남던 장면에 낮은 지면 흔적을 추가한다. 피해량·잔불 횟수는 유지한다.

## 실제 실행

Comfy Cloud의 현재 도구 목록과 노드 스키마를 확인한 뒤, 같은 Meteor 아이콘을 재질·팔레트 참고로 제공했다. 시점/구도는 지면 데칼용 프롬프트로 지정했다. 프레임별 이미지 생성은 하지 않았다.

| 후보 | 모델 / 노드 | 작업 ID | 판단 |
|---|---|---|---|
| nano-v1 | Gemini 3.1 Flash Image / GeminiNanoBanana2V2 | c4b7c30a-62e2-4443-80f0-9bb4ab3f9045 | 기각: 높은 테두리와 둥근 외형이 지면보다 바위처럼 보임 |
| gpt-v1 | gpt-image-2 / OpenAIGPTImageNodeV2 | 53a11d72-fc15-475f-a8c7-a68216c408d4 | 채택: 낮은 테두리, 얕은 흔적, 절제된 균열 |

각 폴더의 `workflow.api.json`과 `request.json`이 실제 제출값이다. 별도 override는 없다. `workflow.ui.json`은 편집 가능한 그래프이며 `cloud-save.json`에 저장 ID/버전이 있다. 전체 프롬프트, 참조 입력, 실제 출력은 함께 보존했다. 노드 패키지 버전은 서비스에서 제공하지 않아 특정 커밋으로 고정할 수 없다. GPT 노드의 seed는 구현되지 않았고 Gemini seed는 최선의 재현만 지원한다. GPT의 custom width/height는 preset 선택 시 사용되지 않는다는 preflight 안내를 확인했다. 지정한 1024×1024 preset과 출력 크기는 일치했다.

## 후처리와 기기 피드백

`finish_crater.py`: 가장자리 크로마 flood-fill → 닫힌 영역 크로마 제거 → 16px mode tile 축소 → 64×64·6색. 설정, 팔레트, 해시는 `finish-manifest.json`에 있다. 비교 시트와 두 원본을 보존했다.

작업본은 `Assets/_Project/Art/VFX/MageTower/MeteorScorch.png`이며 ExternalAssets는 수정하지 않았다. Point, mipmap/readable OFF, Atlas_MageVFX에 포함했다. 기존 FirePit·FireExplosion 애니메이션을 조합한다.

첫 기기 빌드 b15의 3.8×4.3 월드 크기는 세로로 너무 부풀어 보여, b16에서 3.8×2.6으로 낮췄다. 소스 이미지나 피해 반경은 바꾸지 않았다. 검증 근거는 `Recordings/CombatPolish20260917/Iteration1/Device/skill-8-base.mp4`와 Iteration2의 같은 파일이다.

## 비용 구분

제출 전 estimate 응답과 실제 완료 이벤트를 보존했다. 완료 이벤트의 API 표시 사용량은 Nano **17.19**, GPT **15.77**, 합계 **32.96 credits**다. 서비스 문서에 따라 이는 표시용 추정 크레딧이며 확정 달러 청구액이 아니다.

22:01 KST에 다시 조회한 invoice 기반 `get_usage_report`의 12:00–13:00 UTC 버킷은 **$0.156237**다. GPT 이미지 입출력·텍스트 합계 $0.0747564, Gemini 합계 $0.0814806이다. 이 시간대 완료 이벤트는 위 두 작업이며, 보고서의 해당 모델 비용을 이번 제작비로 집계했다. 서비스는 작업별 달러 청구서를 제공하지 않으므로 이 수치는 **해당 시간대의 실제 집계 지출**이다. 처음 조회한 보고서는 12:00 UTC 이전만 포함해 이번 작업 비용 판정에 쓰지 않았다. 원본 조회와 완료 응답, 제출 전 추정 응답은 서로 구별해 보존했다.
