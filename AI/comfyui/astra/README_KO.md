# Astra 프로덕션 워크플로

> 파이프라인 전반·규칙·새 신 만드는 절차는 **`../README.md`** 에 있다.
> 이 문서는 Astra 고유 사항만 다룬다.

## Astra 고유값

| 항목 | 값 |
|---|---|
| 팔레트 | `astra_palette_32.png` — 인디고 / 바이올렛 / 블루 / 실버 / 피부 (녹색 없음) |
| 컷씬 무기 | 없음 (빈 손을 뻗는 포즈) |
| 채택 시드 | 컷씬 A (B 는 손끝이 우측 33px 잘려 `output/archive/` 로) |

## 공용 구현이 사는 곳

다른 신들도 전부 이 파일들을 import 한다. 여기서 고치면 전 캐릭터에 반영된다.

| 파일 | 역할 |
|---|---|
| `custom_nodes/ComfyUI-AstraTools/astra_nodes.py` | `finalize()` — 축소·팔레트 스냅·알파 이진화·정수배 확대. ComfyUI 노드 겸 독립 모듈 |
| `finalize_ultimate.py` | `key_green()` — 3패스 크로마 그린 키잉 |
| `api_to_ui.py` | API 포맷 → 편집기 graph 포맷 변환 (`SCHEMA` / `POS` 는 실측 스키마 기준) |

로컬 ComfyUI 를 쓴다면 `custom_nodes/ComfyUI-AstraTools/` 를 그대로 복사해 넣고
도감 픽셀 체인 `[23]~[26]` 을 `AstraPixelFinalize` 한 노드로 대체하면
그래프 안에서 팔레트가 고정된다.

## 파일

| 파일 | 용도 |
|---|---|
| `Astra_Production_Workflow_UI.json` | ComfyUI 편집기용 graph 포맷 |
| `Astra_Production_Workflow_API.json` | `submit_workflow` 제출용 API 포맷 |
| `Astra_Prompts.md` | 마스터 / 도감 / 컷씬 프롬프트 원문 |
| `build_workflow.py` | API JSON + 프롬프트 문서 생성기 |
| `finalize_local.py` | 도감 팔레트 고정 마감 |
| `finalize_ultimate.py` | 컷씬 그린키 + 팔레트 고정 + 투명 배경 마감 (공용) |

클라우드 워크스페이스에도 같은 그래프가 `astra-production-workflow.json` 으로 저장돼 있다.

## 재실행

```bash
python build_workflow.py
python api_to_ui.py
python finalize_local.py
python finalize_ultimate.py 02_Astra_Ultimate_Raw_A.png 02_Astra_Ultimate_Pixel_Final.png
```
