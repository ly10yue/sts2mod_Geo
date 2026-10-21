"""Restore the tracked Godot resource seed into the disposable build tree."""
from pathlib import Path
import shutil

ROOT = Path(__file__).resolve().parents[1]


def main():
    source = ROOT / 'godot'
    if not (source / 'project.godot').is_file():
        raise FileNotFoundError('Missing tracked godot/project.godot; pull all repository files first.')
    for folder in (source, ROOT / 'assets/song_illustrations'):
        for image in folder.rglob('*.png'):
            with image.open('rb') as stream:
                if stream.read(42).startswith(b'version https://git-lfs.github.com/spec/'):
                    raise ValueError('Missing Git LFS image data: run git lfs pull before building.')
    shutil.copytree(source, ROOT / 'build/godot', dirs_exist_ok=True)


if __name__ == '__main__':
    main()
