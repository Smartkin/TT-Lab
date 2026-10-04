import math
import unittest

from fakes import load_addon_module

collision_builder = load_addon_module("collision_builder")

# Two triangles of a square floor, counter-clockwise seen from above like a mesh drawn on it (Y up)
FLOOR = [((0.0, 0.0, 0.0), (0.0, 0.0, 1.0), (1.0, 0.0, 1.0)), ((0.0, 0.0, 0.0), (1.0, 0.0, 1.0), (1.0, 0.0, 0.0))]


def box(low, high):
    """The 12 triangles of a box, counter-clockwise seen from outside"""
    corner = lambda x, y, z: (high[0] if x else low[0], high[1] if y else low[1], high[2] if z else low[2])
    quads = [((0, 0, 0), (0, 0, 1), (0, 1, 1), (0, 1, 0)), ((1, 0, 0), (1, 1, 0), (1, 1, 1), (1, 0, 1)),
             ((0, 0, 0), (1, 0, 0), (1, 0, 1), (0, 0, 1)), ((0, 1, 0), (0, 1, 1), (1, 1, 1), (1, 1, 0)),
             ((0, 0, 0), (0, 1, 0), (1, 1, 0), (1, 0, 0)), ((0, 0, 1), (1, 0, 1), (1, 1, 1), (0, 1, 1))]
    triangles = []
    for quad in quads:
        a, b, c, d = (corner(*bits) for bits in quad)
        triangles += [(a, b, c), (a, c, d)]

    return triangles


def floor(cells, size, height=lambda x, z: 0.0):
    """A square floor cut into cells, counter-clockwise seen from above"""
    step = size / cells
    point = lambda i, j: (i * step, height(i * step, j * step), j * step)
    triangles = []
    for i in range(cells):
        for j in range(cells):
            a, b, c, d = point(i, j), point(i, j + 1), point(i + 1, j + 1), point(i + 1, j)
            triangles += [(a, b, c), (a, c, d)]

    return triangles


def normal_y(positions, triangle):
    a, b, c = (positions[index] for index in triangle)
    ab = [y - x for x, y in zip(a, b)]
    ac = [y - x for x, y in zip(a, c)]
    return ab[2] * ac[0] - ab[0] * ac[2]


class CollisionBuilderTests(unittest.TestCase):
    def test_corners_of_meshes_become_shared_vertexes(self):
        result = collision_builder.add_triangles([], [], FLOOR)

        self.assertEqual(len(result.positions), 4)
        self.assertEqual(len(result.triangles), 2)
        self.assertEqual((result.skipped, result.dropped), (0, 0))

    def test_corners_closer_than_the_weld_distance_are_one(self):
        nearby = [((0.0005, 0.0, 0.0), (0.0, 0.0, 1.0), (-1.0, 0.0, 0.0))]

        welded = collision_builder.add_triangles([], [], FLOOR + nearby, weld=0.001)
        apart = collision_builder.add_triangles([], [], FLOOR + nearby, weld=0.0001)

        self.assertEqual(len(welded.positions), 5)
        self.assertEqual(len(apart.positions), 6)

    # The game winds a floor so its normal points down, into the solid, the other way from the mesh drawn on it
    def test_triangles_are_wound_like_the_games(self):
        flipped = collision_builder.add_triangles([], [], FLOOR)
        kept = collision_builder.add_triangles([], [], FLOOR, flip=False)

        self.assertTrue(all(normal_y(flipped.positions, triangle) < 0 for triangle in flipped.triangles))
        self.assertTrue(all(normal_y(kept.positions, triangle) > 0 for triangle in kept.triangles))

    def test_flat_triangles_are_left_out_without_their_vertexes(self):
        line = ((5.0, 0.0, 0.0), (6.0, 0.0, 0.0), (7.0, 0.0, 0.0))
        point = ((9.0, 0.0, 0.0), (9.0, 0.0, 0.0005), (9.0005, 0.0, 0.0))

        result = collision_builder.add_triangles([], [], FLOOR + [line, point])

        self.assertEqual(result.dropped, 2)
        self.assertEqual(len(result.positions), 4)

    # Adding to a collision keeps it as it is: its vertexes stay, new corners join them, its triangles aren't made again either way round
    def test_what_the_collision_has_is_left_out(self):
        positions = [(0.0, 0.0, 0.0), (1.0, 0.0, 0.0), (1.0, 0.0, 1.0), (0.0, 0.0, 1.0)]
        triangles = [(0, 1, 2)]

        result = collision_builder.add_triangles(positions, triangles, FLOOR + [((0.0, 0.0, 0.0), (1.0, 0.0, 1.0), (0.0, 1.0, 0.0))])

        self.assertEqual(result.skipped, 1)
        self.assertEqual(result.positions, [(0.0, 1.0, 0.0)])
        self.assertEqual(sorted(sorted(triangle) for triangle in result.triangles), [[0, 2, 3], [0, 2, 4]])

    def test_the_same_triangle_twice_is_made_once(self):
        result = collision_builder.add_triangles([], [], FLOOR + FLOOR)

        self.assertEqual((len(result.triangles), result.skipped), (2, 2))


# The game only collides the player with 32 triangles at a time and slows Crash down to a fifth where there are more, its collision
# is much coarser than the meshes: add_meshes makes them like it
class CoarseCollisionTests(unittest.TestCase):
    def test_a_finely_cut_floor_becomes_its_corners(self):
        result = collision_builder.add_meshes([], [], [floor(10, 10.0)])

        self.assertEqual(len(result.triangles), 2)
        self.assertEqual(sorted(result.positions), [(0.0, 0.0, 0.0), (0.0, 0.0, 10.0), (10.0, 0.0, 0.0), (10.0, 0.0, 10.0)])
        self.assertTrue(all(normal_y(result.positions, triangle) < 0 for triangle in result.triangles))

    # A box with its edges bevelled less than the tolerance becomes the box: a platform's mesh of 167 triangles stands on 12
    def test_a_bevelled_box_becomes_a_box(self):
        bevel = 0.05
        points = [point for x in (-2.0, 2.0) for y in (-0.5, 0.5) for z in (-1.0, 1.0)
                  for point in ((x - math.copysign(bevel, x), y, z), (x, y - math.copysign(bevel, y), z), (x, y, z - math.copysign(bevel, z)))]
        hull_positions, hull_triangles = collision_builder.convex_hull(points)
        mesh = [tuple(hull_positions[vertex] for vertex in triangle) for triangle in hull_triangles]

        result = collision_builder.add_meshes([], [], [mesh])

        self.assertGreater(len(mesh), 12)
        self.assertEqual((result.hulls, len(result.triangles), len(result.positions)), (1, 12, 8))

    # Planks a little apart are one hull, the gaps between them are narrower than Crash
    def test_planks_close_together_are_one_hull(self):
        planks = box((0.0, 0.0, 0.0), (1.0, 0.2, 4.0)) + box((1.05, 0.0, 0.0), (2.05, 0.2, 4.0)) + box((2.1, 0.0, 0.0), (3.1, 0.2, 4.0))

        result = collision_builder.add_meshes([], [], [planks])

        self.assertEqual((result.hulls, len(result.triangles)), (1, 12))
        self.assertEqual(max(position[0] for position in result.positions), 3.1)

    # Under a table there's room, its hull doesn't fit: the top and every leg are hulls of their own
    def test_a_table_keeps_the_room_under_it(self):
        legs = [box((x, 0.0, z), (x + 0.2, 2.0, z + 0.2)) for x in (0.2, 3.6) for z in (0.2, 1.6)]
        table = box((0.0, 2.0, 0.0), (4.0, 2.2, 2.0)) + [triangle for leg in legs for triangle in leg]

        result = collision_builder.add_meshes([], [], [table])

        self.assertEqual((result.hulls, len(result.triangles)), (5, 60))

    # A room's hull would fill it, its floor and walls stay
    def test_a_room_keeps_its_hollow(self):
        corner = lambda x, y, z: (x * 6.0, y * 3.0, z * 6.0)
        quads = [((0, 0, 0), (1, 0, 0), (1, 0, 1), (0, 0, 1)), ((0, 0, 0), (0, 0, 1), (0, 1, 1), (0, 1, 0)), ((1, 0, 0), (1, 1, 0), (1, 1, 1), (1, 0, 1)),
                 ((0, 0, 0), (0, 1, 0), (1, 1, 0), (1, 0, 0)), ((0, 0, 1), (1, 0, 1), (1, 1, 1), (0, 1, 1))]
        room = []
        for quad in quads:
            # Seen from inside
            a, b, c, d = (corner(*bits) for bits in reversed(quad))
            room += [(a, b, c), (a, c, d)]

        result = collision_builder.add_meshes([], [], [room])

        self.assertEqual((result.hulls, len(result.triangles)), (0, 10))
        self.assertEqual(sum(1 for triangle in result.triangles if all(result.positions[vertex][1] == 0.0 for vertex in triangle)), 2)

    # Layers drawn just over the ground (grass, water edges) are no floor of their own, a mesh lying on the collision is there already
    def test_layers_lying_on_others_are_left_out(self):
        decal = [((3.0, 0.05, 3.0), (3.0, 0.05, 7.0), (7.0, 0.05, 7.0)), ((3.0, 0.05, 3.0), (7.0, 0.05, 7.0), (7.0, 0.05, 3.0))]

        result = collision_builder.add_meshes([], [], [floor(1, 10.0), decal])
        onto = collision_builder.add_meshes([(0.0, 0.0, 0.0), (10.0, 0.0, 0.0), (10.0, 0.0, 10.0), (0.0, 0.0, 10.0)], [(0, 1, 2), (0, 2, 3)], [floor(8, 10.0)])

        self.assertEqual((result.covered, len(result.triangles)), (1, 2))
        self.assertEqual((onto.covered, onto.triangles, onto.positions), (1, [], []))

    # Bumps everywhere Crash goes: with every bump he'd touch more triangles than the game takes, the floor is made coarser until he doesn't
    def test_crowded_meshes_are_made_coarser(self):
        crate = floor(30, 3.0, lambda x, z: 0.15 * math.sin(x * 2.0 * math.pi / 0.8) * math.sin(z * 2.0 * math.pi / 0.8))

        fine = collision_builder.add_meshes([], [], [crate], hull_distance=0.0, coarser_where_crowded=False)
        coarse = collision_builder.add_meshes([], [], [crate], hull_distance=0.0)
        before = collision_builder.crowding(fine.positions, fine.triangles, range(len(fine.triangles)))
        after = collision_builder.crowding(coarse.positions, coarse.triangles, range(len(coarse.triangles)))

        self.assertEqual((fine.coarsened, coarse.coarsened), (0, 1))
        self.assertGreater(before.most, collision_builder.MOST_TRIANGLES)
        self.assertLess(after.most, before.most)
        self.assertLess(len(coarse.triangles), len(fine.triangles))
        self.assertIsNotNone(before.worst)

    def test_no_tolerance_and_no_hulls_keep_every_triangle(self):
        mesh = floor(4, 2.0)

        exact = collision_builder.add_meshes([], [], [mesh], tolerance=0.0, hull_distance=0.0)
        triangles = collision_builder.add_triangles([], [], mesh)

        self.assertEqual((exact.positions, exact.triangles), (triangles.positions, triangles.triangles))

    # The collision's own vertexes stay, a mesh made coarser keeps joining them
    def test_the_collisions_vertexes_stay(self):
        positions = [(0.0, 0.0, 0.0), (0.0, 0.0, 10.0), (10.0, 0.0, 10.0)]

        result = collision_builder.add_meshes(positions, [], [floor(5, 10.0)])

        self.assertEqual(len(result.triangles), 2)
        self.assertEqual(result.positions, [(10.0, 0.0, 0.0)])
        self.assertEqual(sorted({vertex for triangle in result.triangles for vertex in triangle}), [0, 1, 2, 3])

    def test_crowding_counts_the_triangles_around_a_floor(self):
        positions = [(0.0, 0.0, 0.0), (0.0, 0.0, 1.0), (1.0, 0.0, 0.0)] + [(0.1 * i, 0.5, 0.0) for i in range(40)]
        # A floor wound like the game's (its normal down) and a fan of 38 thin triangles standing over it
        triangles = [(0, 2, 1)] + [(0, 3 + i, 4 + i) for i in range(38)]

        crowding = collision_builder.crowding(positions, triangles, [0])

        self.assertEqual((crowding.places, crowding.most), (1, 39))


if __name__ == "__main__":
    unittest.main()
