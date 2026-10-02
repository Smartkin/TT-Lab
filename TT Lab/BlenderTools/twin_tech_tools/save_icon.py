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

"""A save icon's animation the way the console's browser plays it, without Blender.

Every frame of the animation is a shape and its weights over time: keys of a time and a weight joined by straight lines, the first and
last weight held before and after them. The browser draws every shape times its weight over the weights' sum (PS2IODB's player,
whose timing was compared with the PS2 BIOS). Blender keeps the shapes after the first as shape keys over it, so their values are
their weights and the first shape's weight is what they leave over, which makes the sum 1 and both draw the same.
"""

import typing


def weight_at(keys: typing.Sequence[float], time: float) -> float:
    """The weight a frame's keys (a time and a weight after another, in order) give at the time, 0 without any."""
    pairs = [(keys[i], keys[i + 1]) for i in range(0, len(keys) - 1, 2)]
    if len(pairs) == 0:
        return 0.0

    if time <= pairs[0][0]:
        return pairs[0][1]

    for (start, first), (end, second) in zip(pairs, pairs[1:]):
        if start <= time < end:
            return first + (second - first) * (time - start) / (end - start)

    return pairs[-1][1]


def basis_keys(others: typing.Sequence[typing.Sequence[float]]) -> typing.List[float]:
    """The first shape's keys for the other shapes' keys: one minus their weights, at every time any of them has a key. Between
    those times every weight goes in a straight line, so the first shape's does too."""
    times = sorted({keys[i] for keys in others for i in range(0, len(keys) - 1, 2)})
    if len(times) == 0:
        return [0.0, 1.0]

    result: typing.List[float] = []
    for time in times:
        result += [time, 1.0 - sum(weight_at(keys, time) for keys in others)]

    return result
