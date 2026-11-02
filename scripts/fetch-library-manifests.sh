#!/usr/bin/env bash
# Fetches the VCV Library database (github.com/VCVRack/library) at a pinned commit into out/vcv-library:
# only its manifests/*.json are read, for the names and tags of modules whose sources the catalog can't scan.
set -euo pipefail
cd "$(dirname "$0")/.."
commit="$1"
dir=out/vcv-library
if [ -d "$dir/.git" ] && [ "$(git -C "$dir" rev-parse HEAD)" = "$commit" ]; then
    exit 0
fi
rm -rf "$dir"
git init -q "$dir"
git -C "$dir" fetch -q --depth 1 https://github.com/VCVRack/library "$commit"
git -C "$dir" -c advice.detachedHead=false checkout -q FETCH_HEAD
echo "VCVRack/library @ ${commit:0:10}"
