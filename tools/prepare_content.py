"""Sync authored content while keeping the peer mod completely read-only.

Card art already exists in build/godot/PhigrosJiu. This script performs no raster edits.
Namespaced IDs belong to the source localization and remain stable when card effects change.
"""
from pathlib import Path
import json
import shutil
import card_design
import generate_design_assets
import prepare_artwork
import prepare_resources

root = Path(__file__).resolve().parents[1]
prepare_resources.main()
prepare_artwork.main()
card_design.main()
generate_design_assets.main()
pack = root / "build/godot/PhigrosJiu"
cards = json.loads((root / "content/cards.json").read_text(encoding="utf-8-sig"))
if len({c["entry"] for c in cards}) != len(cards):
    raise ValueError("Duplicate card identities")
for c in cards:
    if not c["entry"].startswith("PHIGROS_JIU_"):
        raise ValueError("Main card identity lacks namespace: " + c["entry"])
    if not (pack / "cards" / c["portrait"]).is_file():
        raise FileNotFoundError(c["portrait"])
for source in (root / "localization").rglob("*.json"):
    target = pack / "localization" / source.relative_to(root / "localization")
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(source, target)
(pack / "cards.json").write_text(json.dumps(cards, ensure_ascii=False, indent=2), encoding="utf-8")
print(f"PHIGROS_JIU_CONTENT_OK cards={len(cards)} namespace=PHIGROS_JIU_")
