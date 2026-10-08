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
# Parameter of the textures TT Lab made of pictures made in Blender, the picture's ID
BLENDER_IMAGE_PARAMETER = "BlenderImage"


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
    def __init__(self, uri: str, name: str, png_path: str, package: str = "", blender_image: str = ""):
        self.uri = uri
        self.name = name
        self.png_path = png_path
        self.package = package
        # The add-on's ID of the picture TT Lab made the texture of, a picture of a model file's material made in Blender
        self.blender_image = blender_image

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
        self.materials: typing.Dict[str, ProjectMaterial] = {}
        self.textures: typing.Dict[str, ProjectTexture] = {}
        self._index()

    @property
    def assets_path(self) -> str:
        return os.path.join(self.root, "assets")

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
                    parameters = metadata.get("Parameters")
                    blender_image = str(parameters.get(BLENDER_IMAGE_PARAMETER) or "") if isinstance(parameters, dict) else ""
                    self.textures[uri] = ProjectTexture(uri, name, base + ".png", uri_of(metadata.get("Package")), blender_image)

    def texture_of_image(self, blender_image: str) -> typing.Optional[ProjectTexture]:
        """The texture TT Lab made of a picture made in Blender."""
        if not blender_image:
            return None

        return next((texture for texture in self.textures.values() if texture.blender_image == blender_image), None)

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
