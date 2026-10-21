"""Assign 85 unique songs by their highest chart constant and fetch their original illustrations."""
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor, as_completed
import collections, hashlib, json, struct, time, urllib.parse, urllib.request

ROOT=Path(__file__).resolve().parents[1]
cards=json.loads((ROOT/'content/cards.json').read_text(encoding='utf-8-sig'))
info=[r.split('\t') for r in (ROOT/'research/info.tsv').read_text(encoding='utf-8-sig').splitlines() if '\t' in r]
diff={r[0]:list(map(float,r[1:])) for line in (ROOT/'research/difficulty.tsv').read_text(encoding='utf-8-sig').splitlines() if len(r:=line.split('\t'))>1}
tree=json.loads((ROOT/'research/illustration-tree-050.json').read_text(encoding='utf-8'))
images={r['path']:r for r in tree['tree'] if r['type']=='blob'}
songs={r[0]:dict(songId=r[0],name=r[1].strip(),composer=r[2],artist=r[3],constant=max(diff[r[0]]),charts=diff[r[0]])
       for r in info if r[0] in diff and r[0]+'.png' in images}
bands={'Basic':(0,13.5),'Common':(13.6,14.9),'Uncommon':(15.0,16.2),'Rare':(16.3,17.6),'Ancient':(17.7,30)}
chosen={}; used=set(); titles=set()
# Keep existing names where they already fit the intended rarity and have an exact original.
for c in cards:
    s=songs.get(c.get('songId'));lo,hi=bands[c['rarity']]
    if s and lo<=s['constant']<=hi and s['name'] not in titles:
        chosen[c['className']]=s;used.add(s['songId']);titles.add(s['name'])
preferences={
 'GeopeliaDefend':['Glaciaxion'], 'GeopeliaOpeningBeat':['光'], 'GeopeliaCounterbeat':['HumaN'],
 'GeopeliaIMustSayNo':['NO ONE YES MAN','Disorder'], 'GeopeliaTap':['Bonus Time'],
 'GeopeliaHold':['Chronologika'], 'GeopeliaFlick':['DataErr0r'], 'GeopeliaDrag':['Luminescent'],
 'GeopeliaCarrier':['Electron','Electron'], 'GeopeliaEnvelope':['Magenta Potion'],
 'GeopeliaRush':['FULi AUTO SHOOTER'], 'GeopeliaBorrowShield':['Parallel Retrogression(Game Ver.)'],
 'GeopeliaScavenge':['Get Back'], 'GeopeliaLowpass':['Unorthodox Thoughts'],
 'GeopeliaDistortedFate':['Bounded Quietude'], 'GeopeliaTerabyteConnection':['Retribution'],
 'GeopeliaDiamondEyes':['Reimei'], 'GeopeliaBelieveLight':['Horizon Blue'],
 'GeopeliaBorrowedLight':['energy trixxx'], 'GeopeliaRehearsal':['Better Graphic Animation'],
 'GeopeliaArchiveLost':['望影の方舟Six','Petrichor'],
 'GeopeliaOldRecording':['Desultory Signals'], 'GeopeliaTowerResonance':['Exoplanetary Mirage'],
}
for c in cards:
    if c['className'] in chosen:continue
    lo,hi=bands[c['rarity']]
    available=[s for s in songs.values() if lo<=s['constant']<=hi and s['songId'] not in used and s['name'] not in titles]
    preferred=preferences.get(c['className'],[])
    # Stable metadata order breaks ties; choose harder songs for high rarity.
    available.sort(key=lambda s:(preferred.index(s['name']) if s['name'] in preferred else 100,
                                 -s['constant'] if c['rarity'] in ['Rare','Ancient'] else abs(s['constant']-(lo+hi)/2)))
    assert available,c['className']
    s=available[0];chosen[c['className']]=s;used.add(s['songId']);titles.add(s['name'])

out=ROOT/'assets/song_illustrations';out.mkdir(parents=True,exist_ok=True)
def blob_sha(data):return hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()
def fetch(c):
    s=dict(chosen[c['className']]);filename=s['songId']+'.png';entry=images[filename]
    url='https://raw.githubusercontent.com/7aGiven/Phigros_Resource/illustration/'+urllib.parse.quote(filename)
    dest=out/filename
    data=None
    for candidate in [dest,ROOT/'assets/illustrations'/filename]:
        if candidate.exists():
            candidate_bytes=candidate.read_bytes()
            if blob_sha(candidate_bytes)==entry['sha']:data=candidate_bytes;break
    if data is None:
        for attempt in range(5):
            try:
                data=urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'PhigrosJiu-artwork/0.5'}),timeout=50).read()
                assert blob_sha(data)==entry['sha'],'upstream changed during download'
                break
            except Exception:
                if attempt==4:raise
                time.sleep(2**attempt)
    assert data[:8]==b'\x89PNG\r\n\x1a\n',filename
    dest.write_bytes(data)
    w,h=struct.unpack('>II',data[16:24])
    s.update(className=c['className'],rarity=c['rarity'],oldName=c['name'],sourceUrl=url,
             sourceFile=str(dest.relative_to(ROOT)).replace('\\','/'),sha256=hashlib.sha256(data).hexdigest(),
             gitBlobSha=entry['sha'],width=w,height=h,portrait=c['className'].lower()+'.tres')
    return s
rows={}
with ThreadPoolExecutor(max_workers=6) as pool:
    futures={pool.submit(fetch,c):c for c in cards}
    for i,f in enumerate(as_completed(futures),1):
        r=f.result();rows[r['className']]=r
        if i%10==0 or i==85:print(f'ARTWORK_DOWNLOAD={i}/85',flush=True)
ordered=[rows[c['className']] for c in cards]
assert len({r['songId'] for r in ordered})==len({r['sha256'] for r in ordered})==85
manifest=dict(version='0.5.0',constantRule='maximum chart constant (EZ/HD/IN/AT)',
              metadataSource='https://raw.githubusercontent.com/7aGiven/Phigros_Resource/info/difficulty.tsv',
              illustrationTreeSha=tree['sha'],bands=bands,cards=ordered)
(ROOT/'content/artwork-map.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
for rarity in bands:
    values=[r['constant'] for r in ordered if r['rarity']==rarity]
    print(rarity,len(values),min(values),round(sum(values)/len(values),2),max(values))
