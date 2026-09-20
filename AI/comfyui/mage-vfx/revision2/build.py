"""Reference-conditioned ice master; actual API and editable Cloud graph are preserved."""
import json
import sys
from pathlib import Path

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'lobby'))
from comfy_client import Comfy

OUT = Path(__file__).resolve().parent


def save(name, value):
    (OUT / (name + '.json')).write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding='utf8')


def unpack(response):
    if response.get('isError'):
        raise RuntimeError(response)
    structured = response.get('structuredContent')
    if structured:
        return structured.get('result', structured)
    for block in response.get('content', []):
        if block.get('type') == 'text':
            try:
                return json.loads(block['text'])
            except json.JSONDecodeError:
                continue
    return response


def main():
    prompt = ('Refine this ice spell crystal into a larger, more detailed pixel-art glacier crystal cluster for a quarter-view fantasy RPG. '
        'Keep its icy cyan and pale blue palette, three upward shards and faceted geometry. The central shard is tall and dominant, '
        'the two side shards shorter. Add sharply defined internal crystal planes and fine fracture highlights so that this is a detailed '
        '128 by 160 logical pixel sprite, not the same coarse blocks enlarged. Crisp deliberate pixel clusters, six colors, no outlines, '
        'no soft glow, no blurry edges, no scene, no lettering. Fully opaque solid crystals. Show the complete cluster including its base '
        'and both side shards, with generous empty black margins. Keep a pure black flat background. Elevated three-quarter game view.')
    (OUT / 'prompt.txt').write_text(prompt, encoding='utf8')
    upload = json.loads((OUT / 'upload-response.json').read_text(encoding='utf-8-sig'))
    image = '/'.join(x for x in [upload['subfolder'], upload['name']] if x)
    graph = {}
    def n(i, kind, **inputs):
        graph[str(i)] = {'class_type': kind, 'inputs': inputs}
    n(1, 'UNETLoader', unet_name='qwen_image_edit_fp8_e4m3fn.safetensors', weight_dtype='default')
    n(2, 'CLIPLoader', clip_name='qwen_2.5_vl_7b_fp8_scaled.safetensors', type='qwen_image', device='default')
    n(3, 'VAELoader', vae_name='qwen_image_vae.safetensors')
    n(4, 'LoraLoaderModelOnly', model=['1', 0], lora_name='Qwen-Image-Edit-Lightning-4steps-V1.0-bf16.safetensors', strength_model=1)
    n(5, 'ModelSamplingAuraFlow', model=['4', 0], shift=3)
    n(6, 'CFGNorm', model=['5', 0], strength=1)
    n(7, 'LoadImage', image=image)
    n(8, 'TextEncodeQwenImageEdit', clip=['2', 0], vae=['3', 0], image=['7', 0], prompt=prompt)
    n(9, 'TextEncodeQwenImageEdit', clip=['2', 0], vae=['3', 0], image=['7', 0], prompt='')
    n(10, 'VAEEncode', pixels=['7', 0], vae=['3', 0])
    n(11, 'KSampler', model=['6', 0], positive=['8', 0], negative=['9', 0], latent_image=['10', 0], seed=2026092002, steps=4, cfg=1, sampler_name='euler', scheduler='simple', denoise=1)
    n(12, 'VAEDecode', samples=['11', 0], vae=['3', 0])
    n(13, 'SaveImage', images=['12', 0], filename_prefix='ProjectK_Ice_Qwen_v2')
    save('ice-qwen-api', graph)
    c = Comfy()
    preflight = c.call('submit_workflow', {'workflow': graph, 'dry_run': True})
    save('preflight', preflight)
    if preflight.get('isError'):
        raise RuntimeError('Preflight rejected; inspect preserved response')
    saved = c.call('save_workflow', {'workflow_json': graph, 'name': 'ProjectK MageVFX Ice Qwen v2', 'description': 'Purchased ice sprite reference; coherent high-detail master; fixed four-step graph avoids template proxy default misalignment.'})
    save('cloud-save', saved)
    workflow_id = unpack(saved)['workflow_id']
    reopened = c.call('get_saved_workflow', {'workflow_id': workflow_id})
    save('cloud-workflow-response', reopened)
    save('ice-qwen-ui', unpack(reopened)['workflow_json'])
    request = {'workflow': graph, 'confirm': True}
    save('submitted-request', request)
    result = c.call('submit_workflow', request)
    save('submission', result)
    payload = unpack(result)
    save('manifest', dict(status='submitted', prompt_id=payload.get('prompt_id'), workflow_id=workflow_id,
        source='reference-manifest.json', prompt='prompt.txt', api='ice-qwen-api.json', ui='ice-qwen-ui.json',
        node_schemas='get_node.json; ../../../../Recordings/FoundationRevision/Research/sampling-nodes.json',
        rationale='Qwen conditions on purchased sprite. Direct 13-node graph fixes four-step configuration and removes the stock template proxy/switch machinery whose inspected defaults are misaligned. Single master only; animation will be derived deterministically.',
        seed=2026092002, seed_note='Supported by KSampler; cross-hardware bitwise determinism not assumed.', cost='Pending actual usage report'))
    print(json.dumps(dict(prompt_id=payload.get('prompt_id'), workflow_id=workflow_id)), flush=True)


if __name__ == '__main__':
    main()
