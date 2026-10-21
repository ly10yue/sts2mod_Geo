"""Copy verified original art and expose 4:3 AtlasTexture views, without modifying source pixels."""
from pathlib import Path
import ast, hashlib, html, json, re, shutil, struct

ROOT=Path(__file__).resolve().parents[1]
def main():
    manifest=json.loads((ROOT/'content/artwork-map.json').read_text(encoding='utf-8'))
    pack=ROOT/'build/godot/PhigrosJiu';card_dir=pack/'cards';originals=card_dir/'originals'
    originals.mkdir(parents=True,exist_ok=True)
    # Remove only the old top-level portrait aliases; the authoring originals remain archived.
    for p in card_dir.iterdir():
        if p.is_file() and (p.name.endswith('.png') or p.name.endswith('.png.import')):p.unlink()
    for c in manifest['cards']:
        original=ROOT/c['sourceFile'];data=original.read_bytes()
        assert hashlib.sha256(data).hexdigest()==c['sha256'],c['name']
        file=c['songId']+'.png';target=originals/file
        if not target.exists() or target.read_bytes()!=data:shutil.copyfile(original,target)
        w,h=c['width'],c['height']
        if w/h>=4/3:ch=h-h%3;cw=ch*4//3
        else:cw=w-w%4;ch=cw*3//4
        x=(w-cw)//2;y=(h-ch)//2
        texture=f'''[gd_resource type="AtlasTexture" load_steps=2 format=3]

[ext_resource type="Texture2D" path="res://PhigrosJiu/cards/originals/{file}" id="1_art"]

[resource]
atlas = ExtResource("1_art")
region = Rect2({x}, {y}, {cw}, {ch})
filter_clip = true
'''
        (card_dir/c['portrait']).write_text(texture,encoding='utf-8')
    # Read the currently installed peer pack, not a potentially stale source-export directory.
    code=ast.parse((ROOT/'tools/check_pack_coexistence.py').read_text(encoding='utf-8'))
    reader=next(n for n in code.body if isinstance(n,ast.FunctionDef) and n.name=='read_pack')
    scope={'struct':struct,'hashlib':hashlib}
    exec(compile(ast.Module(body=[reader],type_ignores=[]),'pack_reader','exec'),scope)
    peer_path=ROOT.parents[1]/'mods/jiu_2/jiu_2.pck'
    if peer_path.is_file():
        peer=scope['read_pack'](peer_path)
        scene=peer['jiu_2/character/geopelia_visuals.tscn'].decode('utf-8-sig')
        model=peer['jiu_2/character/geopelia_model.png']
        character=pack/'character';character.mkdir(parents=True,exist_ok=True)
        (character/'geopelia_model.png').write_bytes(model)
        scene=scene.replace('res://jiu_2/','res://PhigrosJiu/').replace('load_steps=4','load_steps=5')
        # Keep the root at native scale: the engine resets it in SetScaleAndHue. Scale the body and anchors.
        assert '[node name="Visuals" type="Sprite2D" parent="."]' in scene,'Peer changed visual format; review scale adaptation'
        scene=scene.replace('position = Vector2(0, -200)','position = Vector2(0, -170)')
        scene=scene.replace('Vector2(0, -200), Vector2(0, -204), Vector2(0, -200)',
                            'Vector2(0, -170), Vector2(0, -173.4), Vector2(0, -170)')
        scene=scene.replace('position = Vector2(0, -410)','position = Vector2(0, -348.5)')
        for a,b in [('-250.0','-212.5'),('-410.0','-348.5'),('250.0','212.5'),('-10.0','-8.5')]:
            scene=re.sub(r'(offset_\w+ = )'+re.escape(a)+r'(?=\n|\r)',r'\g<1>'+b,scene)
        scene=scene.replace('texture = ExtResource("2_model")','scale = Vector2(0.85, 0.85)\ntexture = ExtResource("2_model")')
        (character/'geopelia_visuals.tscn').write_text(scene,encoding='utf-8')
        (ROOT/'content/visuals-source.json').write_text(json.dumps(dict(source='mods/jiu_2/jiu_2.pck',scale=0.85,
            peerModelSha256=hashlib.sha256(model).hexdigest(),peerSceneSha256=hashlib.sha256(peer['jiu_2/character/geopelia_visuals.tscn']).hexdigest()),indent=2),encoding='utf-8')
    else:
        # The tracked seed already contains the independent adapted character visuals.
        visual=json.loads((ROOT/'content/visuals-source.json').read_text(encoding='utf-8'))
        model=(pack/'character/geopelia_model.png').read_bytes()
        assert hashlib.sha256(model).hexdigest()==visual['peerModelSha256'], 'Run git lfs pull to restore character art'
    labels={'Basic':'基础','Common':'普通','Uncommon':'罕见','Rare':'稀有','Ancient':'先古'}
    lines=['# 0.5.0 曲名、曲绘与定数对应','',
        '85张牌使用85首不同歌曲及85张不同原曲绘。原图字节保留，游戏通过 AtlasTexture 居中4:3取景，不生成重绘图片。定数使用该曲EZ/HD/IN/AT的最高值；高稀有度优先高定数。','',
        '[曲目信息](https://raw.githubusercontent.com/7aGiven/Phigros_Resource/info/info.tsv) / [定数来源](https://raw.githubusercontent.com/7aGiven/Phigros_Resource/info/difficulty.tsv) / [曲绘资源](https://github.com/7aGiven/Phigros_Resource/tree/illustration)','',
        '| 稀有度 | 牌名/曲名 | 最高定数 | 画师 | 原曲绘 |','| --- | --- | ---: | --- | --- |']
    tiles=[]
    for c in manifest['cards']:
        lines.append(f"| {labels[c['rarity']]} | {c['name']} | {c['constant']} | {c['artist']} | [原图]({c['sourceUrl']}) |")
        tiles.append(f'<article><img loading="lazy" src="{html.escape(c["sourceFile"],quote=True)}"><b>{html.escape(c["name"])}</b><small>{labels[c["rarity"]]} · {c["constant"]} · {html.escape(c["artist"])}</small></article>')
    (ROOT/'ARTWORK.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
    gallery='<!doctype html><meta charset="utf-8"><title>鸠 · 曲绘核对</title><style>body{background:#121720;color:#eee;font:16px system-ui;margin:24px}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(260px,1fr));gap:16px}article{background:#202939;padding:12px;border-radius:12px}img{width:100%;aspect-ratio:4/3;object-fit:cover}b,small{display:block;margin-top:8px}small{color:#b8c5d9}</style><h1>85张卡牌 · 曲名/曲绘/最高谱面定数</h1><main>'+''.join(tiles)+'</main>'
    (ROOT/'ART_GALLERY.html').write_text(gallery,encoding='utf-8')
    print('ARTWORK_PREPARED=85 UNIQUE=85 VISUAL_SCALE=0.85')

if __name__=='__main__':main()
