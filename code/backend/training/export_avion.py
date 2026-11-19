"""Convert the public AVION ViT-L EK100-CLS checkpoint into weights/avion_vitl_ek100_fp16.pt (fp16 state dict, standard layer names, class list inside).
usage: python export_avion.py --ckpt avion_cls_vitl.pt --ann-dir <epic annotations> --out <Backend>/weights/avion_vitl_ek100_fp16.pt"""
import argparse, csv, os, pickle, types
import torch

ap = argparse.ArgumentParser(); ap.add_argument('--ckpt'); ap.add_argument('--ann-dir'); ap.add_argument('--out'); a = ap.parse_args()

class Dummy:
    def __init__(self, *x, **k): pass
    def __setstate__(self, s): pass
class U(pickle.Unpickler):
    def find_class(self, m, n):
        try: return super().find_class(m, n)
        except Exception: return type(n, (Dummy,), {})
pm = types.ModuleType('pm'); pm.Unpickler = U; pm.load = pickle.load; pm.dumps = pickle.dumps
ck = torch.load(a.ckpt, map_location='cpu', weights_only=False, pickle_module=pm)
out = {}
for k, v in ck['state_dict'].items():
    k = k.replace('module.', '').replace('.attn.Wqkv.weight', '.attn.in_proj_weight').replace('.attn.Wqkv.bias', '.attn.in_proj_bias').replace('.mlp.fc1.', '.mlp.c_fc.').replace('.mlp.fc2.', '.mlp.c_proj.')
    out[k] = v.half() if v.is_floating_point() else v
vn = set()
for f in ('EPIC_100_train.csv', 'EPIC_100_validation.csv'):
    r = csv.reader(open(os.path.join(a.ann_dir, f), encoding='utf-8')); next(r)
    for row in r: vn.add('{}:{}'.format(int(row[10]), int(row[12])))
vn = sorted(vn); assert len(vn) == out['fc_cls.weight'].shape[0], (len(vn), out['fc_cls.weight'].shape)
out['label_space'] = [[int(x) for x in v.split(':')] for v in vn]
os.makedirs(os.path.dirname(a.out), exist_ok=True); torch.save(out, a.out); print('saved', a.out, os.path.getsize(a.out) // 2**20, 'MB')
