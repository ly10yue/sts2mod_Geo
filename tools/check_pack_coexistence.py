"""Verify installed/staged packs and localization can be mounted in either order."""
from pathlib import Path
import hashlib
import json
import struct
import sys

ROOT = Path(__file__).resolve().parents[1]
GAME = ROOT.parents[1]
pack_arg = next((a for a in sys.argv[1:] if not a.startswith("--")), None)
main = Path(pack_arg) if pack_arg else GAME / "mods/PhigrosJiu/PhigrosJiu.pck"
peer = GAME / "mods/jiu_2/jiu_2.pck"

def read_pack(path):
    data = path.read_bytes()
    assert data[:4] == b"GDPC", path
    version = struct.unpack_from("<I", data, 4)[0]
    assert version in [2, 3], version
    base = struct.unpack_from("<Q", data, 24)[0]
    cursor = struct.unpack_from("<Q", data, 32)[0] if version == 3 else 100
    count = struct.unpack_from("<I", data, cursor)[0]
    cursor += 4
    entries = {}
    for _ in range(count):
        size = struct.unpack_from("<I", data, cursor)[0]
        cursor += 4
        name = data[cursor:cursor+size].rstrip(b"\0").decode("utf-8").removeprefix("res://")
        cursor += size
        offset, size = struct.unpack_from("<QQ", data, cursor)
        checksum = data[cursor+16:cursor+32]
        cursor += 36
        blob = data[base+offset:base+offset+size]
        assert hashlib.md5(blob).digest() == checksum, (path, name)
        assert name not in entries, (path, name)
        entries[name] = blob
    return entries

a, b = read_pack(main), read_pack(peer)
overlap = set(a) & set(b)
assert not overlap, "Resource collisions: " + str(sorted(overlap))
for root, pack in [("PhigrosJiu", a), ("jiu_2", b)]:
    assert all(n.startswith(root + "/") or n.startswith(".godot/imported/") for n in pack)
for lang in ["zhs", "eng"]:
    for table in ["characters", "cards", "powers", "relics"]:
        one = json.loads(a[f"PhigrosJiu/localization/{lang}/{table}.json"])
        two = json.loads(b[f"jiu_2/localization/{lang}/{table}.json"])
        assert not set(one) & set(two), f"Localization collision: {lang}/{table}"
        assert all(k.startswith("PHIGROS_JIU_") for k in one)
        assert all(not k.startswith("PHIGROS_JIU_") for k in two)

# Preserve the peer's current files, even if another project updates it later.
baseline = json.loads((ROOT / "coexistence-baseline.json").read_text(encoding="utf-8"))
changed = [n for n, digest in baseline["peer_hashes_before"].items() if not (GAME/n).is_file() or hashlib.sha256((GAME/n).read_bytes()).hexdigest() != digest]
if "--verify-peer-unchanged" in sys.argv:
    assert not changed, "Peer files changed since baseline: " + str(changed)
print(json.dumps({"result":"PASS", "main_entries":len(a), "peer_entries":len(b), "resource_collisions":len(overlap), "localization_tables_checked":8, "peer_files_unchanged":len(baseline["peer_hashes_before"])-len(changed)}))
