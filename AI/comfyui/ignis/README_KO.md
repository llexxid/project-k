# Ignis 프로덕션 워크플로

> 파이프라인 전반·규칙·새 신 만드는 절차는 **`../README.md`** 에 있다.
> 이 문서는 Ignis 고유 사항만 다룬다.

## 디자인 (사용자 확정)

붉은 장발(짙은 크림슨 뿌리 → 주홍 → 잉걸빛 끝) / 여성형 / 불꽃으로 짜인 무희복 /
연한 갈색 피부 / 붉은 눈 / **룬 문자가 각인된 화염 창**.
금색·왕관·바이저·갑옷·망토·날개·뿔 없음.

> 예전 로스터 문서의 "남성 마왕 이그니스"는 **폐기됐다.**

## Ignis 고유값

| 항목 | 값 |
|---|---|
| 팔레트 | `ignis_palette_32.png` — ember 4 + 불꽃 램프 10 + 머리 6 + 피부 6 + 금속 5 + 백열 1 |
| 무기 | 화염 창 1자루. 잎사귀형 좌우대칭 창날 + 곧은 자루, 자루에 희미한 룬 각인 |
| 무기 구조 | 자루는 3D 상 하나의 직선·균일 두께. 어깨 뒤로 물러나 깊이감을 만든다 |

`_WEAPON` 블록에 구조 정확도를 따로 명시한다 — 이전 검 버전에서 날이 미묘하게 휘어 나왔다.

## 파일

`ignis_spec.py` 가 팔레트와 프롬프트 전부를 들고 있다.
`build_workflow.py` / `finalize.py` 는 Astra 쪽 공용 구현을 import 한다.

```bash
python build_workflow.py
python finalize.py codex    01_Ignis_Codex_Raw.png      01_Ignis_Codex_Pixel_Final.png
python finalize.py cutscene 02_Ignis_Ultimate_Raw_A.png 02_Ignis_Ultimate_Pixel_Final.png
```
