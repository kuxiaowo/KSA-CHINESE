"""Standard StarMap/Borea archive: one KsaChinese folder, no bundled loader."""
from pathlib import Path
import hashlib
import json
import re
import uuid
import zipfile

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "assets/KsaChinese"
VERSION = re.search(r'^version = "([0-9.]+)"', (ASSETS / "mod.toml").read_text(encoding="utf-8"), re.M)[1]
TOP_FILES = {
    "compatibility.json", "display-sites.json", "FONT-LICENSE.txt",
    "FONT-SOURCE.txt", "language.json", "LICENSE.txt", "mod.toml", "README.txt",
}
SUB_FILES = {"Fonts/KsaUiNotoSC.ttf", "Languages/zh-CN.resx", "Locales/en-US.json", "Locales/zh-CN.json"}
actual = {p.relative_to(ASSETS).as_posix() for p in ASSETS.rglob("*") if p.is_file()}
if actual != TOP_FILES | SUB_FILES:
    raise ValueError(f"Review asset allowlist first: {actual ^ (TOP_FILES | SUB_FILES)}")
payload = {name: (ASSETS / name).read_bytes() for name in sorted(actual)}
payload["KsaChinese.dll"] = (ROOT / "work/build/KsaChinese/KsaChinese.dll").read_bytes()
payload["LICENSE"] = (ROOT / "LICENSE").read_bytes()
payload["THIRD_PARTY.md"] = (ROOT / "THIRD_PARTY.md").read_bytes()
payload["README-生态安装.md"] = (ROOT / "README-生态安装.md").read_bytes()
for name in ("KsaUiLanguages-MIT.txt", "NotoSansSC-OFL.txt"):
    # The font's full license already ships as FONT-LICENSE.txt.
    source = ROOT / "LICENSES" / name
    if source.exists():
        payload["LICENSES/" + name] = source.read_bytes()
manifest = {name: hashlib.sha256(data).hexdigest() for name, data in sorted(payload.items())}
payload["FILES-SHA256.json"] = (json.dumps(manifest, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
archive = ROOT / f"dist/KsaChinese-{VERSION}.zip"
archive.parent.mkdir(exist_ok=True)
temporary = archive.with_suffix("." + uuid.uuid4().hex[:8] + ".tmp")
with zipfile.ZipFile(temporary, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as output:
    for name, data in sorted(payload.items()):
        entry = zipfile.ZipInfo("KsaChinese/" + name, date_time=(2026, 1, 1, 0, 0, 0))
        entry.compress_type = zipfile.ZIP_DEFLATED
        entry.external_attr = 0o100644 << 16
        output.writestr(entry, data, compresslevel=9)
with zipfile.ZipFile(temporary) as check:
    if check.testzip() is not None or len(check.namelist()) != len(set(check.namelist())):
        raise ValueError("Corrupt archive or duplicate entry")
    for name, digest in manifest.items():
        if hashlib.sha256(check.read("KsaChinese/" + name)).hexdigest() != digest:
            raise ValueError("Payload hash mismatch: " + name)
temporary.replace(archive)
digest = hashlib.sha256(archive.read_bytes()).hexdigest()
(archive.parent / f"SHA256SUMS-{VERSION}-Mod.txt").write_text(digest + "  " + archive.name + "\n", encoding="utf-8")
report = dict(archive=archive.name, sha256=digest, files=len(payload), bytes=archive.stat().st_size,
              install_root="KsaChinese", loader_bundled=False, game_binaries_included=False, player_data_included=False)
(ROOT / "reports/mod-package-validation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
print(json.dumps(report))
