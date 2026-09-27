"""Zips the add-on into dist/ the same way build_addon.sh does, without needing the virtual environment or a shell."""

import ast
import os
import zipfile

ROOT_DIRECTORY = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ADDON_MODULE = "twin_tech_tools"


def read_version():
    with open(os.path.join(ROOT_DIRECTORY, ADDON_MODULE, "__init__.py"), encoding="utf-8") as init_file:
        tree = ast.parse(init_file.read())

    for node in tree.body:
        if isinstance(node, ast.Assign) and any(isinstance(target, ast.Name) and target.id == "bl_info" for target in node.targets):
            bl_info = ast.literal_eval(node.value)
            return ".".join(str(part) for part in bl_info["version"])

    raise RuntimeError("bl_info wasn't found in the add-on's __init__.py")


def main():
    output_path = os.path.join(ROOT_DIRECTORY, "dist", f"{ADDON_MODULE}-{read_version()}.zip")
    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    with zipfile.ZipFile(output_path, "w", zipfile.ZIP_DEFLATED) as archive:
        for directory, directories, files in os.walk(os.path.join(ROOT_DIRECTORY, ADDON_MODULE)):
            directories[:] = sorted(name for name in directories if name != "__pycache__")
            for file in sorted(files):
                if file.endswith((".pyc", ".pyo")):
                    continue

                path = os.path.join(directory, file)
                archive.write(path, os.path.relpath(path, ROOT_DIRECTORY))

    print(f"Packaged {output_path}")


if __name__ == "__main__":
    main()
