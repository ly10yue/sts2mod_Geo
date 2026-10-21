"""Require one exact original illustration per song/card, increasing rarity bands and scaled peer visuals."""
from pathlib import Path
import hashlib,json,re
root=Path(__file__).resolve().parents[1]
m=json.loads((root/'content/artwork-map.json').read_text(encoding='utf-8'))
cards=json.loads((root/'content/cards.json').read_text(encoding='utf-8'))
art={r['className']:r for r in m['cards']}
checks=0
def check(test,message):
    global checks
    checks+=1
    assert test,message
for key in ['songId','name','sha256','portrait']:
    check(len({c[key] for c in m['cards']})==85,'Duplicate artwork '+key)
for c in cards:
    a=art[c['className']]
    check(c['name']==a['name'] and c['songId']==a['songId'],'Card/song mismatch')
    lo,hi=m['bands'][c['rarity']]
    check(lo<=a['constant']<=hi,'Song constant outside rarity band')
    source=(root/a['sourceFile']).read_bytes()
    packed=(root/'build/godot/PhigrosJiu/cards/originals'/(a['songId']+'.png')).read_bytes()
    check(hashlib.sha256(source).hexdigest()==a['sha256'],'Original image changed')
    check(source==packed,'Packed image differs from original')
    atlas=(root/'build/godot/PhigrosJiu/cards'/c['portrait']).read_text(encoding='utf-8')
    check('originals/'+a['songId']+'.png' in atlas,'Atlas references wrong song')
allowed_keywords={'Exhaust','Ethereal','Innate','Retain','Unplayable','Sly','Eternal'}
check(all(set(c['keywords'])<=allowed_keywords for c in cards),'Unsupported card keyword')
v=json.loads((root/'content/visuals-source.json').read_text(encoding='utf-8'))
check(hashlib.sha256((root/'build/godot/PhigrosJiu/character/geopelia_model.png').read_bytes()).hexdigest()==v['peerModelSha256'],'Peer model pixel identity')
scene=(root/'build/godot/PhigrosJiu/character/geopelia_visuals.tscn').read_text(encoding='utf-8')
check('res://jiu_2/' not in scene,'Runtime dependency on peer resource root')
check('scale = Vector2(0.85, 0.85)' in scene,'Visual scale')
report=dict(result='PASS',checks=checks,cards=85,uniqueSongs=85,uniqueImages=85,exhaustCards=sum('Exhaust' in c['keywords'] for c in cards),visualScale=0.85,
    bands={r:dict(count=len(values:=[a['constant'] for a in m['cards'] if a['rarity']==r]),minimum=min(values),maximum=max(values)) for r in m['bands']})
(root/'content/artwork-audit.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report))
