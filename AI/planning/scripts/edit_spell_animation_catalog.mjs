import fs from 'node:fs/promises';
import {createRequire} from 'node:module';
import {pathToFileURL} from 'node:url';
const {FileBlob,SpreadsheetFile}=await import(pathToFileURL(createRequire(import.meta.url).resolve('@oai/artifact-tool')).href);
const out='Recordings/SpellAnimationRevision/Planning';
await fs.mkdir(out,{recursive:true});
const w=await SpreadsheetFile.importXlsx(await FileBlob.load('Assets/_Project/Scripts/Skill_Catalog.xlsx'));
const edit=process.argv[2]==='edit';
if(edit){
 const set=(s,c,v)=>w.worksheets.getItem(s).getRange(c).values=[[v]];
 set('Skills','I2','번개를 3회 연속으로 내립니다. 4·8각성마다 같은 패턴의 낙뢰가 1회 추가됩니다.');
 set('Parameters','D2',.5);set('Parameters','E2',2/12);
 set('Targeting','C2','일반: 첫 중심 이후 0.8 이내 분산, 낙뢰마다 반경 0.55. 조준 반경 1.35.\n약 0.167초 간격으로 기본 3회, A4 4회, A8 이상 5회. 천벌 연출 유지.');
 set('Targeting','C9','착탄 지점 고정. 시전 마법진 없음.\n2.4초 가속 낙하, 접촉 후 0.09초에 피해. 잔열 2회.');
 const guide=w.worksheets.getItem('Guide'),rows=guide.getUsedRange().values;
 for(let i=0;i<rows.length;i++){
  if(rows[i][0]==='버전')set('Guide',`B${i+1}`,'게임 0.13.1 운영 개정 2026-09-20. 9종, ID 0–5·7–9. 삭제된 ID 6은 재사용하지 않습니다.');
  if(rows[i][0]==='외부 원본')set('Guide',`B${i+1}`,'ExternalAssets 원본은 보존합니다. 제작 출처: AI/comfyui/mage-vfx/revision1~5. 일반 라이트닝은 최초 ThunderEffects 원화 복원. 운석은 Comfy 표면 마스터를 회전·화염 흐름·파편 48프레임으로 가공했습니다.');
 }
 w.recalculate();
 const exportDir='outputs/spell-animation-20260920';await fs.mkdir(exportDir,{recursive:true});
 await(await SpreadsheetFile.exportXlsx(w)).save(exportDir+'/Skill_Catalog.xlsx');
}
for(const [s,r]of [['Skills','C1:I2'],['Parameters','A1:F2'],['Targeting','A1:D2'],['Targeting','A8:D10'],['Guide','A17:B20']]){
 const blob=await w.render({sheetName:s,range:r,scale:1,format:'png'});
 await fs.writeFile(`${out}/${edit?'after':'before'}-${s}-${r.replace(':','-')}.png`,new Uint8Array(await blob.arrayBuffer()));
}
console.log(edit?'Edited and rendered catalog':'Rendered existing catalog');
