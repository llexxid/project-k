"""Boundary currency detail and representative open panels on the isolated Android QA account."""
from settings_checks import *

def main():
 pause(True,'presentation-pause')
 original=command('presentation-start')['state']['gold']
 checks=[]
 try:
  command('maximum-currency','amount',str(2**63-1))
  for i in range(3):
   style(i,'presentation-format-'+str(i));tap('BtnSaveClose',state('presentation-close-'+str(i)))
   tap('BtnCurrency',state('currency-controls-'+str(i)))
   s=state('currency-maximum-'+str(i));shot('currency-maximum-'+str(i))
   exact=[x for x in s['labels'] if x['text']=='9,223,372,036,854,775,807']
   assert len(exact)==1 and not exact[0]['isTextTruncated'],exact
   assert any(x['text']=='골드' and not x['isTextTruncated'] for x in s['labels'])
   assert any(x['text']=='루비' and not x['isTextTruncated'] for x in s['labels'])
   back();checks.append('exact Int64 detail / '+str(i));print('PASS '+checks[-1],flush=True)
  command('currency-restored','amount',str(original))
  for control,name in [('BtnDevelopment','growth'),('BtnKingdomArmy','army'),('BtnGacha','gacha')]:
   tap(control,state(name+'-controls'))
   for i in range(3):
    style(i,name+'-style-'+str(i));tap('BtnSaveClose',state(name+'-close-'+str(i)))
    s=state(name+'-notation-'+str(i));shot(name+'-notation-'+str(i))
    assert s['main'],s
   back();checks.append(name+' opened during all three notation changes');print('PASS '+checks[-1],flush=True)
 finally:
  command('presentation-restore-currency','amount',str(original))
  (OUT/'presentation-results.json').write_text(json.dumps(dict(passed=len(checks),checks=checks),ensure_ascii=False,indent=2),encoding='utf-8')

if __name__=='__main__':main()
