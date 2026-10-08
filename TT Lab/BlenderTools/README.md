# Blender Twin Tech Tools Add-on

Created by using [blender-addon-template](https://github.com/b0o/blender-addon-template) by Maddison Hellstrom

This is a toolkit for working and creating Twin Tech Lab(TT-Lab) graphics in Blender. It uses [uv](https://github.com/astral-sh/uv) to manage dependencies and virtual environments.

## Getting Started

Install [uv](https://github.com/astral-sh/uv) and run `uv sync` to initialize the virtual environment and install the dependencies:

```bash
uv sync
```

## TT Lab's model files

TT Lab keeps every OGI, model, rigid model, mesh, skin, blend skin, scenery, dynamic scenery, collision, skydome and the save icon in a TT Lab model file (`.tlm`, see [TLM.md](TLM.md)) and loads them from it. Import one with **File > Import > TT Lab Model (.tlm)** and export it back with **File > Export > TT Lab Model (.tlm)** or the **Export TLM** button of the **Twin Tech Model** panel, which suggest the file it came from. TT Lab picks up the changes the next time it loads the asset. Anything not edited in Blender comes back exactly as the game had it. Importing switches the scene's view transform to **Standard**, which shows the materials' colors as the console does.

Exporting writes the model of the active object, or of the one model the selected objects are part of. The button is always there: when the scene can't be exported (nothing selected, an object outside a model, an OGI without its armature, a skeleton the game can't take...) it says why. What's deleted but still in the file until **File > Clean Up > Purge Unused** isn't exported: objects taken out of every scene, and actions nothing plays that have no fake user (the bones' settings of an imported animation still point at it), which exporting names. Give an action a fake user to keep it without playing it.

An OGI becomes this tree:

- **root**: an empty with the OGI's bounding box and collisions in its **Twin Tech** panel. It turns the game's Y up into Blender's Z up, everything under it is written back relative to it. The game takes the bounding box for its instances' collision when the model has no hulls, their shadows and physics: a model keeps the box it was imported with while its meshes are as they were, and exporting writes a box around the meshes at rest once they change past it (or for a model made in Blender).
  - **armature**: the skeleton, a bone for every joint resting where the joint's bind pose puts it.
  - **Skin**: the mesh the armature deforms.
  - **Blend Skin**: the mesh the armature deforms, with a shape key for every shape the facial animations blend.
  - **Rigid Bodies**: holds the meshes that move with a joint. Every body follows its bone through a **Child Of** constraint, Blender can't put objects under bones.
  - **Exit Points**: holds the points other objects attach to, empties that follow their bone the same way. Move them with Blender's tools. The game finds an exit point by its place among them (a character's hand is 0, its head 1), never by its ID: exporting puts them in the order of their IDs and gives them their places as IDs. An object's instances only get as many exit points as the object's **Exit Points** in TT Lab says.
  - **Collision Hulls**: holds the convex meshes the game collides the model with, each following its bone (or none, in the model's space). The game takes a hull for convex and of 64 vertexes and 64 faces at most: exporting writes a hull that isn't convex as its convex hull, saying so (split it into convex hulls to keep its shape), and refuses one past those counts.

Models, rigid models, meshes, skins and blend skins become a root with their mesh under it.

### New models

**Add > Twin Tech** (in the 3D viewport's Add menu) makes a new model laid out the way imported ones are, to build on and export:

- **OGI Model**: a root with the bounding box, an armature of joint 0 and joint 1 under it, a box rigid body on joint 1 under **Rigid Bodies**, an empty **Skin** and **Blend Skin** (with its basis shape key) the armature deforms, and the empty **Exit Points** and **Collision Hulls** holders. Give the skin a mesh weighted to the bones, the blend skin shape keys, replace or add bodies, add bones as needed (they become the next joints), give it actions (exporting makes the bounding box hold the meshes). An empty skin or blend skin is written as none. Keep more than one joint, or an exit point: the game draws only the rigid bodies of a model of one joint and no exit points, never its skin (exporting one warns).
- **Scenery**: a root with the box new chunks keep their objects in, a 20 unit ground in its **Meshes** and an empty **LODs**, an ambient and a directional light (the arrow points at where the light comes from), the ground again as the **Collision** with a `SURF_DEFAULT_0` material (the collision surface asset a material is named after is its surface) and an empty **Dynamic scenery**. Build on it, meshes anywhere under the root are the scenery's, and **Generate Collision** makes the collision out of them.

- **PS2 Save Icon**: the memory card icon the game's saves get, a 3 unit box with grey vertex colors and a checkered 128x128 texture, one shape and no animation. Its shape keys are the icon's shapes and their values' keys its animation.

Exporting a new model asks for a file. Export it over an OGI's or a scenery's `.tlm` in a TT Lab project to make that asset the model, TT Lab reads it the next time it loads the asset.

### Materials

Meshes are drawn with the project's materials. When the model is in a TT Lab project the add-on finds the project from the file's folder, and every material gets the project material's settings and is drawn the way the game draws it. Many chunks have materials of the same name, so a scenery's materials are listed with their chunk (`lambert1 (levels/school/rooftop/roof01)`) and typing the chunk's name finds them.

The **Twin Tech Material** panel of the material properties edits everything TT Lab edits of a material: its game name, render bucket and up to 4 shaders, each a pass drawn over the ones before it. A shader's sub-panels have its type, texture (**Pick Project Texture**, or any picture of Blender's), filter, coordinates, shading, shadows, fog, context, anti-aliasing, LOD and type parameters; blending (a preset or an own `(A - B) * C + D` formula); the alpha test; the depth test and depth writing; the U and V scrolls with their phases and speeds; and the shader animation. Settings the game doesn't read with the others are grayed out like in TT Lab. Exporting the model writes what was changed in Blender over what the project has by then, so what was changed in TT Lab meanwhile stays, and TT Lab takes it into the project's material when it reads the file. Pictures that aren't a project texture's go into the file and become textures of the project, the same texture every time (and given the picture's pixels when it was painted on). The panel says when the material was changed in Blender; **Revert to the Project's** takes the project's settings again, **Make a New Material** makes it one of Blender's (TT Lab adds it to the project instead of changing the project's), **Pick Project Material** makes it another of the project's materials and **Reload Project Materials** reads the project again after it was changed in TT Lab, keeping what was changed in Blender.

A shader's animation is six tracks (U, V, red, green, blue, alpha). Turn **Animation** on and keyframe a track to animate it (hover it and press I), a track without keys keeps its value. The game's frame `n` is the scene's frame `n * scene fps / animation fps`, and the animation loops over its **Frames**, the last blending into the first; imported animations come with their keys laid out that way and a Cycles modifier. The **From Animation** scrolls take U and V as the texture's offset.

The materials show what the console shows (the decompilation's GS settings and VU1 programs): the texture times the vertex colors, a color of 0x80 drawing the texture at half its PNG's brightness (the PS2 keeps textures at half their PNG's values), the lit types lit by TT Lab's default lights (a third grey ambient light at 4.5 and one from above at 3), the environment maps reading the texture by the half vector of the way to the eye and the normal on the screen, the metallic type by the way to the eye reflected off the surface, the alpha test, blending, the scrolls, and the passes added up in the display's values. A material is its passes emitted over what shows through them: exact where nothing behind shows through and where what's behind is only tinted, colors added to what's behind are added in Blender's linear values. The reflection surface shows what's behind it tinted, without the game's moving of it, the shadows' volumes (Color Only) aren't drawn, and depth tests the preview can't do (Always) draw like the others. Meshes without vertex colors show black: **Add Vertex Colors** gives them the ones TT Lab writes for them.

A material made in Blender is drawn like TT Lab makes such materials, an unlit one of the picture of its first Image Texture node, and exported with that picture; TT Lab adds it to the project as a material and a texture, and uses the same ones every time it's exported again. **Use Twin Tech Settings** gives it the settings TT Lab gives it (a skin's the skinned shader first), to edit like the project's.

### Meshes

A mesh holds all parts of the model, the `tt_part` face attribute says which part a face is. Faces given another material become a part of their own. The game's exact values are kept in attributes next to the ones Blender shows (`twin_normal`, `twin_uv`, `twin_joints`, `twin_weights`, `twin_shape_*`, `tt_uv_q` and the alpha flags) and used while Blender's still match them, so don't rename or delete them. A mesh without them still works, it just uses Blender's values everywhere.

- Vertex colors are the `Color` attribute, emit colors `EmitColor`. The game draws the texture times them, 0x80 leaving the texture as the PS2 keeps it (half its PNG's brightness), the materials do the same.
- A skin's vertex groups are the bones. The game's weights are kept while every joint's share of a vertex stays the same.

### Animations

Every animation of the OGI is an action, with a slot for the armature and one for the blend skin's shape keys. Playing an action on the armature plays its facial part on the shape keys too. Actions keep the game's bytes of the animation they were made from, TT Lab keeps the game's values of everything whose keys still hold them: editing one bone on one frame only changes that.

- Twinsanity lets each joint turn inheriting its parent's scale on or off per animation. Select a bone and use the **Twin Tech Bone Settings Per Animation** panel in the bone properties, it shows the settings for the active action and **Add Current Animation** adds them for an action that has none yet:
  - *Doesn't inherit parent's scale*: the joint ignores its parent's scale. The viewport previews it by switching the bone's `Inherit Scale` between `Full` and `None` as the active action changes, on the armatures of Twin Tech models only: other rigs in the scene keep their bones' settings.
  - *Uses additional rotation*: the joint applies its additional animation rotation.
- New actions become new animations of the OGI, which game objects can then play: exporting gives a new action, or a copy of one, an ID of its own from 0x8000 up, which it keeps (save the .blend afterwards), so adding or renaming actions never changes what a game object plays.
- An action's frames are its frame range, from its first frame: an imported action has a manual frame range from 0, lengthen or shorten it there. Keys are sampled on every whole frame, the face's too, which always has as many frames as the action.
- Bones turn by the curves of their rotation mode (quaternion, Euler angles or axis and angle), or by the rotation curves the action has when it has none of that mode (actions keyed while the bone turned another way).
- An animation plays at the frames a second it was imported with, a new one at the scene's. The game's animations have 31 at most (all of its own have 25): a faster action is written at a rate it has, every few frames, keeping its length (60 frames a second at 30, every 2nd frame), exporting says so.
- A scaled armature object (FBX rigs come in at 0.01) is fine: its bones' rests and the animations' moves are both written with its scale.
- Only keys are exported: what constraints (IK, Copy Rotation...) and drivers do to bones isn't, exporting warns about such bones. Bake the actions first (Pose > Animation > Bake Action, with Visual Keying).
- Adding bones gives every animation keys for them, imported ones included: every joint of the model reads its animation's keys, and the game's animations only have keys for the joints of their model.
- Rotations are stored as Euler angles with 1/4096 of a turn precision.

#### Playing the game's animations on another model

The **Twin Tech Animation Retargeting** panel (Object properties) has a **Source Armature**, the one playing the animations (a game OGI's, imported), a **Target Armature**, the one to play them (another model's, like a rig put into an **OGI Model** template), and **Perform Retargeting**. The button is always there and says what's missing:

1. The target's bones are named like the source's joints and get their indexes and settings (react ID, additional rotation): a bone that holds one of the joint indexes is that joint while the target's skeleton is the source's (every joint index both have under the same parent's: exporting gives any rig's bones indexes that only number them), then bones named like the source's joints are those joints, then the hierarchies are walked together (the roots in order, then the children of every matched pair in order) and what's left becomes new joints named `Joint N`. Bones don't have to point the same way, only their rests matter. **Match Bones By** forces joint indexes (a copy of the source's skeleton) or names and the hierarchy (another rig), or **Name Only**: bones named exactly like the source's joints are those joints and nothing else is matched, the target's other bones stay as they are (new joints keeping their names and settings). Those named bones have to be under each other the way the joints are (other bones may be in between, joints the target lacks may be left out), else retargeting refuses, saying which bone is where, and changes nothing. The report says how many bones matched each way.
2. Every animation of the source is retargeted to the target: every matched bone turns from its own rest, in the world, as much as the source's does on every frame, and moves as far beyond where its nearest matched ancestor's turn leaves its rest offset, scaled by how much bigger the target's skeleton is. Bones between matched ones stay at rest under their parents. The shape keys of the mesh it deforms are animated in the order of the source's. Every animation keeps its ID, so the game objects playing it keep working.
3. The source's animations go and the target gets them under the same names, without Blender's `.001`. Export the target's model afterwards.

### Scenery

A scenery becomes its root with its placed meshes under **Meshes**, its LODs under **LODs**, its lights, its collision and its dynamic scenery under it.

- A mesh you make under **Meshes** is a new placed mesh, an empty you make under **LODs** is a new LOD with the meshes under it as its levels. TT Lab reads meshes and LODs anywhere under the root though, grouped however you like, and makes the tree the game culls them with when it builds the scenery, there's nothing to keep up by hand:
  - A mesh stays in the tree node it was in (its **Node**, under **Game Values**) while that node still holds it, a moved mesh that left it and a new one go to the deepest node holding them.
  - New meshes and LODs go after the existing ones.
  - The root's **Bounds Min** and **Bounds Max** are the box the game keeps the chunk's objects in, it has to hold every place objects go. **Tree Depth** is how many levels the tree goes down.
- The scenery is lit by its lights, a scenery without lights has its lit materials drawn black.
- A scenery LOD is an empty with its levels under it as **LOD level** meshes, `0` is the closest one.
- Lights are empties typed as ambient, directional, point or negative lights, and their color and radius are in the panel. A directional light points the way its empty is turned.
- Dynamic scenery models are objects typed **Dynamic model** under the empty typed **Dynamic scenery**, which keeps the chunk's dynamic scenery even when it has no models left. Each moves by its action, the location and rotation of its object on every frame. Movements that weren't edited stay exactly as the game has them.
- Skydomes become their root with their meshes under it, drawn in the order of their **Order**.

### Collision

The collision is one mesh with a material for every surface, in the surface's color. The material's **Surface** field in the **Twin Tech Collision Surface** panel names the collision surface asset. Without one, TT Lab uses the surface the material is named after. Where the game had every triangle and vertex is kept in the `tt_collision_triangle` and `tt_collision_vertex` attributes, the game's collision tree comes out the same while no face was added or removed.

**Generate Collision** in a scenery's **Twin Tech Model** panel makes the collision out of the meshes the scenery draws: the selected object's and every mesh under it, or the whole scenery's (the closest level of LODs, never the dynamic scenery, which moves). It replaces the collision or adds to it, leaving out the triangles it already has, and puts every triangle on the surface you pick (the collision's first one by default). Corners closer than the weld distance become one vertex and triangles that come out flat are left out. The game winds its collision so a triangle's normal points into the solid, a floor's down, the other way from the meshes drawn on it, so the triangles are turned around unless **Wound Like The Game's** is off. TT Lab makes the game's collision tree for a collision made this way.

### The Twin Tech panels

The **Twin Tech** panels are in the Object, Bone (**Twin Tech Joint**) and Material (**Twin Tech Collision Surface**) properties.

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
blender --background --factory-startup --python blender_roundtrip.py -- --collision fixtures/scenery.tlm fixtures/scenery_collision_from_blender.tlm
blender --background --factory-startup --python blender_roundtrip.py -- --add fixtures/scenery.tlm fixtures/scenery_added_in_blender.tlm
blender --background --factory-startup --python blender_retarget.py
blender --background --factory-startup --python blender_templates.py -- fixtures/ogi_template.tlm fixtures/scenery_template.tlm fixtures/save_icon_template.tlm
blender --background --factory-startup --python blender_deleted.py
blender --background --factory-startup --python blender_materials.py -- fixtures/ogi_materials_from_blender.tlm
```

`blender_retarget.py` retargets a changed copy of the fixture model's animations through the retargeting panel's operator and prints `RETARGET OK`. `blender_templates.py` makes the templates, checks them, exports them into the fixtures TT Lab's tests read and prints `TEMPLATES OK`. `blender_deleted.py` exports the fixture model after deleting parts of it without purging them and prints `DELETED OK`. `blender_materials.py` checks the materials against a project it makes up (settings, merging with the project's, animations, materials made in Blender, the panels, and renders that show the console's colors), writes the model TT Lab's tests read and prints `MATERIALS OK`.

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

Meshes with more than 4096 vertexes are exported decimated (the game's biggest skin has 2716), the export says so in its warnings; a mesh with shape keys is left as it is and only warned about.
