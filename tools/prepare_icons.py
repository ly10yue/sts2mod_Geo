"""Reproducible, distinct crops of credited song illustrations; never edit the originals."""
from pathlib import Path
import hashlib, json, re, shutil
from PIL import Image, ImageDraw, ImageFont, ImageOps

ROOT=Path(__file__).resolve().parents[1]

def main():
    cards=json.loads((ROOT/'content/artwork-map.json').read_text(encoding='utf-8'))['cards']
    by_id={c['className'].removeprefix('Geopelia'):c for c in cards}
    associations=dict(Aerial='BelieveLight',Hate='Hate',Archidoxen='SustainAmplifier',Richter='OpeningTheme',
        SignalCapacity='Afterglow',FutureRestart='SignalSingularity',NextRepeat='DoubleExposure',
        PulseGuard='Polyphony',OverdriveGuard='Interest',EchoSignal='Mastering',WorldEcho='WorldEcho',
        SignalEcho='Feast',Beacon='RainbowSignal',DebtDraw='Tachycardia',TapeLoop='Rehearsal',
        EndlessSignal='HappyDisease',EnergyDebt='BorrowedLight',DrawDebt='Flick',SignalDebt='OpeningBeat',
        DamageDebt='DistortedFate',BlockDebt='DelayBeat',Muted='TimeMortgage',FragileGuard='DanceWithSilence',
        Signal='Sync',Rhythm='Counterbeat',RhythmDraw='Tap',EchoGuard='ReplayFootnote',
        SignalDraw='CarrierWave',OpeningSignal='Probe',JudgmentLine='OldRecording',ScoreFragment='Rest',
        StardustBadge='PhantomEnsemble',PerfectFrame='LastTransmission')
    slug=lambda s:re.sub(r'(?<!^)(?=[A-Z])','_',s).lower()
    powers=re.findall(r'public sealed class (Geopelia\w+Power)\s*:',(ROOT/'src/Powers.cs').read_text(encoding='utf-8'))
    targets=set((ROOT/'godot/PhigrosJiu/icons').rglob('*.png'))
    targets.update(ROOT/'godot/PhigrosJiu/icons/powers'/f'{slug(p)}.png' for p in powers)
    targets.add(ROOT/'godot/PhigrosJiu/icons/powers/geopelia_endless_signal_enchantment.png')
    used=set();rows=[];thumbs=[]
    for path in sorted(targets):
        key=''.join(x.title() for x in path.stem.removeprefix('geopelia_').removesuffix('_power').split('_'))
        source=by_id.get(associations.get(key,''))
        if source is None or source['className'] in used:
            source=next(c for c in cards if c['className'] not in used)
        used.add(source['className'])
        image=Image.open(ROOT/source['sourceFile']).convert('RGB');w,h=image.size
        # Center on the central subject rather than shrinking a full landscape into an unreadable icon.
        size=int(min(w,h)*.72);box=((w-size)//2,(h-size)//2,(w+size)//2,(h+size)//2)
        tile=image.crop(box).resize((120,120),Image.Resampling.LANCZOS)
        canvas=Image.new('RGBA',(128,128));mask=Image.new('L',(120,120));d=ImageDraw.Draw(mask)
        if path.parent.name=='relics':d.rounded_rectangle((1,1,118,118),radius=22,fill=255)
        else:d.ellipse((1,1,118,118),fill=255)
        canvas.paste(tile,(4,4),mask)
        edge=ImageDraw.Draw(canvas)
        if path.parent.name=='relics':edge.rounded_rectangle((4,4,123,123),radius=24,outline='#efd290',width=3)
        else:edge.ellipse((4,4,123,123),outline='#c7e2e9',width=3)
        path.parent.mkdir(parents=True,exist_ok=True);canvas.save(path)
        packed=ROOT/'build/godot/PhigrosJiu/icons'/path.parent.name/path.name
        packed.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(path,packed)
        rows.append(dict(icon=path.relative_to(ROOT).as_posix(),source=source['sourceFile'],song=source['name'],crop=box,
            artist=source['artist'],sourceUrl=source['sourceUrl'],sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
        thumbs.append((canvas,path.stem.removeprefix('geopelia_')))
    assert len({row['sha256'] for row in rows})==len(rows)
    (ROOT/'content/icon-artwork-map.json').write_bytes(json.dumps(rows,ensure_ascii=False,indent=2).encode('utf-8'))
    sheet=Image.new('RGB',(900,((len(thumbs)+5)//6)*166),'#19242c');draw=ImageDraw.Draw(sheet)
    for i,(tile,label) in enumerate(thumbs):
        x=(i%6)*150;y=(i//6)*166;sheet.paste(tile,(x+11,y+4),tile)
        draw.text((x+5,y+135),label[:23],fill='white')
    sheet.save(ROOT/'build/icon-gallery.png')
    print(f'UNIQUE_ICONS={len(rows)} SOURCE_CROPS={len(used)}')

if __name__=='__main__':main()
