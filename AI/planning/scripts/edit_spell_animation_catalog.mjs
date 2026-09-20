import fs from 'node:fs/promises';
import {createRequire} from 'node:module';
import {pathToFileURL} from 'node:url';
const {FileBlob,SpreadsheetFile}=await import(pathToFileURL(createRequire(import.meta.url).resolve('@oai/artifact-tool')).href);
const out=process.env.SPELL_CATALOG_OUTPUT || 'Recordings/BloomRevision/Planning';
await fs.mkdir(out,{recursive:true});
const w=await SpreadsheetFile.importXlsx(await FileBlob.load('Assets/_Project/Scripts/Skill_Catalog.xlsx'));
const edit=process.argv[2]==='edit';
if(edit){
 const set=(s,c,v)=>w.worksheets.getItem(s).getRange(c).values=[[v]];
 set('Skills','I2','번개를 3회 연속으로 내립니다. 4·8각성마다 같은 패턴의 낙뢰가 1회 추가됩니다. 낙뢰 위치는 서로 간격을 두고 분산됩니다.');
 set('Parameters','D2',.5);set('Parameters','E2',2/12);
 set('Targeting','C2','일반: 첫 중심 이후 1.15 이내 분산, 기존 낙뢰와 0.45 이상 간격을 우선 확보. 낙뢰마다 반경 0.55, 조준 반경 1.7.\n약 0.167초 간격으로 A0 3회, A4 4회, A8 5회. 매 타격 약한 흔들림, 천벌 타격 때 강한 흔들림.');
 set('Skills','D5','무작위 소범위 폭격');set('Skills','H5',6);
 set('Skills','I5','성역보다 조금 넓은 범위의 무작위 지점에 붉은 별빛을 떨어뜨립니다. 적을 추적하지 않으며 작은 착탄 파동 안에 범위 피해를 줍니다.');
 set('Targeting','B5','전장 중심의 고정 타원 범위 · 적을 지정하거나 추적하지 않음');
 set('Targeting','C5','기본: 반경 3, Y축 0.65배 타원 안 균등 난수. 18개/A4 19개/A8 20개, 0.12초 간격, 비행 0.48초.\n착탄 파동 반경 0.55(Y 0.65배), 최대 6체. 기본형 드래그 불가.\n메테오: 1.5초 낙하, 접촉 후 0.09초에 충돌 피해. 충돌 반경 1.75(Y 0.65배).\n장판 반경 1.25(Y 0.42배), 3.5초, 0.5초마다 피해 200%/총 7틱, 적 60% 감속. 메테오는 드래그 가능.');
 set('Targeting','D5','별빛·운석·폭발 전면 / 작은 파동·갈색 지면·붉은 균열 후면');
 set('Bloom','C5','메테오');set('Bloom','D5','거대한 운석이 1.5초 낙하해 피해 2000%를 줍니다.\n균열 장판이 3.5초 동안 0.5초마다 피해 200%를 주고, 안의 적을 60% 감속합니다.\n쿨타임 1.6배. 장판 밖에서는 약 0.12초 안에 감속이 해제되고 더 약한 기존 감속이 복구됩니다.');
 set('Bloom','F5',1.6);set('Bloom','G5',20);set('Bloom','H1','CrowdOrFieldTickMultiplier');set('Bloom','H5',2);set('Bloom','J5',10);set('Bloom','M5',1.75);set('Bloom','N5',1.75);
 set('Assets','H5','Assets/_Project/Prefabs/VFX/MageTower/StarfallPulse.prefab');
 set('Assets','I5','Assets/_Project/Prefabs/VFX/MageTower/Meteor.prefab');
 const assets=w.worksheets.getItem('Assets');
 assets.getRange('K1:K10').copyFrom(assets.getRange('J1:J10'),'all');
 for(let row=2;row<=10;row++)set('Assets','K'+row,'');
 set('Assets','K1','BloomSecondaryPrefabPath');set('Assets','K5','Assets/_Project/Prefabs/VFX/MageTower/MeteorCrater.prefab');
 assets.getRange('K1:K10').format.columnWidth=assets.getRange('J1:J10').format.columnWidth;
 set('Skills','C9','독립 운석 삭제');set('Skills','D9','ID 3 개화로 통합');set('Skills','I9','ID 8은 비활성 보존 ID입니다. 로스터·장착·뽑기에 나오지 않으며 유성우의 개화 메테오로 통합했습니다.');
 for(const c of ['E9','F9','G9','H9'])set('Skills',c,0);
 set('Bloom','C9','통합됨');set('Bloom','D9','ID 3 메테오 개화 항목을 사용합니다.');
 set('Targeting','B9','비활성 보존 ID');set('Targeting','C9','ID 3 기본 유성우 / 개화 메테오를 참조.');
 for(const c of ['C9','D9','E9','F9','G9','H9','I9','J9'])set('Acquisition',c,0);
 const p=.5/8;
 let p90=3;while(1-Math.pow(1-p,p90)-p90*p*Math.pow(1-p,p90-1)-p90*(p90-1)/2*p*p*Math.pow(1-p,p90-2)<.9)p90++;
 for(const row of [2,3,4,5,6,7,8,10]){set('Acquisition','D'+row,p);set('Acquisition','J'+row,p90);}
 for(const [sheet,last] of [['Skills','I'],['Parameters','O'],['Bloom','N'],['Acquisition','J'],['Assets','J'],['Targeting','D']]){
  const r=w.worksheets.getItem(sheet).getRange('A9:'+last+'9');r.format.fill='#E5E7EB';r.format.font.color='#6B7280';
 }
 w.worksheets.getItem('Targeting').getRange('A5:D5').format.rowHeight=145;
 w.worksheets.getItem('Bloom').getRange('A5:N5').format.rowHeight=110;
 const guide=w.worksheets.getItem('Guide'),rows=guide.getUsedRange().values;
 const guideEdits={
 '버전':'게임 0.14.0 운영 개정 2026-09-20. 활성 8종, ID 0–5·7·9. ID 6·8은 재사용하지 않습니다. 회색 ID 8 행은 통합 이력을 표시합니다.',
 '공통 성장':'E 0~100은 비전 지식, A 0~10은 동일 스킬 파편. A4/A8 기본 타격 +1(화염 회오리 +2틱), A마다 효과량 +5%와 쿨타임 -2%. 메테오는 운석 1회와 3.5초 잔열로 고정됩니다.',
 '확률':'전체 스킬 50%, 비전 지식 10개 30%·20개 15%·50개 5%. 활성 8종은 각각 6.25%입니다.',
 '투자 기간':'각성 누적 55파편. 미보유 특정 스킬은 해금 1회와 중복 2회가 필요하여 평균 48회, 주당 21회 기준 약 2.29주입니다. 기존 파편과 최초 보상은 제외합니다.',
 '확률 편차':`미보유에서 개화까지의 P90은 음이항 계산 기준 ${p90}회입니다. 천장이나 기간 보장은 없습니다.`,
 '특수 개화':'천벌: 2초 예고, 반경 2.1, 최대 10체. 만년빙정: 적 1체면 빙정, 다수면 8개×2회, 기절 없음. 메테오: 유성우 대신 거대 운석·3.5초 균열 장판·적 60% 감속. 세 형태 모두 A10에서 전환합니다.',
 '개화 아이콘':'기본/개화 아이콘과 이름을 전환합니다. 일반 라이트닝은 세 가닥, 천벌은 기존 구도의 보라색 뇌운/낙뢰. 메테오는 운석과 뜨거운 균열, 만년빙정은 여러 송곳과 큰 중심 빙정입니다.',
 '타격 연출':'낙뢰마다 약한 흔들림, 천벌·메테오 충돌 시 절제된 강한 흔들림. 화면 흔들림 설정을 따르며 연속 흔들림을 합산하지 않습니다. 지속 장판·작은 착탄 파동은 후면입니다.',
 '외부 원본':'ExternalAssets 원본 보존. 아트 공정 AI/comfyui/mage-vfx/revision1~6. v6은 기존 도트를 결정론적으로 재조합하고 Comfy의 색상 행렬·16색 양자화로 아이콘을 마감했습니다.',
 '보류 개화':'천벌·만년빙정·메테오 외 5종은 전용 메커니즘 미정이며 피해 또는 회복량이 15% 증가합니다.',
 '조준 구도':'유성우 기본형과 얼음 송곳은 드래그/쿨타임 소비가 없습니다. 메테오는 지점 시전 가능. 작은 파동·메테오 장판은 보이는 타원과 동일한 발 위치 판정을 사용합니다.',
 '각성 안내':'A4/A8 추가 타격·틱과 다음 각성 수치를 표시합니다. 메테오에는 유성우 기본형의 추가 낙하 수를 적용하지 않습니다.',
 '운석 통합':'높은 E/A를 보존. 낮은 E의 실제 비전 지식 지출을 전액 환급, 낮은 A에 쓴 파편과 남은 파편을 합산. 둘 다 보유했으면 중복 보유 30파편 추가. 기존 내역을 보관하고 한 번만 적용. 기존 운석 슬롯은 유성우가 없을 때 교체하며, 이미 장착했다면 해당 슬롯만 비웁니다.'};
 for(let i=0;i<rows.length;i++){
  if(guideEdits[rows[i][0]])set('Guide',`B${i+1}`,guideEdits[rows[i][0]]);
 }
 guide.getRange('A29:B29').copyFrom(guide.getRange('A28:B28'),'all');set('Guide','A29','운석 통합');set('Guide','B29',guideEdits['운석 통합']);guide.getRange('A29:B29').format.rowHeight=110;
 w.recalculate();
 const checks=await w.inspect({kind:'table',range:'Acquisition!A1:J10',include:'values,formulas',tableMaxRows:10,tableMaxCols:10});
 await fs.writeFile(out+'/acquisition-check.ndjson',checks.ndjson);
 const errors=await w.inspect({kind:'match',searchTerm:'#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A|#NUM!|#NULL!|#SPILL!|#CALC!',options:{useRegex:true,maxResults:100},summary:'Final formula scan'});
 await fs.writeFile(out+'/formula-scan.ndjson',errors.ndjson);
 const exportDir=process.env.SPELL_CATALOG_EXPORT || out;await fs.mkdir(exportDir,{recursive:true});
 await(await SpreadsheetFile.exportXlsx(w)).save(exportDir+'/Skill_Catalog.xlsx');
}
for(const [s,r]of [['Skills','C4:I5'],['Bloom','B5:H5'],['Targeting','A5:D5'],['Guide','A25:B29']]){
 const blob=await w.render({sheetName:s,range:r,scale:1,format:'png'});
 await fs.writeFile(`${out}/${edit?'after':'before'}-${s}-${r.replace(':','-')}.png`,new Uint8Array(await blob.arrayBuffer()));
}
console.log(edit?'Edited and rendered catalog':'Rendered existing catalog');
