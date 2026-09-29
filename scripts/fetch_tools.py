#!/usr/bin/env python3
"""Downloads the twinstudio tools (twinmusic, twinpss) of a release into their own folders.

Usage: fetch_tools.py --os Windows|Linux [--out <folder>] [--version <tag>]

Each tool's zip of the release is extracted into <out>/<tool>/, which is where TT Lab looks for them (tools next to its executable).
"""
import argparse
import io
import os
import stat
import sys
import urllib.request
import zipfile

REPO = "Smartkin/twinstudio"
TOOLS = ("twinmusic", "twinpss")
OS_NAMES = {"windows": "windows-latest", "linux": "ubuntu-latest"}


def asset_url(version, name):
    if version == "latest":
        return f"https://github.com/{REPO}/releases/latest/download/{name}"
    return f"https://github.com/{REPO}/releases/download/{version}/{name}"


def fetch(tool, os_name, version, out):
    name = f"{tool}-{OS_NAMES[os_name]}.zip"
    url = asset_url(version, name)
    print(f"Downloading {url}")
    request = urllib.request.Request(url, headers={"User-Agent": "TT-Lab-fetch-tools"})
    with urllib.request.urlopen(request) as response:
        data = response.read()

    folder = os.path.join(out, tool)
    os.makedirs(folder, exist_ok=True)
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        archive.extractall(folder)
        # Python doesn't keep the executable bit the zip has
        if os_name != "windows":
            for info in archive.infolist():
                mode = (info.external_attr >> 16) & 0o777
                path = os.path.join(folder, info.filename)
                if info.is_dir() or not os.path.isfile(path):
                    continue
                if mode & stat.S_IXUSR or os.path.basename(path) == tool:
                    os.chmod(path, os.stat(path).st_mode | stat.S_IXUSR | stat.S_IXGRP | stat.S_IXOTH)

    print(f"{tool} is in {folder}")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--os", required=True, help="Windows or Linux (GitHub's runner.os)")
    parser.add_argument("--out", default=os.path.join("TT Lab", "tools"), help="the tools folder, tools/ next to TT Lab")
    parser.add_argument("--version", default="latest", help="a release tag, the latest release by default")
    args = parser.parse_args()
    os_name = args.os.lower()
    if os_name not in OS_NAMES:
        print(f"No twinstudio build for {args.os}", file=sys.stderr)
        return 1

    for tool in TOOLS:
        fetch(tool, os_name, args.version, args.out)

    return 0


if __name__ == "__main__":
    sys.exit(main())
