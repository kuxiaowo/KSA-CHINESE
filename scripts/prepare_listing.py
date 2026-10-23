"""Render an external-index document only with a real, approved forum URL."""
import argparse
from pathlib import Path
import re
import tomllib

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--forums-url", required=True)
parser.add_argument("--output", type=Path, required=True)
args = parser.parse_args()
if not re.fullmatch(r"https://forums\.ahwoo\.com/(?:threads/|forums/(?:[^/?#\s]+/)+)(?:[^/?#\s]*\.)?[0-9]+/?", args.forums_url):
    parser.error("Use the actual HTTPS Ahwoo thread URL after publication approval.")
text = (root / "ecosystem/KsaChinese.toml.in").read_text(encoding="utf-8")
text = text.replace("__FORUM_THREAD_URL__", args.forums_url)
tomllib.loads(text)
args.output.parent.mkdir(parents=True, exist_ok=True)
args.output.write_text(text, encoding="utf-8", newline="\n")
print(args.output)
