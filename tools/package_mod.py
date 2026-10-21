"""Publish the freshly built staging candidate into the tracked install folder."""
from pathlib import Path
import hashlib
import json
import shutil

ROOT = Path(__file__).resolve().parents[1]


def main():
    inputs = {
        'PhigrosJiu.dll': ROOT / 'src/bin/Release/net9.0/PhigrosJiu.dll',
        'PhigrosJiu.pck': ROOT / 'build/staging/PhigrosJiu.pck',
        'PhigrosJiu.json': ROOT / 'content/PhigrosJiu.json',
    }
    for path in inputs.values():
        if not path.is_file():
            raise FileNotFoundError('Run build.ps1 -NoInstall successfully before packaging: ' + str(path))
    newest_source = max(p.stat().st_mtime for folder in ('src', 'tools', 'localization')
                        for p in (ROOT / folder).rglob('*')
                        if p.is_file() and not any(part in ('bin', 'obj', '__pycache__') for part in p.parts))
    if min(inputs[n].stat().st_mtime for n in ('PhigrosJiu.dll', 'PhigrosJiu.pck')) < newest_source:
        raise ValueError('Sources changed after the candidate was built; rebuild before packaging.')
    output = ROOT / 'dist/PhigrosJiu'
    output.mkdir(parents=True, exist_ok=True)
    for name, path in inputs.items():
        shutil.copyfile(path, output / name)
    # Git stores JSON as LF; publish those exact bytes so hashes survive a fresh checkout.
    (output/'PhigrosJiu.json').write_bytes(inputs['PhigrosJiu.json'].read_text(encoding='utf-8-sig').encode('utf-8'))
    for name in ('README.md', 'CREDITS.md', 'ARTWORK.md', 'DESIGN.md', 'VALIDATION.md', 'ECONOMY.md', 'COEXISTENCE.md'):
        shutil.copyfile(ROOT / name, output / name)
    manifest = dict(version=json.loads(inputs['PhigrosJiu.json'].read_text(encoding='utf-8-sig'))['version'],
                    text_hash_format='UTF-8 with LF line endings',
                    design_sha256=hashlib.sha256((ROOT / 'content/cards.json').read_text(encoding='utf-8-sig').encode('utf-8')).hexdigest(),
                    source_design_sha256=hashlib.sha256((ROOT / 'tools/card_design.py').read_text(encoding='utf-8-sig').encode('utf-8')).hexdigest(),
                    files={name:dict(bytes=(output/name).stat().st_size,sha256=hashlib.sha256((output/name).read_bytes()).hexdigest())
                           for name in inputs})
    (ROOT / 'dist/manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('PACKAGED_MOD=' + str(output))


if __name__ == '__main__':
    main()
