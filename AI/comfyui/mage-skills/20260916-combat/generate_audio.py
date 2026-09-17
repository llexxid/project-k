"""Two low-cost pilot cues. Schema and exact API/UI submissions are preserved."""
import sys,json
from pathlib import Path
sys.dont_write_bytecode=True;sys.path[:0]=['AI/comfyui/lobby','AI/comfyui/_shared']
from comfy_client import Comfy
import workflow_json
folder=Path(__file__).parent/'Audio-v1';folder.mkdir(exist_ok=True)
def save(name,value):(folder/name).write_text(json.dumps(value,ensure_ascii=False,indent=2),encoding='utf8')
c=Comfy()
prompts={
 'Sword':'One isolated restrained medieval sword attack for a cozy pixel RPG. A short dry steel blade whoosh, then one small muted metallic contact. Weighty but soft, no piercing treble, no long ringing. Starts immediately, ends with a fast natural decay. No music, voice, ambience, reverb, cinematic boom or multiple attacks. Total less than 0.7 seconds.',
 'Heavy':'One isolated heavy wooden club impact for a cozy pixel RPG. A short low woody thud with a subtle leather swish before contact. Rounded compact impact, no sub-bass boom, no crackling, no crushing bones or gore. Dry close recording, starts immediately and decays quickly. No music, voice, ambience, reverb or multiple attacks. Total less than 0.7 seconds.'}
api={}
for i,(key,prompt) in enumerate(prompts.items()):
 node=str(1+i*10);sink=str(2+i*10)
 api[node]={'class_type':'ElevenLabsTextToSoundEffects','inputs':{'text':prompt,'model':'eleven_sfx_v2','model.duration':.7,'model.loop':False,'model.prompt_influence':.6,'output_format':'mp3_44100_192'}}
 api[sink]={'class_type':'SaveAudioAdvanced','inputs':{'audio':[node,0],'filename_prefix':'KingdomIdle/CombatRevision/'+key,'format':'flac'}}
workflow_json.SCHEMA['ElevenLabsTextToSoundEffects']=(['text','model','model.duration','model.loop','model.prompt_influence','output_format'],[],['AUDIO'])
workflow_json.SCHEMA['SaveAudioAdvanced']=(['filename_prefix','format'],['audio'],['AUDIO'])
save('prompts.json',prompts);save('workflow.api.json',api);save('workflow.ui.json',workflow_json.convert(api))
validation=c.call('submit_workflow',{'workflow':api,'dry_run':True});save('validation.json',validation)
if validation.get('isError'):raise RuntimeError('Inspect preflight; not submitted.')
request={'workflow':api,'confirm':True};save('request.json',request)
result=c.call('submit_workflow',request);save('submission.json',result);print(json.dumps(result,ensure_ascii=False))
save('cloud-save.json',c.call('save_workflow',{'workflow_json':api,'name':'KingdomIdle combat audio pilot 20260916','overwrite':False}))
