from math import ceil,comb
import json
rows=[]
for name,p,f in [('AllSkills',.05,30)]:
 for owned in [False,True]:
  copies=ceil(55/f)+(0 if owned else 1)
  def quantile(q):
   for n in range(copies,2001):
    cdf=1-sum(comb(n,k)*p**k*(1-p)**(n-k) for k in range(copies))
    if cdf>=q:return n
  rows.append(dict(roster=name,individualProbability=p,duplicateFragments=f,alreadyOwned=owned,drawsNeeded=copies,meanPulls=copies/p,medianPulls=quantile(.5),p90Pulls=quantile(.9),meanWeeksAt21=copies/p/21,p90WeeksAt21=quantile(.9)/21))
p='.utmp/catalog-integration/mage-validation/investment.json'
with open(p,'w',encoding='utf8')as out:json.dump({'method':'Exact negative-binomial distribution, 55 total fragments, no first-clear bonuses or pity, 21 mage pulls per week (all recurring coins allocated to mage)','rows':rows},out,ensure_ascii=False,indent=2)
print(json.dumps(rows,indent=2))
