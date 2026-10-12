# Blender tools to work with TwinTech Lab(TT-Lab) files
# Copyright (C) 2025 Smyshliaev "Smartkin" Vladislav
#
# This program is free software; you can redistribute it and/or
# modify it under the terms of the GNU General Public License
# as published by the Free Software Foundation; either version 2
# of the License, or (at your option) any later version.
#
# This program is distributed in the hope that it will be useful,
# but WITHOUT ANY WARRANTY; without even the implied warranty of
# MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
# GNU General Public License for more details.
#
# You should have received a copy of the GNU General Public License
# along with this program; if not, write to the Free Software
# Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

"""The TT Lab project a model file belongs to, and its material and texture assets.

A project is the folder holding a `.tson` file. Every asset has a `<name>.json` with its URI next to its data: a material's
`.data` is JSON with its shaders, a texture's data is a `.png`. Nothing here needs Blender.
"""

import json
import os
import typing

PROJECT_EXTENSION = ".tson"
_MATERIAL_TYPE = "TT_Lab.Assets.Graphics.Material,"
_TEXTURE_TYPE = "TT_Lab.Assets.Graphics.Texture,"
_ASSET_FOLDERS = ("Material", "Texture")
# Parameters of the textures TT Lab made of pictures made in Blender: the picture's ID, and the SHA-256 of the picture's PNG the texture
# was made of (a blend file copied keeps its pictures' IDs, the copy's picture of another file is another texture)
BLENDER_IMAGE_PARAMETER = "BlenderImage"
BLENDER_IMAGE_PNG_PARAMETER = "BlenderImagePng"


class ProjectMaterial:
    def __init__(self, uri: str, name: str, package: str, data_path: str):
        self.uri = uri
        self.name = name
        self.package = package
        self.data_path = data_path
        self._shaders: typing.Optional[typing.List[typing.Dict[str, typing.Any]]] = None
        self.material_name = name

    @property
    def shaders(self) -> typing.List[typing.Dict[str, typing.Any]]:
        """The material's shaders as TT Lab saved them, read when first needed."""
        if self._shaders is None:
            data = self.read()
            self._shaders = [shader for shader in data.get("Shaders", []) if isinstance(shader, dict)] if data is not None else []

        return self._shaders

    def read(self) -> typing.Optional[typing.Dict[str, typing.Any]]:
        """The material's data as TT Lab has it saved now, None when it can't be read."""
        data = _load_json(self.data_path)
        if not isinstance(data, dict):
            return None

        self.material_name = str(data.get("Name", self.name))
        return data

    def texture_uris(self) -> typing.List[str]:
        return [uri for uri in (uri_of(shader.get("TextureId")) for shader in self.shaders) if uri]

    @property
    def location(self) -> str:
        """Where in its package the material is, a scenery's chunk or a PSM's image. Empty for the package's own materials."""
        return _location(self.uri)

    @property
    def label(self) -> str:
        """The name with where the material is, many chunks have materials of the same name."""
        return "%s (%s)" % (self.name, self.location) if self.location else self.name


class ProjectTexture:
    def __init__(self, uri: str, name: str, png_path: str, package: str = "", blender_image: str = "", blender_png: str = ""):
        self.uri = uri
        self.name = name
        self.png_path = png_path
        self.package = package
        # The add-on's ID of the picture TT Lab made the texture of, a picture of a model file's material made in Blender, and the SHA-256
        # of the picture's PNG
        self.blender_image = blender_image
        self.blender_png = blender_png

    @property
    def label(self) -> str:
        """The name with where the texture is, chunks and skies have textures of the same name."""
        location = _location(self.uri)
        return "%s (%s)" % (self.name, location) if location else self.name


def _location(uri: str) -> str:
    path = uri.split("://", 1)[-1].split("/")
    return "/".join(path[1:-2]) if len(path) > 3 else ""


class Project:
    def __init__(self, root: str):
        self.root = root
        self.name = _project_name(root)
        self.materials: typing.Dict[str, ProjectMaterial] = {}
        self.textures: typing.Dict[str, ProjectTexture] = {}
        self._first_dependencies: typing.Dict[str, str] = {}
        self._index()

    @property
    def assets_path(self) -> str:
        return os.path.join(self.root, "assets")

    def version_of(self, package: typing.Optional[str]) -> str:
        """The version of the game a package's assets are for, "PS2" or "XBOX": the version of the package it was made on, its first
        dependency, followed to one of the project's packages of a version like TT Lab's Project.GetPlatform, PS2 when it gets to none."""
        versions = {"res://Global PS2_%s" % self.name: "PS2", "res://PS2_%s" % self.name: "PS2",
                    "res://Global XBOX_%s" % self.name: "XBOX", "res://XBOX_%s" % self.name: "XBOX"}
        visited = set()
        current = package or ""
        while current and current not in visited:
            if current in versions:
                return versions[current]

            visited.add(current)
            current = self._first_dependency(current)

        return "PS2"

    def _first_dependency(self, package: str) -> str:
        """A package's first dependency, its file is in a folder of its name under the assets."""
        if package not in self._first_dependencies:
            name = package.split("://", 1)[-1]
            metadata = _load_json(os.path.join(self.assets_path, name, name + ".json"))
            dependencies = metadata.get("Dependencies") if isinstance(metadata, dict) else None
            self._first_dependencies[package] = uri_of(dependencies[0]) if isinstance(dependencies, list) and len(dependencies) > 0 else ""

        return self._first_dependencies[package]

    def _index(self) -> None:
        for directory, directories, files in os.walk(self.assets_path):
            directories.sort()
            if os.path.basename(directory) not in _ASSET_FOLDERS:
                continue

            for file in sorted(files):
                if not file.endswith(".json"):
                    continue

                path = os.path.join(directory, file)
                metadata = _load_json(path)
                if not isinstance(metadata, dict):
                    continue

                uri = uri_of(metadata.get("URI"))
                asset_type = str(metadata.get("Type", ""))
                name = str(metadata.get("Alias") or metadata.get("InvariantName") or os.path.splitext(file)[0])
                base = os.path.splitext(path)[0]
                if asset_type.startswith(_MATERIAL_TYPE):
                    self.materials[uri] = ProjectMaterial(uri, name, uri_of(metadata.get("Package")), base + ".data")
                elif asset_type.startswith(_TEXTURE_TYPE):
                    parameters = metadata.get("Parameters") if isinstance(metadata.get("Parameters"), dict) else {}
                    self.textures[uri] = ProjectTexture(uri, name, base + ".png", uri_of(metadata.get("Package")), str(parameters.get(BLENDER_IMAGE_PARAMETER) or ""),
                                                        str(parameters.get(BLENDER_IMAGE_PNG_PARAMETER) or ""))

    def texture_of_image(self, blender_image: str, png: typing.Optional[str] = None) -> typing.Optional[ProjectTexture]:
        """The texture TT Lab made of a picture made in Blender: of the picture's ID and, given the SHA-256 of the picture's PNG, of that
        PNG. A blend file copied keeps its pictures' IDs, and the copy's picture of another file isn't the first file's texture."""
        if not blender_image:
            return None

        return next((texture for texture in self.textures.values() if texture.blender_image == blender_image and (png is None or texture.blender_png == png)), None)

    def texture_of(self, material: ProjectMaterial) -> typing.Optional[ProjectTexture]:
        """The texture the material is drawn with, its first shader's that has one."""
        for uri in material.texture_uris():
            texture = self.textures.get(uri)
            if texture is not None and os.path.exists(texture.png_path):
                return texture

        return None

    def materials_of_packages(self, packages: typing.Iterable[str]) -> typing.List[ProjectMaterial]:
        wanted = set(packages)
        return sorted((material for material in self.materials.values() if not wanted or material.package in wanted), key=lambda material: material.name.lower())

    def textures_of_platform(self, platform: typing.Optional[str]) -> typing.List[ProjectTexture]:
        """The textures a model of the version can draw, both versions' without one."""
        return sorted((texture for texture in self.textures.values() if platform is None or platform_of(texture.package) in (platform, None)),
                      key=lambda texture: texture.label.lower())


def find_root(path: str) -> typing.Optional[str]:
    """The folder of the project the file is in, None when it isn't in one."""
    directory = os.path.dirname(os.path.abspath(path))
    while True:
        try:
            if any(name.endswith(PROJECT_EXTENSION) for name in os.listdir(directory)):
                return directory
        except OSError:
            pass

        parent = os.path.dirname(directory)
        if parent == directory:
            return None

        directory = parent


def _project_name(root: str) -> str:
    """The project's name, which its packages of a version are named after: its file's, which TT Lab names after it."""
    try:
        files = sorted(name for name in os.listdir(root) if name.endswith(PROJECT_EXTENSION))
    except OSError:
        files = []

    if len(files) == 0:
        return ""

    data = _load_json(os.path.join(root, files[0]))
    name = data.get("Name") if isinstance(data, dict) else None
    return str(name) if name else files[0][:-len(PROJECT_EXTENSION)]


def version_of_file(path: str) -> str:
    """The version of the game a file of a project's assets is for, "PS2" or "XBOX". TT Lab takes a package it can't tell for PS2,
    and so is a file outside of a project."""
    project = open_project(path) if path else None
    package = package_of(project.root, path) if project is not None else None
    return project.version_of(package) if project is not None and package is not None else "PS2"


def package_of(root: str, path: str) -> typing.Optional[str]:
    """The URI of the package the file is in, packages are the folders right under the project's assets."""
    relative = os.path.relpath(os.path.abspath(path), os.path.join(root, "assets"))
    parts = relative.split(os.sep)
    return "res://" + parts[0] if len(parts) > 1 and parts[0] != ".." else None


def platform_of(package: str) -> typing.Optional[str]:
    """The version of the game a package is for, None for the packages both share. TT Lab names them after it."""
    name = package.upper()
    if "XBOX" in name:
        return "XBOX"

    return "PS2" if "PS2" in name else None


_projects: typing.Dict[str, Project] = {}


def open_project(path: str, refresh: bool = False) -> typing.Optional[Project]:
    """The project the file is in, indexed once and reused."""
    root = find_root(path)
    if root is None:
        return None

    if refresh or root not in _projects:
        _projects[root] = Project(root)

    return _projects[root]


def uri_of(value: typing.Any) -> str:
    """A URI as TT Lab writes them, {"_uri": ...} or the text itself."""
    if isinstance(value, dict):
        value = value.get("_uri")

    return str(value) if value else ""


def _load_json(path: str) -> typing.Any:
    try:
        with open(path, encoding="utf-8-sig") as file:
            return json.load(file)
    except (OSError, ValueError):
        return None
