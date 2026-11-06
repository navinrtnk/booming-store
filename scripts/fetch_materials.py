# Copyright (c) 2026 Navin Ratnayake (@navinrtnk). SPDX-License-Identifier: MIT
"""Download the CC0 photographic PBR maps used by the scene from Poly Haven.

Run from any directory with Python 3. No third-party Python dependencies.
"""
import concurrent.futures
import hashlib
import json
from pathlib import Path
import urllib.request

ROOT = Path(__file__).resolve().parents[1] / "Assets" / "Materials"
ASSETS = ("white_oak_veneer", "wood_floor", "marble_01", "white_plaster_02")
MAPS = {"Diffuse": "albedo", "nor_gl": "normal", "Rough": "roughness"}


def download(job):
    asset, kind, entry = job
    target = ROOT / asset / f"{kind}.jpg"
    target.parent.mkdir(parents=True, exist_ok=True)
    if not target.exists() or hashlib.md5(target.read_bytes()).hexdigest() != entry["md5"]:
        request = urllib.request.Request(entry["url"], headers={"User-Agent": "BoomingStore/0.1 asset-fetcher"})
        with urllib.request.urlopen(request, timeout=90) as response:
            data = response.read()
        if hashlib.md5(data).hexdigest() != entry["md5"]:
            raise ValueError(f"Checksum mismatch: {entry['url']}")
        target.write_bytes(data)
    return {"path": str(target.relative_to(ROOT.parent)), "source": entry["url"],
            "sha256": hashlib.sha256(target.read_bytes()).hexdigest(), "license": "CC0-1.0"}


if __name__ == "__main__":
    jobs = []
    for asset in ASSETS:
        request = urllib.request.Request(f"https://api.polyhaven.com/files/{asset}", headers={"User-Agent": "BoomingStore/0.1 asset-fetcher"})
        with urllib.request.urlopen(request, timeout=30) as response:
            metadata = json.load(response)
        for channel, kind in MAPS.items():
            jobs.append((asset, kind, metadata[channel]["4k"]["jpg"]))
    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
        manifest = list(pool.map(download, jobs))
    (ROOT / "sources.json").write_text(json.dumps(manifest, indent=2) + "\n")
    print(f"Verified {len(manifest)} CC0 4K material maps.")
