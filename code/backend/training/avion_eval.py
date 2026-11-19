"""Evaluate the public AVION ViT-L EK100 action-recognition checkpoint (zhaoyue-zephyrus/avion, EPIC-trained on the TRAIN split only) on
EPIC validation-split segments of videos that are on disk. EVALUATION ONLY: nothing is trained or tuned on these videos.

usage: python avion_eval.py --ckpt X.pt --avion-repo E:/samsung_temp/AVION/repo --ann-dir <epic annotations> --data D1 D2 ... [--n 300]
"""
import argparse, csv, glob, os, random, sys, time, types
import numpy as np, torch, cv2

def stub_clip():                                    # avion imports the OpenAI `clip` package only for its zoo loader; we load the checkpoint directly
    for n in ('flash_attn', 'flash_attn.modules', 'flash_attn.modules.mha', 'flash_attn.modules.mlp'):
        if n not in sys.modules: sys.modules[n] = types.ModuleType(n)
    sys.modules['flash_attn.modules.mha'].MHA = object; sys.modules['flash_attn.modules.mlp'].Mlp = object
    if 'clip' not in sys.modules:
        m = types.ModuleType('clip'); m.load = lambda *a, **k: (_ for _ in ()).throw(RuntimeError('clip zoo not used')); sys.modules['clip'] = m

MEAN = np.array([108.3272985, 116.7460125, 104.09373615000001], np.float32)
STD = np.array([68.5005327, 66.6321579, 70.32316305], np.float32)

def label_space(ann_dir):
    vn = set(); rows = []
    for f in ('EPIC_100_train.csv', 'EPIC_100_validation.csv'):
        r = csv.reader(open(os.path.join(ann_dir, f), encoding='utf-8')); next(r)
        for row in r:
            vn.add('{}:{}'.format(int(row[10]), int(row[12])))
    vn = sorted(vn)
    return vn, {v: i for i, v in enumerate(vn)}

def load_model(ckpt, repo, device='cuda', frames=16):
    sys.path.insert(0, repo); stub_clip()
    from avion.models.transformer import VisionTransformer
    from avion.models.model_clip import VideoClassifier
    import pickle
    class Dummy:
        def __init__(self, *a, **k): pass
        def __setstate__(self, s): pass
    class U(pickle.Unpickler):                      # the checkpoint also pickles training args (pandas etc.); only the weights are needed
        def find_class(self, mod, name):
            try: return super().find_class(mod, name)
            except Exception: return type(name, (Dummy,), {})
    pm = types.ModuleType('pm'); pm.Unpickler = U; pm.load = pickle.load; pm.dumps = pickle.dumps
    ck = torch.load(ckpt, map_location='cpu', weights_only=False, pickle_module=pm)
    sd = {k.replace('module.', ''): v for k, v in ck['state_dict'].items()}
    vis = VisionTransformer(224, 14, 1024, 24, 16, 4, output_dim=sd['visual.image_projection'].shape[-1] if 'visual.image_projection' in sd else 768,
                            patch_dropout=0., drop_path_rate=0., num_frames=frames, use_fast_conv1=True, use_flash_attn=False)
    model = VideoClassifier(vis, dropout=0., num_classes=sd['fc_cls.weight'].shape[0])
    rm = {}                                           # the checkpoint was trained with flash-attn layers; map them to the standard layers (same maths)
    for k, v in sd.items():
        k = k.replace('.attn.Wqkv.weight', '.attn.in_proj_weight').replace('.attn.Wqkv.bias', '.attn.in_proj_bias').replace('.mlp.fc1.', '.mlp.c_fc.').replace('.mlp.fc2.', '.mlp.c_proj.')
        rm[k] = v
    res = model.load_state_dict(rm, strict=False); print('load: missing', len(res.missing_keys), res.missing_keys[:4], 'unexpected', len(res.unexpected_keys), res.unexpected_keys[:4])
    assert not res.missing_keys, 'weights did not load'
    return model.to(device).half().eval()

def read_clip(path, t0, t1, fps_hint=None, n=16, size=224):
    cap = cv2.VideoCapture(path); fps = cap.get(cv2.CAP_PROP_FPS) or 60.0
    idx = np.linspace(t0 * fps, max(t0 * fps, t1 * fps - 1), n).astype(int); out = []
    for i in idx:
        cap.set(cv2.CAP_PROP_POS_FRAMES, int(i)); ok, fr = cap.read()
        if not ok: fr = out[-1] if out else np.zeros((size, size, 3), np.uint8)
        else:
            fr = cv2.cvtColor(fr, cv2.COLOR_BGR2RGB); h, w = fr.shape[:2]; s = size / min(h, w)
            fr = cv2.resize(fr, (max(size, round(w * s)), max(size, round(h * s))), interpolation=cv2.INTER_AREA)
            h, w = fr.shape[:2]; y, x = (h - size) // 2, (w - size) // 2; fr = fr[y:y + size, x:x + size]
        out.append(fr)
    cap.release()
    a = (np.stack(out).astype(np.float32) - MEAN) / STD              # T,H,W,C
    return torch.from_numpy(a).permute(3, 0, 1, 2)                   # C,T,H,W

def ts(t): h, m, s = t.split(':'); return int(h) * 3600 + int(m) * 60 + float(s)

if __name__ == '__main__':
    ap = argparse.ArgumentParser(); ap.add_argument('--ckpt'); ap.add_argument('--avion-repo'); ap.add_argument('--ann-dir'); ap.add_argument('--data', nargs='+')
    ap.add_argument('--n', type=int, default=300); ap.add_argument('--seed', type=int, default=0)
    a = ap.parse_args()
    vns, v2i = label_space(a.ann_dir)
    verb_of = np.array([int(v.split(':')[0]) for v in vns]); noun_of = np.array([int(v.split(':')[1]) for v in vns])
    files = {}
    for d in a.data:
        for f in glob.glob(os.path.join(d, '**', '*.MP4'), recursive=True) + glob.glob(os.path.join(d, '**', '*.mp4'), recursive=True):
            if '.part' not in f: files[os.path.basename(f).split('.')[0]] = f
    segs = []
    r = csv.reader(open(os.path.join(a.ann_dir, 'EPIC_100_validation.csv'), encoding='utf-8')); next(r)
    for row in r:
        if row[2] in files: segs.append((row[2], ts(row[4]), ts(row[5]), int(row[10]), int(row[12])))
    random.Random(a.seed).shuffle(segs); segs = segs[:a.n]
    print(len(files), 'videos on disk;', len(segs), 'validation segments evaluated')
    model = load_model(a.ckpt, a.avion_repo)
    hv = hn = ha = 0; t0 = time.time()
    for k, (vid, s, e, v, nn) in enumerate(segs):
        x = read_clip(files[vid], s, e).unsqueeze(0).cuda().half()
        with torch.no_grad(): p = torch.softmax(model(x).float(), -1)[0].cpu().numpy()
        pv = np.bincount(verb_of, weights=p, minlength=97).argmax(); pn = np.bincount(noun_of, weights=p, minlength=300).argmax()
        hv += int(pv == v); hn += int(pn == nn); ha += int(vns[int(p.argmax())] == '{}:{}'.format(v, nn))
        if (k + 1) % 25 == 0: print(f'{k + 1}/{len(segs)} verb {hv / (k + 1):.3f} noun {hn / (k + 1):.3f} action {ha / (k + 1):.3f}  ({time.time() - t0:.0f}s)', flush=True)
    n = len(segs); print(f'FINAL n={n} verb top1 {hv / n:.3f} noun top1 {hn / n:.3f} action top1 {ha / n:.3f}')
