#!/usr/bin/env bash
# Zips the add-on into dist/ as a Blender extension, see package_addon.py
set -euo pipefail

base_dir=$(dirname "${BASH_SOURCE[0]}")
python3 "$base_dir/package_addon.py"
