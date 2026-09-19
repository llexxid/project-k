import fs from 'node:fs/promises';
import {createRequire} from 'node:module';
import {pathToFileURL,fileURLToPath} from 'node:url';
const {FileBlob, SpreadsheetFile}=await import(pathToFileURL(createRequire(import.meta.url).resolve('@oai/artifact-tool')).href);
const root=fileURLToPath(new URL('../../../',import.meta.url)).replaceAll('\\','/').replace(/\/$/,'');
const out=`${root}/Recordings/FoundationRevision/Planning`;
const files=['Assets/_Project/Scripts/Skill_Catalog.xlsx','AI/planning/beta-20260913/왕국군키우기_가이드_퀘스트_업적_카탈로그_베타개정.xlsx'];
for(const [i,file] of files.entries()) {
 const target=`${out}/${i===0?'skill':'quest'}-export.xlsx`;
 const w=await SpreadsheetFile.importXlsx(await FileBlob.load(`${root}/${file}`));
 if(i===0) {
  const skills=w.worksheets.getItem('Skills'),p=w.worksheets.getItem('Parameters'),b=w.worksheets.getItem('Bloom'),g=w.worksheets.getItem('Guide');
  skills.getRange('E5').values=[[28]];skills.getRange('G5').values=[[18]];
  skills.getRange('I7').values=[['바위를 연속으로 솟아올려 주변 적을 공격하고 1.4초간 기절시킵니다.']];
  p.getRange('D5:E5').values=[[2.16,.12]];p.getRange('F7').values=[[1.4]];p.getRange('C8').values=[[2.6]];
  b.getRange('I3').values=[[0]];b.getRange('D3').values=[['여러 적: 송곳 8개를 두 번 생성합니다. 각각 피해 50%.\n적 하나: 거대 빙정으로 피해 650%를 줍니다. 기절 효과는 없습니다.']];
  w.worksheets.getItem('Assets').getRange('G9').values=[['']];
  const targeting=w.worksheets.getItem('Targeting');
  targeting.getRange('C5').values=[['별빛은 비행 전반 70% 동안 대상을 추적한 뒤 낙하 지점을 확정\n0.12초 간격 · 18개 기본 낙하 · 비행 0.48초']];
  targeting.getRange('C9').values=[['착탄 지점 고정 · 시전 마법진 없음 · 충돌 후 잔열 2회']];
  const rows=g.getUsedRange().values;
  const replace={
   '버전':'게임 0.13.0 운영 개정 2026-09-20. 9종, ID 0–5·7–9. 삭제된 ID 6은 재사용하지 않습니다.',
   '외부 원본':'ExternalAssets 원본은 보존합니다. 최신 제작은 AI/comfyui/mage-vfx/revision1~4 및 finish-manifest.json에 실제 API·UI 그래프·원본·채택/기각·비용을 기록했습니다. 운석 시전 원과 지속 불길을 제거하고 크레이터를 축소했습니다.'
  };
  for(let r=1;r<rows.length;r++)if(replace[rows[r][0]])g.getRange(`B${r+1}`).values=[[replace[rows[r][0]]]];
  const extra=[
   ['왕국군 공격 계수','시전당 1회 효과량 = round(기본 효과량×1.04^E×(1+0.05A)) + round(출전 3인 ATK 합×기본 효과량/400×(1+0.005E+0.025A)). 사망한 출전 병사도 합산하고 시전 시작 시 고정합니다.'],
   ['수동 시전','자동 OFF에서 우측 스테이지 표시 위에 장착 스킬을 나열합니다. ON이면 대응하는 퇴장 애니메이션. 준비 점등·시계 방향 쿨다운 음영·남은 초를 표시합니다.'],
   ['조준 구도','발 위치 기준 원형 게임 판정과 쿼터뷰 타원 장판 표시를 구분합니다. 분산 스킬인 얼음 송곳·유성우는 드래그 시전과 쿨다운 소비를 하지 않습니다.'],
   ['각성 안내','A4/A8 추가 타격·틱, 다음 각성의 효과량·쿨타임·타격 수, A10 개화 효과를 상세 화면에 표시합니다. 운석은 추가 타격 보너스가 없습니다.'],
   ['성역 연출','반경 2.6. 한 번 상승한 불투명 문양을 유지하며 발밑 지면 파동으로 회복하고 종료 때 한 번 하강합니다.'],
   ['픽셀과 레이어','핵심 VFX 목표 32px/월드 단위, 4~6색·Point·ASTC4×4. 단발 타격은 전면, 독 늪·잔열·흡인 등 지속 장판은 후면입니다. 이펙트가 큰 경우 원본 논리 해상도도 함께 높입니다.']
  ];
  const found=rows.findIndex(r=>r[0]==='왕국군 공격 계수');const start=found<0?rows.length+1:found+1;g.getRange(`A${start}:B${start+extra.length-1}`).values=extra;
  g.getRange(`A${start}:B${start+extra.length-1}`).format={font:{name:'맑은 고딕',size:11},wrapText:true,rowHeight:68};
  w.recalculate();await (await SpreadsheetFile.exportXlsx(w)).save(target);
  for(const [s,r] of [['Skills','A1:I10'],['Parameters','A1:I10'],['Bloom','A1:I3'],['Guide',`A${start}:B${start+5}`]]){
   const png=await w.render({sheetName:s,range:r,scale:1,format:'png'});await fs.writeFile(`${out}/skill-${s}-updated.png`,new Uint8Array(await png.arrayBuffer()));
  }
 } else {
  const s=w.worksheets.getItem('00_개요');
  s.getRange('A1').values=[['가이드 퀘스트 업적 운영 카탈로그']];s.getRange('A2').values=[['2026-09-20 개정 · 기존 시트와 목표 ID 보존 · 무한 진행과 장비 경제 반영']];
  const updates={30:['퀘스트 골격','연결','10001 계열 가이드와 일일·주간·업적 런타임 연결. 서버 배포를 의미하지 않음'],31:['이벤트 발행','연결','처치·클리어·전투 시간·시전·장비 강화 등 승인 이벤트 집계'],32:['누적 진행도','연결','LocalProgression의 최고 기록과 기간 카운터 사용'],33:['보상 지급','연결','로컬 권한 저장의 원자 지급·중복 수령 방지. 서버 이관은 별도 검증'],34:['일일/주간 리셋','연결','KST 일/주 기간 키로 처리'],35:['업적 UI','연결','목록과 완료·수령 상태 연결'],36:['가이드 UI','연결','메인 HUD에서 후속 가이드와 보상 표시'],37:['스테이지','무한 진행','1~3장 도입부 후 산적·고블린·오크 순환. 장 번호와 수치·보상 계속 성장'],38:['장비','개정','강화석 분해·강화, 자동 분해·필터·정렬. 보관함 포화로 전투·뽑기·던전을 차단하지 않음'],40:['재화','개정','Ruby=6, EquipmentStone=7. 강화석은 장비 강화에 사용. 기존 미사용 재화 보존']};
  for(const [row,value]of Object.entries(updates))s.getRange(`A${row}:C${row}`).values=[value];
  s.getRange('A58:E58').merge();s.getRange('A58').values=[['환생 개정은 문답 단계다. 골드·골드 강화 초기화 방향만 확정했으며 보상 유형/양과 목표 ID는 확정 전 변경하지 않는다.']];
  s.getRange('A59:E59').merge();s.getRange('A59').values=[['장비 강화 목표는 동일하게 레벨 증가를 집계한다. 소모 재료만 동일 장비에서 강화석으로 변경하고 분해 자체를 필수 소비 퀘스트로 추가하지 않는다.']];
  s.getRange('A58:E59').format={wrapText:true,rowHeight:46,font:{name:'맑은 고딕',size:11,color:'#48566A'}};
  w.recalculate();await(await SpreadsheetFile.exportXlsx(w)).save(target);
  const png=await w.render({sheetName:'00_개요',range:'A28:E40',scale:1,format:'png'});await fs.writeFile(`${out}/quest-overview-updated.png`,new Uint8Array(await png.arrayBuffer()));
 }
 // Apply the exported workbook after this process exits to avoid Windows file locks.
 console.log('Exported',target,'from',file);
}
