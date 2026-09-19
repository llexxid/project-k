import fs from 'node:fs/promises';
import { FileBlob, SpreadsheetFile } from '@oai/artifact-tool';
process.on('uncaughtException',error=>{console.error(error.message);process.exit(1);});

const root = 'C:/Users/me/Desktop/KingdomIdle/project-k';
const out = `${root}/Recordings/FoundationRevision/Planning`;
await fs.mkdir(out, {recursive:true});
const mode = process.argv[2] ?? 'preview';
const files = [
  'Assets/_Project/Scripts/Stage_Catalog.xlsx',
  'AI/planning/beta-20260913/Stage_Revised_베타개정.xlsx',
];
for (let index=0; index<files.length; index++) {
  const file=files[index];
  const wb=await SpreadsheetFile.importXlsx(await FileBlob.load(`${root}/${file}`));
  console.log((await wb.inspect({kind:'sheet',include:'id,name',maxChars:2400})).ndjson);
  if(index===1) {
    const plan=wb.worksheets.getItem('베타개정');
    if(mode==='edit') {
      plan.getRange('A1').values=[['왕국군 키우기 · Stage 운영 개정 · 2026-09-20']];
      plan.getRange('A3').values=[['현재 런타임 원본: Assets/_Project/Scripts/Stage_Catalog.xlsx → StageDataGenerator → StageDatabaseSO. 기존 ID 보존.']];
      plan.getRange('A2').values=[['첫 6개 시트는 구 구현 호환 기록이다. 이 시트는 기획 설명·계산용이며 현재 배포 수치의 원본은 런타임 Stage_Catalog.xlsx이다.']];
      plan.getRange('A4').values=[['1~3장 도입부 유지. 3장 보스 이후 4장 진입; 도적 → 고블린 → 오크 테마를 반복하며 장 번호·능력치·보상은 계속 증가.']];
      plan.getRange('L166').values=[['4-1 진입 / 도적 테마 반복']];
      const notes=[
        ['2026-09-20 운영 변경: 무한 진행 · 던전 복귀'],
        ['4장부터 진행번호=(장-1)×11+웨이브. 기준 능력치=원본 초기값×원본 성장률^32.'],
        ['기준 능력치×(진행번호/33)^지수. HP 2 / 공격 1.3 / 골드 1.4 / 경험치 1.3. 보스 배율과 몬스터 역할 계수 유지.'],
        ['초반 33구간의 수치·ID는 유지한다. 후반은 거듭제곱 성장으로 전환해 지수 증가에 따른 조기 수치 초과를 방지한다.'],
        ['4장 이후 보스 최초 완료: 고대 주화 100 + 전직 파편 40. 계정당 해당 장 1회, 환생 후 재지급하지 않는다.'],
        ['원본 Inputs의 endlessHpPower / endlessAtkPower / endlessGoldPower / endlessExpPower 및 보상 입력을 조정한다.'],
        ['던전 성공·실패 모두 결과 표시 후 6초 자동 메인 복귀. 팝업 하단에 남은 초와 줄어드는 바. 수동 복귀·재도전 유지.'],
        ['보스 자동 도전은 하단 스테이지 표시 옆 체크박스. 던전에서는 숨김. 3장 완료 후 자동 도전 설정을 강제로 끄지 않는다.'],
        ['후반 스테이지의 오프라인 보상과 퀘스트 골드도 같은 런타임 능력치·보상 데이터를 사용한다.'],
      ];
      for(let row=487;row<487+notes.length;row++) {
        plan.getRange(`A${row}:P${row}`).copyFrom(plan.getRange(row===487?'A9:P9':'A4:P4'),'all');
        plan.getRange(`A${row}:P${row}`).merge();
        plan.getRange(`A${row}`).values=[notes[row-487]];
      }
      plan.getRange('A487:P487').format={fill:'#24374B',font:{name:'맑은 고딕',size:12,bold:true,color:'#FFFFFF'},rowHeight:28};
      plan.getRange('A488:P495').format={font:{name:'맑은 고딕',size:11,color:'#48566A'},rowHeight:30};
      wb.recalculate();
      await (await SpreadsheetFile.exportXlsx(wb)).save(`${root}/${file}`);
    }
    for(const [name,range] of [['header','A1:P15'],['continuation','A487:P495']]) {
      if(mode!=='edit'&&name==='continuation') continue;
      const preview=await wb.render({sheetName:'베타개정',range,scale:1,format:'png'});
      await fs.writeFile(`${out}/stage-plan-${name}-${mode}.png`,new Uint8Array(await preview.arrayBuffer()));
    }
    continue;
  }
  const inputs=wb.worksheets.getItem('Inputs');
  if(mode==='edit') {
    const values=inputs.getUsedRange().values;
    const start=values.length+1;
    const additions=[
      ['endlessHpPower',2,'지수','34번째 구간부터: 33구간 일반 HP × (진행번호/33)^지수'],
      ['endlessAtkPower',1.3,'지수','이후 공격 성장; 몬스터별 역할 계수 유지'],
      ['endlessGoldPower',1.4,'지수','이후 골드 성장; 보스 배율은 기존 Inputs 적용'],
      ['endlessExpPower',1.3,'지수','이후 경험치 성장; 기존 계정 레벨 상한 유지'],
      ['endlessBossCoins',100,'주화/장','4장 이후 보스 최초 완료 시; 계정당 해당 장 1회'],
      ['endlessBossFragments',40,'전직 파편/장','4장 이후 보스 최초 완료 시; 환생 후 중복 지급 없음'],
    ];
    if(!values.some(row=>row[0]==='endlessHpPower')) {
      inputs.getRange(`A${start}:D${start+additions.length-1}`).copyFrom(inputs.getRange('A49:D54'),'all');
      inputs.getRange(`A${start}:D${start+additions.length-1}`).values=additions;
      inputs.getRange(`B${start}:B${start+additions.length-1}`).setNumberFormat('0.##');
    }
    inputs.getRange('A55:D60').format={font:{name:'맑은 고딕',size:11,color:'#24374B'},rowHeight:32};
    inputs.getRange('B55:B60').format={fill:'#EDF3FF',font:{color:'#1763B1'}};
    if(index===0) {
      const flow=wb.worksheets.getItem('KillCountFlow');
      const rows=flow.getUsedRange().values;
      const col=rows[0].indexOf('DefeatAction');
      if(col<0) throw new Error('DefeatAction column missing');
      for(let row=1;row<rows.length;row++) if(['GoldClear','RubyClear'].includes(rows[row][0]))
        flow.getCell(row,col).values=[['AwaitDefeatChoice']];
    }
    wb.recalculate();
    const result=await SpreadsheetFile.exportXlsx(wb);
    await result.save(`${root}/${file}`);
  }
  const preview=await wb.render({sheetName:'Inputs',autoCrop:'all',scale:1,format:'png'});
  await fs.writeFile(`${out}/stage-inputs-${index}-${mode}.png`,new Uint8Array(await preview.arrayBuffer()));
  console.log((await wb.inspect({kind:'region',sheetId:'Inputs',range:'A49:D60',maxChars:3300})).ndjson);
}
