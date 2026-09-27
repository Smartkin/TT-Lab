# Blender Twin Tech Tools Add-on

Created by using [blender-addon-template](https://github.com/b0o/blender-addon-template) by Maddison Hellstrom

This is a toolkit for working and creating Twin Tech Lab(TT-Lab) graphics in Blender. It uses [uv](https://github.com/astral-sh/uv) to manage dependencies and virtual environments.

## Getting Started

Install [uv](https://github.com/astral-sh/uv) and run `uv sync` to initialize the virtual environment and install the dependencies:

```bash
uv sync
```

## TT Lab's model files

TT Lab keeps every OGI, model, rigid model, mesh, skin, blend skin, scenery, dynamic scenery, collision and skydome in a TT Lab model file (`.tlm`, see [TLM.md](TLM.md)) and loads them from it. Import one with **File > Import > TT Lab Model (.tlm)** and export it back with **File > Export > TT Lab Model (.tlm)**, which suggests the file it came from. TT Lab picks up the changes the next time it loads the asset. Anything not edited in Blender comes back exactly as the game had it.

An OGI becomes this tree:

- **root**: an empty with the OGI's bounding box and collisions in its **Twin Tech** panel. It turns the game's Y up into Blender's Z up, everything under it is written back relative to it.
  - **armature**: the skeleton, a bone for every joint resting where the joint's bind pose puts it.
  - **Skin**: the mesh the armature deforms.
  - **Blend Skin**: the mesh the armature deforms, with a shape key for every shape the facial animations blend.
  - **Rigid Bodies**: holds the meshes that move with a joint. Every body follows its bone through a **Child Of** constraint, Blender can't put objects under bones.
  - **Exit Points**: holds the points other objects attach to, empties that follow their bone the same way. Move them with Blender's tools.

Models, rigid models, meshes, skins and blend skins become a root with their mesh under it.

### Materials

Meshes are drawn with the project's materials. When the model is in a TT Lab project the add-on finds the project from the file's folder and shows every material with its texture. **Pick Project Material** in the **Twin Tech Project Material** panel of the material properties swaps in another of the project's materials. Many chunks have materials of the same name, so a scenery's materials are listed with their chunk (`lambert1 (levels/school/rooftop/roof01)`) and typing the chunk's name finds them. **Reload Project Materials** reads them again after they were changed in TT Lab.

A material made in Blender is exported with the image of its first Image Texture node. TT Lab adds it to the project as a material and a texture drawn with that image, and uses the same ones for it every time it's exported again.

### Meshes

A mesh holds all parts of the model, the `tt_part` face attribute says which part a face is. Faces given another material become a part of their own. The game's exact values are kept in attributes next to the ones Blender shows (`twin_normal`, `twin_uv`, `twin_joints`, `twin_weights`, `twin_shape_*`, `tt_uv_q` and the alpha flags) and used while Blender's still match them, so don't rename or delete them. A mesh without them still works, it just uses Blender's values everywhere.

- Vertex colors are the `Color` attribute, emit colors `EmitColor`. The game draws a color of half brightness at full brightness, the materials do the same.
- A skin's vertex groups are the bones. The game's weights are kept while every joint's share of a vertex stays the same.

### Animations

Every animation of the OGI is an action, with a slot for the armature and one for the blend skin's shape keys. Playing an action on the armature plays its facial part on the shape keys too. Actions keep the game's bytes of the animation they were made from, TT Lab keeps the game's values of everything whose keys still hold them: editing one bone on one frame only changes that.

- Twinsanity lets each joint turn inheriting its parent's scale on or off per animation. Select a bone and use the **Twin Tech Bone Settings Per Animation** panel in the bone properties, it shows the settings for the active action and **Add Current Animation** adds them for an action that has none yet:
  - *Doesn't inherit parent's scale*: the joint ignores its parent's scale. The viewport previews it by switching the bone's `Inherit Scale` between `Full` and `None` as the active action changes.
  - *Uses additional rotation*: the joint applies its additional animation rotation.
- New actions become new animations of the OGI, which game objects can then play. A copy of an action keeps the ID of the one it was copied from, TT Lab gives the copy an ID of its own.
- An action's frames are its frame range. Keys are sampled on every whole frame.
- Rotations are stored as Euler angles with 1/4096 of a turn precision.

### Scenery

A scenery becomes its root with the tree the game culls it with under it, its lights, its collision and its dynamic scenery.

- The tree is the hierarchy of empties typed **Tree node**, with every mesh and LOD under the node it's culled with. There's nothing to keep up by hand:
  - A mesh stays in the tree node it's under while it wasn't moved or is still inside that node's box.
  - A mesh moved out of its node's box, or put anywhere outside of the tree, goes to the deepest node it's inside of. Boxes grow to hold what's in them.
  - New meshes and LODs go after the existing ones of their node.
  - Each tree node's **Lights** field says which lights light what it holds.
- A scenery LOD is an empty with its levels under it as **LOD level** meshes, `0` is the closest one.
- Lights are empties typed as ambient, directional, point or negative lights, and their color and radius are in the panel. A directional light points the way its empty is turned.
- Dynamic scenery models are objects typed **Dynamic model** under the empty typed **Dynamic scenery**, which keeps the chunk's dynamic scenery even when it has no models left. Each moves by its action, the location and rotation of its object on every frame. Movements that weren't edited stay exactly as the game has them.
- Skydomes become their root with their meshes under it, drawn in the order of their **Order**.

### Collision

The collision is one mesh with a material for every surface, in the surface's color. The material's **Surface** field in the **Twin Tech Material** panel names the collision surface asset. Without one, TT Lab uses the surface the material is named after. Where the game had every triangle and vertex is kept in the `tt_collision_triangle` and `tt_collision_vertex` attributes, the game's collision tree comes out the same while no face was added or removed.

### The Twin Tech panels

The **Twin Tech** panels are in the Object, Bone (**Twin Tech Joint**) and Material (**Twin Tech Material**) properties.

- **Type** is the element's role. Changing it gives the element that role's settings.
- Lists (an OGI's collisions) have buttons to add, remove and move items. Their order is the order the game gets them in.
- Values TT Lab only keeps as the game had them are hidden until **Game Values** is turned on.
- TT Lab data the add-on doesn't know is kept as it was and written back.

## Testing

`python -m unittest discover -s tests` runs the tests that don't need Blender. `tests/blender_roundtrip.py` imports and exports the fixtures TT Lab's tests write in Blender, TT Lab's tests check what it exported:

```bash
cd tests
blender --background --factory-startup --python blender_roundtrip.py -- fixtures/ogi.tlm fixtures/ogi_from_blender.tlm
blender --background --factory-startup --python blender_roundtrip.py -- --edit fixtures/ogi.tlm fixtures/ogi_edited_in_blender.tlm
blender --background --factory-startup --python blender_roundtrip.py -- fixtures/scenery.tlm fixtures/scenery_from_blender.tlm
```

## Developing or making an changes

1. Activate the virtual environment:

- Linux

```bash
source .venv/bin/activate
```

- Windows

```pwsh
.\.venv\Scripts\activate.ps1
```

2. Open your editor and start developing your add-on. Preferably, start your editor from inside of the virtualenv shell so that your editor's LSP is aware of the virtual environment and dependencies. Also don't forget to adjust `pyproject.toml` dev-dependencies section according to your editor of choice.

- For Neovim, the [`blender.nvim`](https://github.com/b0o/blender.nvim) plugin is recommended.
- For VSCode, the [`blender_vscode`](https://github.com/JacquesLucke/blender_vscode) extension is recommended.

## License

Blender Add-on Template &copy; 2024 Maddison Hellstrom

GNU General Public License v2.0 or later
