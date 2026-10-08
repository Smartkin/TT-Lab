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

"""PNGs of 8 bit RGBA pixels, which pictures made in Blender go into model files as. Nothing here needs Blender."""

import struct
import typing
import zlib

_SIGNATURE = b"\x89PNG\r\n\x1a\n"


def encode(width: int, height: int, rgba: bytes) -> bytes:
    """A PNG of the pixels, 4 bytes each, rows from the top."""
    if len(rgba) != width * height * 4:
        raise ValueError("%d bytes aren't %dx%d RGBA pixels" % (len(rgba), width, height))

    stride = width * 4
    rows = b"".join(b"\x00" + rgba[row * stride:(row + 1) * stride] for row in range(height))
    return _SIGNATURE + _chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)) + _chunk(b"IDAT", zlib.compress(rows, 6)) + _chunk(b"IEND", b"")


def decode(data: bytes) -> typing.Tuple[int, int, bytes]:
    """The size and RGBA pixels of a PNG `encode` writes (8 bit RGBA, no filters but none)."""
    if not data.startswith(_SIGNATURE):
        raise ValueError("Not a PNG")

    position = len(_SIGNATURE)
    width = height = 0
    compressed = b""
    while position < len(data):
        length, kind = struct.unpack(">I4s", data[position:position + 8])
        body = data[position + 8:position + 8 + length]
        position += 12 + length
        if kind == b"IHDR":
            width, height = struct.unpack(">II", body[:8])
        elif kind == b"IDAT":
            compressed += body

    rows = zlib.decompress(compressed)
    stride = width * 4
    return width, height, b"".join(rows[row * (stride + 1) + 1:(row + 1) * (stride + 1)] for row in range(height))


def _chunk(kind: bytes, body: bytes) -> bytes:
    return struct.pack(">I", len(body)) + kind + body + struct.pack(">I", zlib.crc32(kind + body) & 0xFFFFFFFF)
