"""Zips the add-on into dist/ as a Blender extension, the way `blender -c extension build` does, without needing Blender, the virtual
environment or a shell: the add-on's files at the root of the zip, its manifest among them."""

import glob
import os
import re
import zipfile

ROOT_DIRECTORY = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ADDON_MODULE = "twin_tech_tools"
ADDON_DIRECTORY = os.path.join(ROOT_DIRECTORY, ADDON_MODULE)


def read_version():
    with open(os.path.join(ADDON_DIRECTORY, "blender_manifest.toml"), encoding="utf-8") as manifest:
        match = re.search(r'^version\s*=\s*"([^"]+)"', manifest.read(), re.MULTILINE)

    if match is None:
        raise RuntimeError("The add-on's blender_manifest.toml has no version")

    return match.group(1)


def main():
    output_path = os.path.join(ROOT_DIRECTORY, "dist", f"{ADDON_MODULE}-{read_version()}.zip")
    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    # TT Lab ships everything in dist/ next to itself, older versions' zips would go along
    for old in glob.glob(os.path.join(os.path.dirname(output_path), f"{ADDON_MODULE}-*.zip")):
        if old != output_path:
            os.remove(old)

    with zipfile.ZipFile(output_path, "w", zipfile.ZIP_DEFLATED) as archive:
        for directory, directories, files in os.walk(ADDON_DIRECTORY):
            directories[:] = sorted(name for name in directories if name != "__pycache__")
            for file in sorted(files):
                if file.endswith((".pyc", ".pyo")):
                    continue

                path = os.path.join(directory, file)
                archive.write(path, os.path.relpath(path, ADDON_DIRECTORY))

    print(f"Packaged {output_path}")


if __name__ == "__main__":
    main()
