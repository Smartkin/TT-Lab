# TT Lab model files (`.tlm`)

TT Lab keeps every model, mesh, skin, blend skin, OGI, scenery, dynamic scenery, collision, skydome and the save icon in a `.tlm` file. TT Lab
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
`DynamicScenery`, `Collision`, `Skydome` or `SaveIcon`.

### Materials

Parts refer to materials by their index in `materials`. A material is one of the project's `Material` assets:

```json
{ "uri": "res://Global PS2_Project/Material/lambert2", "name": "lambert2" }
```

A file the Blender add-on writes has `"exported_by": "twin_tech_tools <version>"` at its top, which TT Lab's own files never have. TT Lab
reads such a file, takes what it brings and writes the file again without it, by URIs: the next export into the file finds that and knows
TT Lab has what it sent there. A model written through a linked TT Lab (LINK.md) names its materials by their URIs only, TT Lab has them as
Blender shows them already; only a material the link didn't make yet comes with what follows.

A material Blender changed or made has `data`: the material as TT Lab keeps it in its data file (Newtonsoft's JSON of `MaterialData`:
`Name`, `DmaChainIndex`, `Shaders` with every `LabShader` field, enums as numbers, the vectors as their floats' bits), with a shader's
picture of the file as `"Image": <index>` into the entry's `images`:

- `changed`: the paths of the values Blender changed since it last had the material TT Lab had (`["DmaChainIndex"]`,
  `["Shaders", 0, "ABlending"]`, a vector's component `["Shaders", 0, "UvScrollSpeed", 2]`, a shader's picture as its `TextureId`, its
  animation whole as `Animation`). TT Lab sets those over what its material has by then, the values it changed meanwhile stay. Without
  `changed` (a material made in Blender TT Lab never had) all of `data` is the material's.
- `shader_sources`: when Blender's shaders aren't the ones it had in their order, where each one came from among TT Lab's material's
  shaders (-1 for one added in Blender): TT Lab's shaders are put in Blender's order before the paths are set.
- `images`: the pictures the shaders draw, each `{ "name", "blender_id"?, "uri"?, "png"? }`: `uri` is the project texture it is (painted in
  Blender, its `png` goes into that texture), `blender_id` the add-on's ID of a picture made in Blender, which TT Lab finds the texture it
  made of it by among the textures made for the model (its `BlenderOwner` parameter is the model's URI); `png` only while TT Lab doesn't
  have the picture as Blender shows it.

A material made in Blender has a `blender_id` too, which finds the material TT Lab made of it among the ones made for the model (a blend
file copied and exported over another model makes that model's own), changed in place from then on:

```json
{ "name": "Hat", "blender_id": "<uuid>",
  "data": { "Name": "HAT", "DmaChainIndex": 2, "Shaders": [ { "ShaderType": 1, "TxtMapping": 1, "Image": 0, ... } ] },
  "images": [ { "png": <u8 view>, "name": "Hat", "blender_id": "<uuid>" } ] }
```

TT Lab writes the file again with what it made of Blender's as its own: the material's URI with its `blender_id` and its pictures'
textures, which the add-on takes as the project's from then on:

```json
{ "uri": "res://PS2_Project/Material/Hat", "name": "Hat", "blender_id": "<uuid>",
  "images": [ { "blender_id": "<uuid>", "uri": "res://PS2_Project/Texture/Hat" } ] }
```

The materials and textures made for a model that a file the add-on wrote no longer uses are deleted once nothing else uses them. An
earlier add-on wrote a material made in Blender without settings as its picture, which TT Lab makes an unlit material of (a skin's the
skinned shader), blended with `"alpha": "BLEND"` and a cut-out of `alpha_cutoff` with `"alpha": "CLIP"`:

```json
{ "name": "New material", "blender_id": "<uuid>", "alpha": "BLEND", "image": { "png": <u8 view>, "name": "wood.png" } }
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
`mesh`, `skin` or `shape`. A model's parts have no material.

### OGI

```
ogi              data: BoundingBoxMin, BoundingBoxMax, JointIdCount
├─ armature      joints, animations
├─ skin          mesh with joints and weights
├─ shape         mesh with shapes (the blend skin), as many as the mesh has shape keys
├─ rigid_bodies
│  └─ body       joint, data: Order, mesh in the joint's space
├─ exit_points
│  └─ exit_point joint, data: Id, transform relative to the joint, matrix
└─ collision_hulls
   └─ hull       joint (255 for none), vertices, faces, planes, edge_directions, face_normals, edges
```

`skin` and `shape` are left out when the OGI has none, and have no transform: the add-on bakes where their object is under the root
into the vertexes, they're in the model's space like the bind poses. A `body` moved away from its joint has a transform relative to
the joint's rest, TT Lab moves the vertexes by it; the add-on works it out from where the object follows its bone (through its Child
Of constraint or as the bone's child) with the armature at rest, whatever pose is on. `matrix` of an exit point is the game's matrix
(16 floats, translation last), kept while the node's transform still is the one it stands for; the add-on carries it with the object
without showing it, like the bones' bind poses. The game finds an exit point by its place and never reads its `Id`: exit points are
in the order of their IDs and their IDs are their places, 0 to n-1 (the add-on writes them so, TT Lab renumbers others with a warning).

#### Collision hulls

The convex hulls the game collides the model with (its `ModelCollisionData`), each on a joint or, with a `joint` of 255, in the model's
space:

```json
{ "kind": "hull", "name": "Hull 0", "joint": 6, "vertices": <f32 view>, "faces": <u8 view>,
  "planes": <f32 view>, "edge_directions": <f32 view>, "face_normals": <f32 view>, "edges": <u8 view> }
```

- `vertices` are 4 floats each (W is 1), `faces` every face's vertex count followed by its vertexes, counter-clockwise seen from
  outside. The add-on shows them as a wire mesh.
- The rest is what the game reads without working it out: `planes` one per face (unit normal pointing out, the plane's constant in W,
  a point is inside where `n·p + w ≤ 0`), `face_normals` and `edge_directions` the unique ones (either way, W 1) that hull against
  hull tests take as separating axes, `edges` the two vertexes of each. TT Lab works them out again (`TwinCollisionHull.ComputeFromFaces`,
  the way the game's own hull builder does) when the node has a transform, which it bakes into the vertexes, or when the add-on's
  `twin_vertices` and `twin_faces`, the ones it imported, aren't the mesh's anymore. The game's tools ordered them differently from
  the game's builder, so unedited hulls keep them as they were.

Dynamic scenery models keep their hulls the same way, as `hull` children without a `joint`.

#### Joints

```json
{
  "index": 3, "parent": 1, "name": "Joint 3",
  "bind": [16 floats],
  "bone": { "axes": [-2, 1, 3], "length": 0.25 },
  "data": {
    "Id": 255, "Detail": 0, "AdditionalAnimationRotation": [4],
    "LocalTranslation": [4], "LocalRotation": [4], "WorldTranslation": [4], "InverseBindMatrix": [16], "UnusedRotation": [4]
  }
}
```

- `parent` is the parent's `index`, -1 for the root. Bones added in Blender have no `index` and get the next free ones.
- A skeleton has one root, joint 0, and its joints are numbered 0 to n-1 in the order the game walks it: from the root down, a
  joint's children in the order of their indexes, each with everything under it. The game works out the joints' matrices in that walk
  and draws the joint of an index with the matrix of that place in it. A joint has 12 children at most and a skin is drawn with 63
  joints at most. The add-on refuses to export an armature of several root bones or past those limits, and numbers every bone in the
  walk's order when the indexes its bones have don't follow it. TT Lab refuses files that break it.
- `Id` is the ID the game finds the joint by, 255 for none (most of the game's models have none): an object's header counts the IDs
  its animations and scripts use, which find their joint by it (N. Gin's joint 4 has ID 0, joint 3 ID 1).
- `bind` is the joint's matrix in model space (column vectors, 16 floats row after row) that bones get as their rest pose, with their
  own axes (`bone`). It's the inverse of the game's inverse bind matrix, which can have a scale bones can't.
- `bone` is what the joint's bone is in Blender, which the add-on writes and TT Lab keeps with the joint, for the next import to make
  the same bone: `axes` are the joint's axes the bone's X, Y and Z are, each one more than the axis's index and negative turned
  around (`[1, 2, 3]` is the joint's own, `[-2, 1, 3]` has the bone's Y along the joint's X), `length` the bone's in the model's
  space. Without it the add-on picks the axes (a bone points down its limb at the child its joint was aimed at and keeps the roll of
  the bone before it on a chain, else X to the model's left when it stands up or hangs down and Z up when it points to the side) and
  the length (to the child). What follows a joint is in the joint's frame in the file; in Blender bodies and hulls are in the bone's
  axes, an exit point keeps the joint's (its axes are what the game turns what it holds by).
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
  every frame's shape weights, `shapes` of them per frame. The add-on writes a weight for every shape key of the shape, the game's
  weights of shapes without a key stay, and an action animating the shape keys of a model gets a `facial` of its own.
- `exact` is the animation as the game stores it (`TwinAnimation` followed by `TwinMorphAnimation`). The add-on keeps it with the
  action and writes it back, edited or not. TT Lab keeps the game's values of every joint, frame and track whose keys still round to
  them (translations, scales and weights to the same 1/4096, rotations closer than half of the game's 1/4096 of a turn) and makes
  the rest from the keys. An animation whose keys all hold is `exact`.
- `additional_rotation` makes the game turn the joint's Euler angles by the joint's `AdditionalAnimationRotation` (AnimateJoint), TT Lab keeps the
  angles as the turn from it. The game blends a joint from frame to frame angle by angle, each the shorter way round, which strays far from the turn
  close to a middle angle of a quarter turn (gimbal lock). The add-on's export gives every joint the additional rotation the game blends its
  animations best with (`animation_frames.py`: where gimbal lock is decides it, the turn about it doesn't) and sets `additional_rotation` in the
  animations made from keys; a joint keeps its own while an animation keeping the game's values (`exact`) turns by it. TT Lab picks every frame's
  set of angles so the game's blends follow the turns, from the last frame into the first too when the last frame comes back near the first.
- `joint_count` is the amount of joints the animation has, which can be more or fewer than the skeleton's (a few of the game's are).
  Joints without keys keep their values from `exact`. Every joint of the model reads the settings of its index from the animation
  playing, with no check, so the add-on gives an animation one for every bone once the model has more bones than when the animation
  was imported (each action keeps that count, `model_joints`, retargeted copies the source model's).
- The root's `JointIdCount` is the game's count of joint IDs it binds (every ID below it), kept while as many joints have an ID; only
  the game's models whose IDs have gaps have one (else -1 or none), TT Lab binds every ID of the others.
- `fps` is 1 to 31 (5 bits of the game's header, which plays an animation for `frames` over it; the game's all have 25). The add-on
  writes a faster action every few frames at a rate that keeps its length, TT Lab clamps what's out of range with a warning.
- `facial.frames` is `frames`: the game reads the weights at the main animation's frame, the weights of fewer frames hold their last.
- Animations without `id` are new and get IDs from `0x8000` up. An animation with the `id` of one before it is a copy, and gets a
  new one too. The add-on gives them their IDs when it exports and keeps them with the actions, TT Lab only numbers older files'.
  These are the IDs the model's game objects pick animations by. The build gives the game's: an animation keeps its `id` while it's
  below 2500 and no other animation of that `id` (an edited copy in another model) got it first, the others get IDs of their own.

### Scenery

```
scenery              data: FogColor, TreeDepth, BoundsMin, BoundsMax, UnusedByte, LightOrder, TreeNodes
├─ scenery_meshes    "Meshes"
│  └─ scenery_mesh   data: Order, Node, Matrix, BoundingBox, transform, mesh
├─ scenery_lods      "LODs"
│  └─ scenery_lod    data: Order, Node, Matrix, BoundingBox, LodType, MinDrawDistance, MaxDrawDistance, ModelsDrawDistances, transform
│     └─ lod_mesh    data: Level, mesh
├─ lights
│  └─ ambient_light, directional_light, point_light, spot_light   data, transform
├─ collision
└─ dynamic_scenery
   └─ dynamic_model
```

- TT Lab writes the placed meshes under `Meshes` and the LODs under `LODs`, numbered in their group. They can be anywhere under the
  root (any node with a mesh and no known kind is a placed mesh, and so is a `lod_mesh` outside a LOD), in the order of their `Order`
  (their place among all of them), new ones last. Made in Blender, a mesh in `Meshes` or `LODs` is written as a placed mesh, an empty in
  `LODs` as a LOD and the meshes under it as its levels. TT Lab makes the tree the game culls them with when it builds the scenery: an octree of the box
  `BoundsMin`/`BoundsMax` (the box the game keeps the chunk's objects in), `TreeDepth` levels deep. `Node` is the tree node a mesh is in,
  its octants from the root (`0` to `7`, bit 0 X, bit 1 Y, bit 2 Z, a set bit the lower half, empty for the root). A mesh stays in its
  node while the node's cell grown twice around its middle holds the mesh, the others and new ones go down the octants their middle is
  in for as long as that holds. `TreeNodes` (a dictionary keyed by index) holds the game's values of the nodes TT Lab doesn't work out
  to the bit: `Path`, `BoundsCenter`, `BoundsMin`, `BoundsMax`, `BoundsHalfSize` and, for a node whose light bits aren't the root's,
  `LightsEnabler` (128 booleans). They're kept while they're still within a hair of what TT Lab works out. A file without a usable box
  gets one around the collision and what's placed.
- `Matrix` of a placed mesh or LOD is the game's matrix (16 floats, translation last), kept while the node's transform is still the one
  it stands for. `BoundingBox` is the box the game culls it with (8 floats), kept while it still holds the mesh.
- Lights are empties: the node's Z axis (the empty's arrow) is a directional light's `Direction`, pointing at where the light comes
  from, and a spot light's (`spot_light`, the tools' negative light), pointing where it shines. `Direction` is kept while the arrow still
  points along it. Every light has `Intensity` (multiplies `Color`), `Enabled` (the game never reads it), `PositionW` and the bounds
  the tools kept (`BoundsMin`, `BoundsMax`, the game works them out again from the intensity). Point and spot lights fade with
  `AttenuationPower` (intensity · (25 / (d² + 25))^power), a spot light has its `ConeAngle` and `FalloffAngle` in 65536ths of a turn and the
  `InnerConeCosine`/`OuterConeCosine` the game lights with, made again from the angles when they changed.
- `collision` has the collision's `surfaces`:

  ```json
  { "surface": "res://...", "name": "Floor", "color": [4 floats], "vertices": 12, "position": <f32 view>, "faces": <u32 view>,
    "triangles": <i32 view>, "vertexes": <i32 view> }
  ```

  `triangles` and `vertexes` say where every triangle and vertex was in the collision, the tree the game finds collisions with comes out
  the same while every surface still has them. `data` has the vertexes no triangle uses (`UnusedVertexes`, `UnusedPositions`).
- `dynamic_model` has `data` (`Order`, `UsesLod`, `BoundingBoxMin`, `BoundingBoxMax`), `hull` children (see Collision hulls), a `mesh` and its
  movement:

  ```json
  "animation": { "frames": 30, "exact": <u8 view>, "translation": <f32 view>, "rotation": <f32 view> }
  ```

  `translation` and `rotation` are every frame's translation and Euler angles (X, then Y, then Z), exactly the game's values. `exact` is
  the movement as the game stores it, kept while the keys are its values.

`Skydome` files have a `skydome` root with a `skydome_mesh` (`data: Order`, `mesh`) for every mesh. `Collision` files have a `collision`
root, `DynamicScenery` files a `dynamic_scenery` root.

### Save icon

The PS2 memory card icon the game's saves get (`Startup\Crash.ico`, Uka Uka's mask) has one node, the root:

```json
{
  "kind": "save_icon", "name": "Crash",
  "data": { "TextureType": 6, "FrameLength": 1, "AnimationSpeed": 1.0, "PlayOffset": 0, "FileId": 65536, "HeaderValue": 1065353216, "AnimationTag": 1 },
  "mesh": { "parts": [ ... ] },
  "animation": { "frames": [ { "shape": 0, "keys": <f32 view> } ] },
  "exact": <u8 view>
}
```

- The icon is triangles of 3 corners, every corner with a position in each shape, a normal, a UV and a color. Its one part has the corners
  that are the same in everything as one vertex, its faces in the icon's order. The icon's Y goes down, the file has it turned half a
  turn about X so it stands up like every model. Positions, normals and UVs are the icon's 4096ths, `color` its bytes (RGBA).
- `shapes` are the icon's shapes after the first, as offsets from the first. The add-on makes them shape keys, `Shape 1` for the second.
- `animation.frames` are the shapes' weights over the animation, in the icon's order: every frame's `keys` are a time and a weight after
  another. The console draws every shape times its weight over the weights' sum, the keys joined by straight lines with the first and
  last weight held, 60 frames a second times `AnimationSpeed`, looping over `FrameLength` (PS2IODB's player, timed against the PS2 BIOS).
  The add-on makes the keys the curves of the shape keys' values. Blender adds the shape keys' offsets from the basis instead, so once
  the curves changed the first shape's keys are written as one minus the other shapes' weights at every time any of them has a key,
  which draws what Blender shows; while they're as imported the first shape's keys stay as they were. A `FrameLength` of 1 plays
  nothing (the game's icon has one shape and that), so an icon animated in Blender with it gets the frame of its last key instead.
- The texture is the file's material, embedded with its image (128x128, an image of another size is resized). Its texels are 5 bits of
  red, green and blue and an alpha bit, the image's 8 bits each spread from them and opaque or transparent.
- `TextureType` 14 or 15 (bit 3) run length encodes the texture. `FileId`, `HeaderValue` and `AnimationTag` are what the game's icon has.
- `exact` is the icon as the game has it. The add-on keeps it with the root and writes it back, TT Lab uses it while the file still has
  everything of it: its corners' fourth words, which the tools wrote 0 into, and the encoding of a run length encoded texture.

### Meshes

```json
{ "parts": [ { ...part... } ] }
```

Each part is drawn with one material and becomes one of the game's submodels. Blender keeps the parts of a mesh in one object, the
`tt_part` face attribute says which part a face belongs to.

| Key | View | Per vertex | Meaning |
|---|---|---|---|
| `material` | | | Index into `materials`, -1 for none (a model's parts, or a part made in Blender without one, which TT Lab draws with a placeholder) |
| `vertices` | | | Number of vertexes |
| `faces` | `u32` | | 3 vertex indexes per triangle |
| `position` | `f32` | 3 | Position |
| `normal` | `f32` | 3 | Unit normal as it's shown |
| `twin_normal` | `f32` | 3 | The game's normal, used while `normal` still points the same way. Zero length ones always stay |
| `uv` | `f32` | 2 | UV as the game has it (V down) |
| `twin_uv` | `f32` | 2 | The game's UV, used while `uv` only moved by rounding errors. Written by the add-on |
| `uv_q` | `f32` | 1 | Q of rigid models' UVs, 1 when missing or 0 |
| `color` | `u8` | 4 | RGBA exactly as the game has it |
| `emit_color` | `u8` | 4 | Emit color of rigid models, the color the game's lit shaders (StandardLit, LitEnvironmentMap, LitReflectionSurface) light, the game's of the RGB of `color`. A part drawn with one has to have it, TT Lab warns about one without. The add-on's `EmitColor` color attribute, written for the parts that had it, the parts drawn with one of those shaders and every rigid part of a mesh none of whose parts had it (added in Blender). The add-on doesn't export a PS2 model with a mesh drawn with one of them without it |
| `alpha_flags` | `u8` | 2 | Whether the color and the emit color are blended with their alpha |
| `joints` | `u8` | 3 | A skin's joints of every vertex in the game's order |
| `weights` | `f32` | 3 | Their weights, 0 for unused ones |
| `group_joints` | `i32` | 4 | Joints of the vertex groups Blender has, -1 for none. Written by the add-on |
| `group_weights` | `f32` | 4 | Their weights, adding up to anything like Blender's |
| `shapes` | list of `f32` | 3 | A blend skin's offset of every vertex for every shape |
| `twin_shapes` | list of `f32` | 3 | The game's offsets, used while `shapes` only moved by rounding errors. Written by the add-on |
| `strips` | | | The strips the game draws the part with, reused while they still draw its triangles |
| `compression` | | | How a skin packs its positions and UVs |

A skin's `joints` and `weights` are used while `group_joints` and `group_weights` are missing or still the same influences, up to
their order and scale. Otherwise the vertex groups' 3 strongest influences are the vertex's, scaled to add up to 1 the way Blender
scales them when it deforms the vertex.

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
