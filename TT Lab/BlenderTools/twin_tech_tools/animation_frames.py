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

"""The additional rotations of an OGI's joints that the game blends their animations best with, without Blender.

The game blends a joint from a frame to the next angle by angle (AnimateJoint: the Euler angles about X, then Y, then Z, each the shorter
way round, the rotation of the angles turned by the joint's additional rotation when the animation's settings of the joint say so), not
along the shortest turn between the frames. Close to a middle angle of a quarter turn (gimbal lock) the angles of nearby rotations are far
apart and those blends stray far from the turns: a run's thigh swinging past it strayed a quarter of the leg mid-stride. TT Lab keeps a
joint's angles as the turn from its additional rotation, which turns the angles' axes and gimbal lock with them, so the export gives every
joint whose additional rotation no animation keeping the game's values needs the one whose blends follow its animations' turns closest.
Only where gimbal lock is matters: turning the axes about it adds the same to the last angle of every frame, and TT Lab picks every
frame's set of angles (TlmAnimationFrames) the way the costs here do.

Quaternions are (w, x, y, z) like tlm_math's. A frame's cost goes through every frame of the joint's animations: in Blender with numpy
arrays of one value per frame, without numpy (the tests' Python) frame by frame on floats, through the same formulas.
"""

import math
import struct
import typing

try:
    import numpy
except ImportError:
    numpy = None  # type: ignore[assignment]

from . import tlm_math

Quaternion = tlm_math.Quaternion
# Whether an animation turns a joint's angles by the joint's additional rotation, and the angles in every frame (4096ths of a turn)
GameAngles = typing.Tuple[bool, typing.Any]

TURN = math.pi * 2
RAW_UNITS_PER_TURN = 4096
IDENTITY: Quaternion = (1.0, 0.0, 0.0, 0.0)
# Frames more than a quarter turn apart (the cosine of half that between their quaternions): no angles blend along such a jump
JUMP_DOT = math.cos(math.pi / 4)
# Steps of an angle this close to half a turn, which rounding to the game's units can turn the other way round
AMBIGUOUS_STEP = math.pi - math.pi / 180
REFINING_STEPS = (8.0, 3.0)
# How much less another frame's blends have to stray, summed up, for a joint to change its additional rotation
CLEARLY_BETTER = 0.8
# Blending between frames whose middle angles are both within this of none to the other set of the second frame turns the middle angle
# past gimbal lock and strays far from the turn, never less than blending to the same set: it isn't worked out
NEAR_GIMBAL_LOCK = math.pi / 4
FAR = 4.0


def _squared_distance(degrees: float) -> float:
    """The squared distance of the unit quaternions of two rotations this many degrees apart: 2 - 2 cos of half the angle."""
    return (2.0 * math.sin(math.radians(degrees) / 4.0)) ** 2


def _degrees(distance: float) -> float:
    return math.degrees(4.0 * math.asin(min(1.0, math.sqrt(distance) / 2.0)))


# A joint whose animations' blends all follow their turns this closely keeps its additional rotation
GOOD_ENOUGH = _squared_distance(1.0)
# Blends straying this far already count as they are, whichever way their half turns go
LARGE_DEVIATION = _squared_distance(45.0)


class _Floats:
    """The functions the formulas use, on floats."""
    sin = staticmethod(math.sin)
    cos = staticmethod(math.cos)
    atan2 = staticmethod(math.atan2)
    sqrt = staticmethod(math.sqrt)
    abs = staticmethod(abs)
    maximum = staticmethod(max)
    minimum = staticmethod(min)
    round = staticmethod(round)

    @staticmethod
    def where(condition: typing.Any, chosen: typing.Any, other: typing.Any) -> typing.Any:
        return chosen if condition else other

    @staticmethod
    def sign(value: float) -> float:
        return 0.0 if value == 0 else math.copysign(1.0, value)


class _Arrays:
    """The same on numpy arrays."""
    if numpy is not None:
        sin = staticmethod(numpy.sin)
        cos = staticmethod(numpy.cos)
        atan2 = staticmethod(numpy.arctan2)
        sqrt = staticmethod(numpy.sqrt)
        abs = staticmethod(numpy.abs)
        maximum = staticmethod(numpy.maximum)
        minimum = staticmethod(numpy.minimum)
        round = staticmethod(numpy.round)
        where = staticmethod(numpy.where)
        sign = staticmethod(numpy.sign)


def _multiply(a: typing.Any, b: typing.Any) -> typing.Any:
    aw, ax, ay, az = a
    bw, bx, by, bz = b
    return (aw * bw - ax * bx - ay * by - az * bz,
            aw * bx + ax * bw + ay * bz - az * by,
            aw * by - ax * bz + ay * bw + az * bx,
            aw * bz + ax * by - ay * bx + az * bw)


def _normalized(q: typing.Any, m: typing.Any) -> typing.Any:
    w, x, y, z = q
    length = m.sqrt(w * w + x * x + y * y + z * z)
    return (w / length, x / length, y / length, z / length)


def _from_angles(angles: typing.Any, m: typing.Any) -> typing.Any:
    """The game's rotation of Euler angles, like TT Lab's quat(vec3): the turn about Z of the turn about Y of the turn about X."""
    sx, cx = m.sin(angles[0] * 0.5), m.cos(angles[0] * 0.5)
    sy, cy = m.sin(angles[1] * 0.5), m.cos(angles[1] * 0.5)
    sz, cz = m.sin(angles[2] * 0.5), m.cos(angles[2] * 0.5)
    return (cx * cy * cz + sx * sy * sz, sx * cy * cz - cx * sy * sz, cx * sy * cz + sx * cy * sz, cx * cy * sz - sx * sy * cz)


def _euler(q: typing.Any, m: typing.Any) -> typing.Any:
    """The rotation's Euler angles the way TT Lab works them out (GlmExtensions.ToEulerAngles): the middle one within a quarter turn,
    at gimbal lock the turn about X and Z in the first."""
    w, x, y, z = _normalized(q, m)
    turned_x = (1.0 - 2.0 * (y * y + z * z), 2.0 * (x * y + w * z), 2.0 * (x * z - w * y))
    cosine = m.sqrt(turned_x[0] * turned_x[0] + turned_x[1] * turned_x[1])
    middle = m.atan2(-turned_x[2], cosine)
    lock = cosine <= 1e-7
    first = m.where(lock, m.atan2(-turned_x[2] * 2.0 * (x * y - w * z), 1.0 - 2.0 * (x * x + z * z)),
                    m.atan2(2.0 * (y * z + w * x), 1.0 - 2.0 * (x * x + y * y)))
    last = m.where(lock, 0.0 * cosine, m.atan2(turned_x[1], turned_x[0]))
    return first, middle, last


def _other_set(angles: typing.Any) -> typing.Any:
    """The rotation's other set of Euler angles."""
    return angles[0] + math.pi, math.pi - angles[1], angles[2] + math.pi


def _shorter_way(difference: typing.Any, m: typing.Any) -> typing.Any:
    return difference - m.round(difference / TURN) * TURN


def _distance(a: typing.Any, b: typing.Any, m: typing.Any) -> typing.Any:
    near = sum((first - second) ** 2 for first, second in zip(a, b))
    far = sum((first + second) ** 2 for first, second in zip(a, b))
    return m.minimum(near, far)


def _deviation(start: typing.Any, end: typing.Any, start_rotation: typing.Any, end_rotation: typing.Any, m: typing.Any) -> typing.Any:
    """How far the game's blend from one set of angles to another strays from the shortest turn between their rotations: the squared
    distance of their quaternions halfway. A step of about half a turn goes either way in the game once the angles are rounded to its
    units, the worse way counts (at gimbal lock the first and last angles stepping half a turn are the same rotation only while they turn
    the same way). TlmAnimationFrames.Deviation in TT Lab."""
    steps = tuple(_shorter_way(b - a, m) for a, b in zip(start, end))
    dot = sum(a * b for a, b in zip(start_rotation, end_rotation))
    flip = m.where(dot < 0.0, -1.0, 1.0)
    halfway = _normalized(tuple(a + b * flip for a, b in zip(start_rotation, end_rotation)), m)
    worst = _distance(_from_angles(tuple(a + s * 0.5 for a, s in zip(start, steps)), m), halfway, m)
    ambiguous = tuple(m.abs(step) >= AMBIGUOUS_STEP for step in steps)
    flipping = (worst < LARGE_DEVIATION) & (ambiguous[0] | ambiguous[1] | ambiguous[2])
    if m is _Floats:
        return _flipped_worst(start, steps, halfway, ambiguous, worst, m) if flipping else worst

    # Few blends have steps of half a turn
    chosen = numpy.nonzero(flipping)
    if len(chosen[0]) == 0:
        return worst

    pick = lambda values: tuple(value[chosen] for value in values)
    worst = worst.copy()
    worst[chosen] = _flipped_worst(pick(start), pick(steps), pick(halfway), pick(ambiguous), worst[chosen], m)
    return worst


def _flipped_worst(start: typing.Any, steps: typing.Any, halfway: typing.Any, ambiguous: typing.Any, worst: typing.Any, m: typing.Any) -> typing.Any:
    """The worst of the blends of every way the steps of half a turn can go."""
    for flips in range(1, 8):
        applies = True
        for axis in range(3):
            if flips >> axis & 1:
                applies = applies & ambiguous[axis]

        turned = tuple(step - m.sign(step) * TURN if flips >> axis & 1 else step for axis, step in enumerate(steps))
        other = _distance(_from_angles(tuple(a + s * 0.5 for a, s in zip(start, turned)), m), halfway, m)
        worst = m.where(applies, m.maximum(worst, other), worst)

    return worst


class _Track:
    """A track of rotations as the frame search sees it: the frames its blends go between, without the blends of a joint that doesn't
    move (they don't stray in any frame) or jumps past a quarter turn (they stray in every frame)."""

    def __init__(self, rotations: typing.Sequence[Quaternion]):
        frames = [_normalized(rotation, _Floats) for rotation in rotations]
        count = len(frames)
        dots = [abs(sum(a * b for a, b in zip(frames[index], frames[(index + 1) % count]))) for index in range(count)]
        # The track comes back to where it started: its last frame is no farther from its first than the frames are from each other
        self.loops = count > 1 and dots[-1] >= min(dots[:-1]) - 1e-12
        self.jumps = False
        self.rotations: typing.List[Quaternion] = []
        self.blends: typing.List[typing.Tuple[int, int]] = []
        slots: typing.Dict[int, int] = {}

        def slot(index: int) -> int:
            if index not in slots:
                slots[index] = len(self.rotations)
                self.rotations.append(frames[index])

            return slots[index]

        for index in range(count if self.loops else count - 1):
            if dots[index] < JUMP_DOT:
                self.jumps = True
            elif dots[index] < 1.0 - 1e-12:
                self.blends.append((slot(index), slot((index + 1) % count)))


class _Prepared:
    """The tracks of a joint that move. With numpy every frame of all of them is in one array, worked out all at once like _Track does it."""

    def __init__(self, tracks: typing.Sequence[typing.Any]):
        tracks = [track for track in tracks if len(track) > 1]
        if numpy is None:
            self.tracks = [track for track in (_Track(rotations) for rotations in tracks) if len(track.blends) > 0]
            self.empty = len(self.tracks) == 0
            return

        self.empty = len(tracks) == 0
        if self.empty:
            return

        lengths = numpy.array([len(track) for track in tracks])
        frames = numpy.concatenate([numpy.asarray(track, dtype=numpy.float64).reshape(-1, 4) for track in tracks])
        frames = frames / numpy.linalg.norm(frames, axis=1)[:, None]
        firsts = numpy.cumsum(lengths) - lengths
        lasts = firsts + lengths - 1
        following = numpy.arange(len(frames)) + 1
        following[lasts] = firsts
        dots = numpy.abs(numpy.sum(frames * frames[following], axis=1))
        closure = numpy.zeros(len(frames), dtype=bool)
        closure[lasts] = True
        loops = dots[lasts] >= numpy.minimum.reduceat(numpy.where(closure, numpy.inf, dots), firsts) - 1e-12
        track_of_frame = numpy.repeat(numpy.arange(len(tracks)), lengths)
        counted = ~closure | loops[track_of_frame]
        jumps = numpy.logical_or.reduceat(counted & (dots < JUMP_DOT), firsts)
        starts = numpy.nonzero(counted & (dots >= JUMP_DOT) & (dots < 1.0 - 1e-12))[0]
        self.empty = len(starts) == 0
        if self.empty:
            return

        ends = following[starts]
        used, slots = numpy.unique(numpy.concatenate([starts, ends]), return_inverse=True)
        rotations = frames[used]
        self.rotations = tuple(rotations[:, component] for component in range(4))
        self.starts, self.ends = slots[:len(starts)], slots[len(starts):]
        # Blends of a track follow each other, tracks without any have none to sum
        counts = numpy.bincount(track_of_frame[starts], minlength=len(tracks))
        moving = counts > 0
        self.blend_counts = counts[moving]
        self.blend_starts = numpy.cumsum(self.blend_counts) - self.blend_counts
        # A loop has to come back to the set of angles it started with, a jump can change it for free
        self.closed = (loops & ~jumps)[moving]


def _costs(prepared: _Prepared, frames: typing.Sequence[Quaternion]) -> typing.List[typing.Tuple[float, float]]:
    """For every frame, the least sum of every blend's deviation that angles kept with it allow and the largest of them, each frame of an
    animation having two sets of angles: blends between the same sets of two frames stray exactly as far as between their other sets, so
    what counts is where the sets change, and a loop has to come back to the set it started with."""
    if numpy is None:
        return [_float_cost(prepared, frame) for frame in frames]

    # A row per frame, a column per frame of the animations
    inverses = numpy.array([tlm_math.quat_inverse(frame) for frame in frames])
    local = _multiply(tuple(inverses[:, component, None] for component in range(4)), prepared.rotations)
    angles = _euler(local, _Arrays)
    starts, ends = prepared.starts, prepared.ends
    start_angles = tuple(angle[:, starts] for angle in angles)
    end_angles = tuple(angle[:, ends] for angle in angles)
    start_rotations = tuple(component[:, starts] for component in local)
    end_rotations = tuple(component[:, ends] for component in local)
    same = _deviation(start_angles, end_angles, start_rotations, end_rotations, _Arrays)
    other = numpy.full(same.shape, FAR)
    near = numpy.nonzero((numpy.abs(start_angles[1]) > NEAR_GIMBAL_LOCK) | (numpy.abs(end_angles[1]) > NEAR_GIMBAL_LOCK))
    if len(near[0]) > 0:
        pick = lambda values: tuple(value[near] for value in values)
        other[near] = _deviation(pick(start_angles), _other_set(pick(end_angles)), pick(start_rotations), pick(end_rotations), _Arrays)

    chosen = numpy.minimum(same, other)
    totals, worsts = chosen.sum(axis=1), chosen.max(axis=1)
    if prepared.closed.any():
        # Of every loop changing its set of angles an odd number of times the blend changing it least changes the other way
        changes = numpy.add.reduceat((other < same).astype(numpy.int64), prepared.blend_starts, axis=1)
        odd = prepared.closed[None, :] & (changes % 2 == 1)
        gap = numpy.abs(same - other)
        least = numpy.minimum.reduceat(gap, prepared.blend_starts, axis=1)
        totals = totals + numpy.where(odd, least, 0.0).sum(axis=1)
        changed = numpy.where(gap == numpy.repeat(least, prepared.blend_counts, axis=1), numpy.maximum(same, other), 0.0)
        worsts = numpy.maximum(worsts, numpy.where(odd, numpy.maximum.reduceat(changed, prepared.blend_starts, axis=1), 0.0).max(axis=1))

    return list(zip(totals.tolist(), worsts.tolist()))


def _float_cost(prepared: _Prepared, frame: Quaternion) -> typing.Tuple[float, float]:
    inverse = tlm_math.quat_inverse(frame)
    total, worst = 0.0, 0.0
    for track in prepared.tracks:
        local = [_multiply(inverse, rotation) for rotation in track.rotations]
        angles = [_euler(rotation, _Floats) for rotation in local]
        changes, least_gap, least_worst = 0, math.inf, 0.0
        for start, end in track.blends:
            same = _deviation(angles[start], angles[end], local[start], local[end], _Floats)
            near = abs(angles[start][1]) > NEAR_GIMBAL_LOCK or abs(angles[end][1]) > NEAR_GIMBAL_LOCK
            other = _deviation(angles[start], _other_set(angles[end]), local[start], local[end], _Floats) if near else FAR
            total += min(same, other)
            worst = max(worst, min(same, other))
            changes += 1 if other < same else 0
            if abs(same - other) < least_gap:
                least_gap, least_worst = abs(same - other), max(same, other)

        if track.loops and not track.jumps and changes % 2 == 1:
            total += least_gap
            worst = max(worst, least_worst)

    return total, worst


def _turning(axis: typing.Tuple[float, float, float]) -> Quaternion:
    """The turn taking Z to the axis the shortest way, which puts gimbal lock (a turned X axis along Z) along the axis."""
    w = 1.0 + axis[2]
    if w < 1e-12:
        return 0.0, 1.0, 0.0, 0.0

    return tlm_math.quat_normalized((w, -axis[1], axis[0], 0.0))


def _hemisphere(count: int) -> typing.List[typing.Tuple[float, float, float]]:
    """Directions spread evenly over the half of the sphere above the XY plane (the other half puts gimbal lock in the same places)."""
    golden = math.pi * (3.0 - math.sqrt(5.0))
    axes = []
    for index in range(count):
        z = 1.0 - (index + 0.5) / count
        radius = math.sqrt(1.0 - z * z)
        axes.append((math.cos(golden * index) * radius, math.sin(golden * index) * radius, z))

    return axes


AXES = _hemisphere(16)


def choose_frame(tracks: typing.Sequence[typing.Sequence[Quaternion]], current: Quaternion) -> Quaternion:
    """The additional rotation a joint's animations' angles blend best with in the game: the one putting gimbal lock where the blends of
    every frame of every animation (the tracks: the joint's rotations, the additional rotation included) follow their turns closest. The
    current one stays while every blend already follows its turn within a degree with it, or no other one blends clearly better."""
    prepared = _Prepared(tracks)
    if prepared.empty:
        return current

    current_cost, current_worst = _costs(prepared, [tlm_math.quat_normalized(current)])[0]
    if current_worst < GOOD_ENOUGH:
        return current

    best_axis, best_cost = _best_axis(prepared, [(0.0, 0.0, 1.0)] + AXES, None, math.inf)
    # A frame only a little better isn't worth turning every frame of the animations for
    if best_cost > current_cost * CLEARLY_BETTER:
        return current

    for degrees in REFINING_STEPS:
        step = math.radians(degrees)
        for _ in range(2):
            center = best_axis
            first = _unit(_cross(center, (0.0, 1.0, 0.0) if abs(center[0]) > 0.9 else (1.0, 0.0, 0.0)))
            second = _cross(center, first)
            ring = [tuple(c * math.cos(step) + (a * math.cos(angle) + b * math.sin(angle)) * math.sin(step) for c, a, b in zip(center, first, second))
                    for angle in (direction * math.pi / 3.0 for direction in range(6))]
            best_axis, best_cost = _best_axis(prepared, ring, best_axis, best_cost)
            if best_axis == center:
                break

    return _turning(best_axis)


def _best_axis(prepared: _Prepared, axes: typing.List[typing.Tuple[float, float, float]], best_axis: typing.Any, best_cost: float) -> typing.Tuple[typing.Any, float]:
    """The axis of gimbal lock blending best of the axes and the best one so far. Lower costs win by more than rounding, so of equally
    good axes the first stays."""
    axes = [_unit(axis) for axis in axes]
    for axis, (cost, _) in zip(axes, _costs(prepared, [_turning(axis) for axis in axes])):
        if best_axis is None or cost < best_cost - best_cost * 1e-12 - 1e-15:
            best_axis, best_cost = axis, cost

    return best_axis, best_cost


def _unit(vector: typing.Sequence[float]) -> typing.Tuple[float, float, float]:
    length = math.sqrt(sum(value * value for value in vector))
    return vector[0] / length, vector[1] / length, vector[2] / length


def largest_deviation(tracks: typing.Sequence[typing.Sequence[Quaternion]], frame: Quaternion) -> float:
    """How far (in degrees) the game's blends of the tracks stray from their turns at most, kept with the frame, every frame's angles
    the set that blends best."""
    prepared = _Prepared(tracks)
    return 0.0 if prepared.empty else _degrees(_costs(prepared, [tlm_math.quat_normalized(frame)])[0][1])


def _cross(a: typing.Sequence[float], b: typing.Sequence[float]) -> typing.Tuple[float, float, float]:
    return a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]


def game_angles(exact: bytes) -> typing.List[GameAngles]:
    """Every joint's angles in every frame of an animation as the game stores it (TwinAnimation: its packed counts and frames, every joint's
    settings, the values of its static channels, then every frame's animated values). With numpy a joint's angles are an array of a row
    per frame."""
    packed, frames = struct.unpack_from("<IH", exact, 0)
    joints = packed & 0x7F
    statics = (packed >> 10 & 0xFFE) // 2
    animated = packed >> 22
    settings = [struct.unpack_from("<4H", exact, 6 + 8 * joint) for joint in range(joints)]
    offset = 6 + 8 * joints
    static_values = struct.unpack_from("<%dh" % statics, exact, offset)
    offset += 2 * statics
    if numpy is not None:
        frame_values = numpy.frombuffer(exact, dtype="<i2", count=frames * animated, offset=offset).reshape(frames, animated).astype(numpy.int64)
    else:
        frame_values = [struct.unpack_from("<%dh" % animated, exact, offset + 2 * animated * frame) for frame in range(frames)]

    result: typing.List[GameAngles] = []
    for flags, choices, static_index, animated_index in settings:
        # Where each rotation channel's values are: a set bit is a channel that doesn't change
        sources = []
        next_static, next_animated = static_index, animated_index
        for channel in range(9):
            if choices >> channel & 1:
                sources.append((True, next_static))
                next_static += 1
            else:
                sources.append((False, next_animated))
                next_animated += 1

        rotation_sources = sources[3:6]
        if numpy is not None:
            columns = [numpy.full(max(frames, 1), static_values[index]) if static else
                       (frame_values[:, index] if frames > 0 else numpy.zeros(1, dtype=numpy.int64)) for static, index in rotation_sources]
            result.append((bool(flags >> 12 & 1), numpy.stack(columns, axis=1)))
            continue

        angles = [tuple(static_values[index] if static else (frame_values[frame][index] if frames > 0 else 0) for static, index in rotation_sources)
                  for frame in range(max(frames, 1))]
        result.append((bool(flags >> 12 & 1), angles))

    return result


def holds(angles: typing.Tuple[int, int, int], rotation: Quaternion, additional: typing.Optional[Quaternion]) -> bool:
    """Whether the rotation is the game's one of the angles (turned by the additional rotation), closer than half of the smallest step
    the game turns joints by: TT Lab keeps the game's values of such a frame (TlmAnimations.HoldsRotation)."""
    game = _from_angles(tuple(angle / RAW_UNITS_PER_TURN * TURN for angle in angles), _Floats)
    if additional is not None:
        game = _multiply(additional, game)

    difference = math.sqrt(_distance(_normalized(game, _Floats), _normalized(rotation, _Floats), _Floats))
    return difference * 2.0 < math.pi / RAW_UNITS_PER_TURN


def holds_any(angles: typing.Sequence[typing.Tuple[int, int, int]], rotations: typing.Sequence[Quaternion], additional: typing.Optional[Quaternion]) -> bool:
    """Whether any frame's rotation holds the game's angles of it (frames past the game's last read its last, like TT Lab)."""
    return holding([(angles, rotations, additional)])[0]


def holding(tracks: typing.Sequence[typing.Tuple[typing.Any, typing.Any, typing.Optional[Quaternion]]]) -> typing.List[bool]:
    """For every track (the game's angles of every frame, every frame's rotation and the additional rotation turning the angles, or None),
    whether any frame's rotation holds the game's angles of it, all at once."""
    if numpy is None:
        return [any(holds(angles[min(frame, len(angles) - 1)], rotation, additional) for frame, rotation in enumerate(rotations))
                if len(angles) > 0 else False for angles, rotations, additional in tracks]

    counts = [len(rotations) if len(angles) > 0 else 0 for angles, rotations, _ in tracks]
    if sum(counts) == 0:
        return [False] * len(tracks)

    frame_angles = numpy.concatenate([numpy.asarray(angles, dtype=numpy.float64)[numpy.minimum(numpy.arange(count), len(angles) - 1)]
                                      for (angles, _, _), count in zip(tracks, counts) if count > 0])
    keys = numpy.concatenate([numpy.asarray(rotations, dtype=numpy.float64).reshape(-1, 4) for (_, rotations, _), count in zip(tracks, counts) if count > 0])
    turns = numpy.concatenate([numpy.tile(additional if additional is not None else IDENTITY, (count, 1)) for (_, _, additional), count in zip(tracks, counts) if count > 0])
    game = _multiply(tuple(turns.T), _from_angles(tuple(frame_angles.T / RAW_UNITS_PER_TURN * TURN), _Arrays))
    difference = numpy.sqrt(_distance(_normalized(game, _Arrays), _normalized(tuple(keys.T), _Arrays), _Arrays))
    held = difference * 2.0 < math.pi / RAW_UNITS_PER_TURN
    used = [count for count in counts if count > 0]
    per_track = iter(numpy.logical_or.reduceat(held, numpy.cumsum(used) - numpy.array(used)).tolist())
    return [bool(next(per_track)) if count > 0 else False for count in counts]
