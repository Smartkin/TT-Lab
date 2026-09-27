# TT Lab model files (`.tlm`)

TT Lab keeps every model, mesh, skin, blend skin, OGI, scenery, dynamic scenery, collision and skydome in a `.tlm` file. TT Lab
writes it (`TT Lab/AssetData/Graphics/TlModel`) and the Blender add-on reads and writes it (`twin_tech_tools/tlm.py`). Both follow
this document.

## Container

| Offset | Size | Content |
|---|---|---|
| 0 | 4 | `TLM\0` |
| 4 | 4 | Format version, `u32`, currently 1 |
| 8 | 4 | Length of the JSON in bytes, a multiple of 4 |
| 12 | J | UTF-8 JSON, padded with spaces |
| 12 + J | 4 | Length of the binary data in bytes |
| 16 + J | B | Binary data |

All numbers are little endian. Arrays live in the binary data and the JSON points at them with a *view*:

```json
{ "offset": 0, "count": 12, "type": "f32" }
```

`count` is the number of values, `type` is one of `f32`, `i32`, `u32`, `i16`, `u16`, `u8`. Every view starts at a multiple of 4.

## Top level

```json
{
  "asset": { "type": "Ogi", "name": "OGI 54" },
  "materials": [ { "uri": "res://...", "name": "..." } ],
  "root": { "kind": "ogi", ... }
}
```

`asset.type` is the TT Lab asset the file belongs to: `Ogi`, `Model`, `RigidModel`, `Mesh`, `Skin`, `BlendSkin`, `Scenery`,
`DynamicScenery`, `Collision` or `Skydome`.

### Materials

Parts refer to materials by their index in `materials`. A material is one of the project's `Material` assets:

```json
{ "uri": "res://Global PS2_Project/Material/lambert2", "name": "lambert2" }
```

or, for a material made in Blender that the project doesn't have yet, an embedded one TT Lab turns into project assets when it
loads the file:

```json
{ "name": "New material", "shaders": [ { ...Twin Tech shader fields... } ], "image": { "png": <u8 view>, "name": "wood.png" } }
```

## Nodes

Every node has a `kind` and a `name`. Nodes may have:

- `data`: the element's Twin Tech values, with the keys `schema.json` lists for the node's type;
- `translation` (3 floats), `rotation` (quaternion `x, y, z, w`) and `scale` (3 floats): its transform relative to its parent;
- `joint`: the index of the OGI joint it's attached to;
- `mesh`: its geometry;
- `children`: nodes under it.

### Standalone models

`Model`, `RigidModel`, `Mesh`, `Skin` and `BlendSkin` files have one node, the root, with the asset's mesh: `model`, `rigid_model`,
`mesh`, `skin` or `shape` (`data: { "BlendsAmount": 2 }`). A model's parts have no material.

### OGI

```
ogi              data: BoundingBoxMin, BoundingBoxMax, Collisions
├─ armature      joints, animations
├─ skin          mesh with joints and weights
├─ shape         data: BlendsAmount, mesh with shapes (the blend skin)
├─ rigid_bodies
│  └─ body       joint, data: Order, mesh in the joint's space
└─ exit_points
   └─ exit_point joint, data: Id, Matrix, transform relative to the joint
```

`skin` and `shape` are left out when the OGI has none. A `body` moved away from its joint has a transform relative to it, TT Lab
moves the vertexes by it. `Matrix` of an exit point is the game's matrix (16 floats, translation last), kept while the node's
transform still is the one it stands for.

#### Joints

```json
{
  "index": 3, "parent": 1, "name": "Joint 3",
  "bind": [16 floats],
  "data": {
    "ReactId": 255, "ChildrenAmt2": 0, "AdditionalAnimationRotation": [4],
    "LocalTranslation": [4], "LocalRotation": [4], "WorldTranslation": [4], "InverseBindMatrix": [16], "UnusedRotation": [4]
  }
}
```

- `parent` is the parent's `index`, -1 for the root. Bones added in Blender have no `index` and get the next free ones.
- `bind` is the joint's matrix in model space (column vectors, 16 floats row after row) that bones get as their rest pose. It's the
  inverse of the game's inverse bind matrix, which can have a scale bones can't.
- `data` holds the game's values, which TT Lab keeps while the bone is still where `bind` put it, ignoring the scale. A moved bone
  gets its local transform from its parent's bone, and its inverse bind matrix from where it is with the scale it had.
- `UnusedRotation` is reserved: the game's tools set it but the game doesn't use it, it's kept so the game's files come back the same.

#### Animations

```json
{
  "id": 495, "name": "Walk", "fps": 25, "frames": 36, "joint_count": 40,
  "exact": <u8 view>,
  "joints": [
    { "joint": 0, "translation": <f32 view>, "rotation": <f32 view>, "scale": <f32 view>,
      "independent_scaling": false, "additional_rotation": false }
  ],
  "facial": { "frames": 36, "shapes": 4, "weights": <f32 view>, "unused_flag": false, "additional_rotation": false }
}
```

- `translation`, `rotation` (quaternion `x, y, z, w`) and `scale` hold every frame's local transform of the joint, with its
  additional rotation applied when `additional_rotation` is set. A track with one key holds for every frame. `facial.weights` holds
  every frame's shape weights, `shapes` of them per frame.
- `exact` is the animation as the game stores it (`TwinAnimation` followed by `TwinMorphAnimation`). The add-on keeps it with the
  action and writes it back, edited or not. TT Lab keeps the game's values of every joint, frame and track whose keys still round to
  them (translations, scales and weights to the same 1/4096, rotations closer than half of the game's 1/4096 of a turn) and makes
  the rest from the keys. An animation whose keys all hold is `exact`.
- `joint_count` is the amount of joints the animation has, which can be more than the skeleton's. Joints without keys keep their
  values from `exact`.
- Animations without `id` are new and get IDs from `0x8000` up. An animation with the `id` of one before it is a copy, and gets a
  new one too.

### Scenery

```
scenery            data: FogColor, UnkByte, HasLighting, LightOrder
├─ tree_node       data: Kind, Slot, UnkVec1..4, LightsEnabler, SceneryTypes, UnkUInt (the root)
│  ├─ tree_node    the node's children, in the slots they had
│  ├─ scenery_mesh data: Order, Matrix, BoundingBox, transform, mesh
│  └─ scenery_lod  data: Order, Matrix, BoundingBox, LodType, MinDrawDistance, MaxDrawDistance, ModelsDrawDistances, transform
│     └─ lod_mesh  data: Level, mesh
├─ lights
│  └─ ambient_light, directional_light, point_light, negative_light   data, transform
├─ collision
└─ dynamic_scenery
   └─ dynamic_model
```

- The tree is the hierarchy of `tree_node`s, every mesh and LOD under the node the game culls it with. A mesh keeps its node while it wasn't
  moved or is still in the node's box, a mesh that left it or isn't under any node (any node with a mesh and no known kind is a placed
  mesh) goes to the deepest node it's in. Nodes grow to hold what's in them, nodes made in Blender take the box of what's placed in them.
- `Matrix` of a placed mesh or LOD is the game's matrix (16 floats, translation last), kept while the node's transform is still the one
  it stands for. `BoundingBox` is the box the game culls it with (8 floats), kept while it still holds the mesh.
- A directional light's rotation is its direction.
- `collision` has the collision's `surfaces`:

  ```json
  { "surface": "res://...", "name": "Floor", "color": [4 floats], "vertices": 12, "position": <f32 view>, "faces": <u32 view>,
    "triangles": <i32 view>, "vertexes": <i32 view> }
  ```

  `triangles` and `vertexes` say where every triangle and vertex was in the collision, the tree the game finds collisions with comes out
  the same while every surface still has them. `data` has the vertexes no triangle uses (`UnusedVertexes`, `UnusedPositions`).
- `dynamic_model` has `data` (`Order`, `LodFlag`, `BoundingBoxMin`, `BoundingBoxMax`, `Collisions`), a `mesh` and its movement:

  ```json
  "animation": { "frames": 30, "exact": <u8 view>, "translation": <f32 view>, "rotation": <f32 view> }
  ```

  `translation` and `rotation` are every frame's translation and Euler angles (X, then Y, then Z), exactly the game's values. `exact` is
  the movement as the game stores it, kept while the keys are its values.

`Skydome` files have a `skydome` root with a `skydome_mesh` (`data: Order`, `mesh`) for every mesh. `Collision` files have a `collision`
root, `DynamicScenery` files a `dynamic_scenery` root.

### Meshes

```json
{ "parts": [ { ...part... } ] }
```

Each part is drawn with one material and becomes one of the game's submodels. Blender keeps the parts of a mesh in one object, the
`tt_part` face attribute says which part a face belongs to.

| Key | View | Per vertex | Meaning |
|---|---|---|---|
| `material` | | | Index into `materials`, -1 for none |
| `vertices` | | | Number of vertexes |
| `faces` | `u32` | | 3 vertex indexes per triangle |
| `position` | `f32` | 3 | Position |
| `normal` | `f32` | 3 | Unit normal as it's shown |
| `twin_normal` | `f32` | 3 | The game's normal, used while `normal` still points the same way. Zero length ones always stay |
| `uv` | `f32` | 2 | UV as the game has it (V down) |
| `twin_uv` | `f32` | 2 | The game's UV, used while `uv` only moved by rounding errors. Written by the add-on |
| `uv_q` | `f32` | 1 | Q of rigid models' UVs, 1 when missing or 0 |
| `color` | `u8` | 4 | RGBA exactly as the game has it |
| `emit_color` | `u8` | 4 | Emit color of rigid models |
| `alpha_flags` | `u8` | 2 | Whether the color and the emit color are blended with their alpha |
| `joints` | `u8` | 3 | A skin's joints of every vertex in the game's order |
| `weights` | `f32` | 3 | Their weights, 0 for unused ones |
| `group_joints` | `i32` | 4 | Joints of the vertex groups Blender has, -1 for none. Written by the add-on |
| `group_weights` | `f32` | 4 | Their weights |
| `shapes` | list of `f32` | 3 | A blend skin's offset of every vertex for every shape |
| `twin_shapes` | list of `f32` | 3 | The game's offsets, used while `shapes` only moved by rounding errors. Written by the add-on |
| `strips` | | | The strips the game draws the part with, reused while they still draw its triangles |
| `compression` | | | How a skin packs its positions and UVs |

A skin's `joints` and `weights` are used while `group_joints` and `group_weights` are missing or still the same influences, up to
their order and scale. Otherwise the vertex groups' 3 strongest influences are the vertex's.

`strips`:

```json
{ "vertexes": <i32 view>, "batch_sizes": <i32 view>, "padding": "QuadWord", "ignores_facing": false,
  "joint_palette_sizes": <i32 view>, "joint_palettes": <i32 view>, "blend_shapes": <f32 view> }
```

`compression`: `{ "PositionScale": 0.0001, "PositionOffset": [4], "UvScale": 0.0002, "UvOffset": [4] }`.

The add-on keeps the vertexes in the order it read them. A vertex whose faces need different UVs, normals or colors after an edit
is written once for each of them, after the others. A triangle the game draws from both sides has its own copies of the vertexes,
TT Lab merges vertexes that are the same in everything the game stores. Triangles on a vertex twice, which Blender can't have, are
kept aside with where they were and put back while the part still has all of its other triangles.
