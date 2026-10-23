"""Local candidate only: upstream checks/stamper/snapshot, no external publication."""
from datetime import datetime, timezone
from pathlib import Path
import hashlib
import json
import shutil
import subprocess
import sys
import tomllib

root = Path(__file__).resolve().parents[1]
index = root / ".tools/content-index"
releases = root / ".tools/content-index-releases"
candidate = root / "work/ecosystem-candidate"
candidate.mkdir(parents=True, exist_ok=True)
sys.path.insert(0, str(index / "tools"))
import check_schema

template = (root / "ecosystem/KsaChinese.toml.in").read_text(encoding="utf-8")
unfilled = tomllib.loads(template)
errors = list(check_schema.validator().iter_errors(unfilled))
if len(errors) != 1 or list(errors[0].path) != ["links", "forums"]:
    raise ValueError("Unfilled template must fail only for its pending real forum URL: " + str(errors))
# A deliberately synthetic thread URL exists ONLY in ignored local fixture inputs.
# This is not a published thread and must never be submitted as listing metadata.
fixture = template.replace("__FORUM_THREAD_URL__", "https://forums.ahwoo.com/threads/1/")
listing = index / "listings/KsaChinese.toml"
listing.write_text(fixture, encoding="utf-8", newline="\n")
results = []
for script in ("check_layout.py", "check_schema.py", "check_index.py", "check_license.py", "check_status.py", "check_images.py"):
    done = subprocess.run([sys.executable, "tools/" + script], cwd=index, text=True, encoding="utf-8", capture_output=True)
    results.append(dict(check=script, exit_code=done.returncode, output=done.stdout + done.stderr))
    print(script, done.returncode, (done.stdout + done.stderr).strip())
    done.check_returncode()

sys.path.insert(0, str(releases / "tools"))
import stamp_release
import build_snapshot
import hosts

authored = candidate / "authored"
generated = candidate / "releases"
for name in ("listings", "packs"):
    (authored / name).mkdir(parents=True, exist_ok=True)
(authored / "listings/KsaChinese.toml").write_text(fixture, encoding="utf-8")
shutil.copy2(index / "listings/StarMap.toml", authored / "listings/StarMap.toml")
(authored / "index-status.toml").write_text("spec_version = 1\n", encoding="utf-8")
shutil.copy2(index / "tags.toml", authored / "tags.toml")
shutil.copytree(releases / "releases/StarMap", generated / "StarMap", dirs_exist_ok=True)
facts = dict(tag="v0.1.6", release_date=datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
             url="https://github.com/kuxiaowo/KSA-CHINESE/releases/download/v0.1.6/KsaChinese-0.1.6.zip",
             prerelease=False, content_type="application/zip",
             changelog="https://github.com/kuxiaowo/KSA-CHINESE/releases/tag/v0.1.6")
archive = root / "dist/KsaChinese-0.1.6.zip"
asset, _ = hosts.GitHubHost("kuxiaowo/KSA-CHINESE", None, listing_id="KsaChinese")._asset({
    "tag_name": "v0.1.6",
    "assets": [{"name": name, "state": "uploaded"} for name in
               ("KsaChinese-0.1.6-5638-Portable.zip", archive.name)]})
if asset is None or asset["name"] != archive.name:
    raise ValueError("The watcher did not select the standard mod archive")
versions = releases / "game-versions.json"
document = stamp_release.stamp(tomllib.loads(fixture), facts, archive.read_bytes(),
                               json.loads(versions.read_text(encoding="utf-8"))["versions"])
if document["install"]["root"] != "KsaChinese" or document["loader"]["min"] != "0.4.8":
    raise ValueError("Unexpected derived installation/loader")
(generated / "KsaChinese").mkdir(exist_ok=True)
(generated / "KsaChinese/0.1.6.json").write_text(stamp_release.serialize(document), encoding="utf-8")
snapshot = build_snapshot.build(authored, generated, versions)
(candidate / "index.json").write_text(build_snapshot.serialize(snapshot), encoding="utf-8")
report = dict(upstream_checks=results, install=document["install"], loader=document["loader"],
              standard_archive_selected_over_portable=True,
              archive_sha256=hashlib.sha256(archive.read_bytes()).hexdigest(),
              real_forum_url_pending=True, fixture_forum_url_used_only_locally=True,
              final_listing_schema_passed=False, public_index_submitted=False)
(root / "reports/ecosystem-metadata-validation.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print("Local candidate snapshot ready; real forum URL and external approval remain pending.")
