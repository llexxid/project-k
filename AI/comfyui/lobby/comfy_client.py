"""Small stdlib client for the configured Comfy Cloud MCP. Credentials never enter artifacts/logs."""
import json,sys,time,urllib.request,urllib.error,urllib.parse
from pathlib import Path
class Comfy:
 def __init__(self):
  credentials=json.loads((Path.home()/'.claude/.credentials.json').read_text())
  entry=next(v for k,v in credentials['mcpOAuth'].items() if k.startswith('comfy'))
  self.token=entry['accessToken'];self.url=entry['serverUrl'];self.session=None;self.seq=0
  if entry.get('expiresAt',0) < (time.time()+60)*1000:self.refresh()
  self.rpc('initialize',{'protocolVersion':'2024-11-05','capabilities':{},'clientInfo':{'name':'project-k-lobby-assets','version':'1.0'}})
 def refresh(self):
  path=Path.home()/'.claude/.credentials.json';data=json.loads(path.read_text('utf-8'))
  key=next(k for k in data['mcpOAuth'] if k.startswith('comfy'));entry=data['mcpOAuth'][key]
  with urllib.request.urlopen('https://cloud.comfy.org/.well-known/oauth-authorization-server') as r:metadata=json.load(r)
  body=urllib.parse.urlencode({'grant_type':'refresh_token','refresh_token':entry['refreshToken'],'client_id':entry['clientId']}).encode()
  req=urllib.request.Request(metadata['token_endpoint'],data=body,headers={'Content-Type':'application/x-www-form-urlencoded'})
  with urllib.request.urlopen(req) as r:token=json.load(r)
  data=json.loads(path.read_text('utf-8'));entry=data['mcpOAuth'][key]
  entry['accessToken']=token['access_token'];entry['refreshToken']=token.get('refresh_token',entry['refreshToken'])
  entry['expiresAt']=int((time.time()+token.get('expires_in',3600))*1000)
  path.write_text(json.dumps(data),encoding='utf-8');self.token=entry['accessToken']
 def rpc(self,method,params):
  self.seq+=1
  headers={'Authorization':'Bearer '+self.token,'Content-Type':'application/json','Accept':'application/json, text/event-stream'}
  if self.session:headers['Mcp-Session-Id']=self.session
  req=urllib.request.Request(self.url,data=json.dumps({'jsonrpc':'2.0','id':self.seq,'method':method,'params':params}).encode(),headers=headers)
  try:
   with urllib.request.urlopen(req,timeout=180) as r:
    self.session=r.headers.get('Mcp-Session-Id',self.session);raw=r.read().decode()
  except urllib.error.HTTPError as e:raise RuntimeError('Comfy HTTP '+str(e.code)) from None
  if raw.startswith('event:') or raw.startswith('data:'):
   vals=[json.loads(line[5:].strip()) for line in raw.splitlines() if line.startswith('data:')]
   obj=next(v for v in reversed(vals) if 'result' in v or 'error' in v)
  else: obj=json.loads(raw)
  if 'error' in obj:raise RuntimeError(obj['error'])
  return obj.get('result',{})
 def call(self,name,args):return self.rpc('tools/call',{'name':name,'arguments':args})
if __name__=='__main__':
 c=Comfy()
 if sys.argv[1]=='list':out=c.rpc('tools/list',{})
 else:out=c.call(sys.argv[1],json.loads(Path(sys.argv[2]).read_text('utf-8')) if len(sys.argv)>2 else {})
 print(json.dumps(out,ensure_ascii=False,indent=2))
