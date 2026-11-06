# Copyright (c) 2026 Navin Ratnayake (@navinrtnk). SPDX-License-Identifier: MIT
"""Fetch and SHA-256 verify the CC0 inputs listed in Assets/Customer/sources.json.

Uses HTTP byte ranges to extract selected ZIP members without downloading the
whole 267 MB asset pack. Inputs stay in the ignored .tools/customer-source folder.
No third-party Python packages are required.
"""
from pathlib import Path
import hashlib
import json
import struct
import urllib.request
import zlib

ROOT = Path(__file__).resolve().parents[1]
DESTINATION = ROOT / ".tools" / "customer-source"


class RemoteZip:
    def __init__(self, url):
        self.url = url
        request = urllib.request.Request(url, method="HEAD")
        with urllib.request.urlopen(request, timeout=60) as response:
            size = int(response.headers["Content-Length"])
        tail = self.read(max(0, size-65536), size-1)
        end = tail.rfind(b"PK\x05\x06")
        if end < 0: raise ValueError("ZIP directory record was not found")
        record = struct.unpack_from("<4s4H2LH", tail, end)
        central = self.read(record[6], record[6]+record[5]-1)
        self.members = {}
        offset = 0
        while central[offset:offset+4] == b"PK\x01\x02":
            method = struct.unpack_from("<H", central, offset+10)[0]
            crc, packed, size = struct.unpack_from("<LLL", central, offset+16)
            name_len, extra_len, comment_len = struct.unpack_from("<HHH", central, offset+28)
            start = struct.unpack_from("<L", central, offset+42)[0]
            name = central[offset+46:offset+46+name_len].decode("utf-8")
            self.members[name] = (method, crc, packed, size, start)
            offset += 46+name_len+extra_len+comment_len

    def read(self, first, last):
        request = urllib.request.Request(self.url, headers={"Range": f"bytes={first}-{last}"})
        with urllib.request.urlopen(request, timeout=60) as response:
            if response.status != 206: raise ValueError("Asset host did not honor the byte-range request")
            result = response.read()
        if len(result) != last-first+1: raise ValueError("Incomplete asset download")
        return result

    def extract(self, member):
        method, crc, packed, size, start = self.members[member]
        header = self.read(start, start+29)
        name_len, extra_len = struct.unpack_from("<HH", header, 26)
        offset = start+30+name_len+extra_len
        data = self.read(offset, offset+packed-1)
        if method == 8: data = zlib.decompress(data, -15)
        elif method != 0: raise ValueError(f"Unsupported ZIP method: {method}")
        if len(data) != size or zlib.crc32(data) != crc: raise ValueError("ZIP integrity check failed")
        return data


if __name__ == "__main__":
    manifest = json.loads((ROOT / "Assets" / "Customer" / "sources.json").read_text())
    DESTINATION.mkdir(parents=True, exist_ok=True)
    archives = {}
    for source in manifest["inputs"]:
        name = source.get("file") or Path(source["member"]).name
        if Path(name).name != name: raise ValueError("Input filename must be a basename")
        target = DESTINATION / name
        expected = source["sha256"]
        if target.exists() and hashlib.sha256(target.read_bytes()).hexdigest() == expected:
            print("Verified", name)
            continue
        if "archive" in source:
            url = source["archive"]
            if url not in archives: archives[url] = RemoteZip(url)
            data = archives[url].extract(source["member"])
        else:
            with urllib.request.urlopen(source["url"], timeout=60) as response: data = response.read()
        if hashlib.sha256(data).hexdigest() != expected: raise ValueError(f"SHA-256 mismatch: {name}")
        target.write_bytes(data)
        print("Downloaded and verified", name)
