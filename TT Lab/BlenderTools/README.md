# Blender Twin Tech Tools Add-on

Created by using [blender-addon-template](https://github.com/b0o/blender-addon-template) by Maddison Hellstrom

This is a toolkit for editing Twin Tech Lab (TT Lab) models in Blender: OGIs, sceneries, skydomes, models, meshes, skins, blend skins, dynamic scenery, collision and the save icon. It uses [uv](https://github.com/astral-sh/uv) to manage dependencies and virtual environments.

## Installing

TT Lab installs the add-on it comes with into Blender: **Tools > Install the Blender add-on**, or **Install the add-on** in the Preferences' **Blender** section. It needs Blender 4.5 or newer and goes in as an extension (Blender's **Get Extensions** lists it as Twin Tech Tools). TT Lab 1.0.0's add-on is turned off, its files stay. TT Lab finds Blender on the path, its Flatpak, Steam's or its install in Program Files; pick another one in the Preferences' **Blender** section. **Edit in Blender** offers to install the add-on when TT Lab didn't install this version into that Blender yet.

To install it by hand, use **Get Extensions > Install from Disk** with the zip in TT Lab's `BlenderTools/dist`.

## Editing with TT Lab

**Edit in Blender** opens a model in Blender: the project tree's context menu of a model, the Blender button at the top of a model's editor, and a chunk's scene toolbar for its scenery. Blender opens the model's blend file. The first time TT Lab asks for a new blend file or an existing one. A new one goes where you say (the project's `source` folder first): Blender imports the model into it and saves it there. TT Lab remembers each model's blend file on this computer only, so a project shared between people has everyone's own.

**Use an Existing Blend File** (the first time, or next to Edit in Blender: the project tree's context menu, the arrow next to the editor's Blender button or its right click, the arrow next to the scene toolbar's) puts the model of a blend file you made before in the model's place, of this project or another one. Blender opens the blend file (a Blender linked to TT Lab that has it open already takes it) and its model of the same kind takes the model's place, asking which when the file has several. Another project's model brings that project's materials and pictures: they become the model's own, made again in this project (copies of the ones the scene's other models draw too, which keep theirs), whatever this project has of the same names. Once it's linked the model is written over TT Lab's in one undo step ("From Blender: Crash replaced by Custom of custom.blend"), unsaved like them when the model has unsaved changes in TT Lab, and the blend file is saved: it's the model's blend file from then on, no longer the one of a model whose model it was, and a Blender that had the model before stops standing for it. When the checks or TT Lab refuse it nothing is replaced and the status bar says why: syncing once it's fixed replaces the model, closing it without saving leaves everything as it was. Saved while TT Lab doesn't have the project open, the model waits until it does.

While TT Lab has the model's project open, the scene's models link to it by themselves. The **Twin Tech Model** panel says so, and TT Lab's project tree marks the model and what it draws as **Blender**'s.

- Materials and pictures made in Blender are the project's at once, unsaved until the blend file is saved, which has TT Lab save them. Settings changed in Blender go to TT Lab once they stop changing for a moment, a picture once a stroke is done. Each arrives in TT Lab as an undo step, "From Blender: ...", in the asset's editor when it's open.
- What TT Lab changes of them comes into Blender as an undo step, "From TT Lab: ...". Renames follow both ways.
- **Sync with TT Lab** (the Twin Tech Model panel) writes the scene's models through TT Lab and saves what Blender made and changed. Writing every model takes a while with big models, so saving the blend file does it as the extension's preference says (Edit > Preferences > Add-ons > Twin Tech Tools, also next to the button): **Prompt to Sync on Save** (the default) asks once the blend file is saved, and Sync writes the models and saves the blend file again with TT Lab's answers; **Always on Save** writes them as the blend file is saved; **Only Manually** leaves them to the button. A model written is one undo step in TT Lab that keeps the model before it, a scenery's a step of its chunk's scene. A model with changes in TT Lab this Blender didn't see (made after it last read or wrote the model) isn't written until you say what to do with it, right after the sync: write Blender's over it, take TT Lab's (the model is imported again as TT Lab has it, its unsaved changes too) or leave it for now. A model TT Lab already has as Blender has it changes nothing in TT Lab, no undo step. A model the checks refuse isn't written and the status bar says why. Syncing in Edit Mode writes what's edited: it leaves Edit Mode while it writes the models.
- Closing the blend file without saving takes back in TT Lab what Blender made and changed since it was saved. TT Lab quitting first drops them without asking.
- A project material Blender couldn't read from the project's files is drawn magenta, and takes TT Lab's settings once it's linked.
- One blend file can hold models of several projects. Two projects of the same name have the same asset paths, so each model's materials and pictures stay its own project's: TT Lab only changes what its project's models draw, and importing a model shares materials and pictures only with the scene's models of the same project (a copy of the project, which keeps its UUID, counts as the project).

Without TT Lab, syncing writes each model into its own file with what Blender made or changed of its materials and pictures. TT Lab takes those when it reads the file: it finds them by their Blender IDs among the ones made for that model, changes them in place, and deletes the ones made in Blender that the file doesn't use any more and nothing else does.

**Open in TT Lab** (the Twin Tech Model panel) shows the model in TT Lab: the TT Lab that has its project open, else a running TT Lab opens the project (asking about its own unsaved changes first), else TT Lab starts with it.

A new scene's first **Ctrl+S** suggests the project's `source` folder.

## TT Lab's model files

TT Lab keeps every model in a TT Lab model file (`.tlm`, see [TLM.md](TLM.md)). **File > Import > TT Lab Model (.tlm)** imports one. Importing a model the scene has already puts it in that one's place after asking; **Import as a Copy** adds a second one. **Export TLM** in the Twin Tech Model panel writes the model into a file, and so does **File > Export > TT Lab Model (.tlm)**. Anything not edited in Blender comes back exactly as the game had it. Importing switches the scene's view transform to **Standard**, which shows the materials' colors as the console does.

Blender shows the model as the game does: the game's x, y and z are Blender's -x, -z and y, a reflection, with the faces turned to face the same way. Crash faces -Y, his left at +X.

Exporting writes the model of the active object, or of the one model the selected objects are part of, and says why when the scene can't be exported. What's deleted but still in the file until **File > Clean Up > Purge Unused** isn't exported: objects taken out of every scene, and actions nothing plays that have no fake user (the bones' settings of an imported animation still point at it), which exporting names. Give an action a fake user to keep it without playing it.

### What a model is made of

An OGI is its root and what's under it:

- **root**: an empty with the OGI's bounding box and collisions in its **Twin Tech** panel. The game takes the bounding box for its instances' collision when the model has no hulls, their shadows and physics: a model keeps the box it was imported with while its meshes are as they were, and exporting writes a box around the meshes at rest once they change past it (or for a model made in Blender).
- **armature**: the skeleton, a bone for every joint resting where the joint's bind pose puts it. One per model. A bone points down its limb at the child its joint was aimed at, as long as the way to it, with its own axes picked from the joint's. A bone carrying on its parent's chain (pointing within 45 degrees of it) keeps the roll of the bone before it; other bones (roots, a limb branching off further) get X to the model's left when they stand up or hang down (Blender's roll 0 of a spine or a leg) and Z up when they point to the side (an arm's, until it hangs about 40 degrees down). A bone with nowhere to point (nothing below it elsewhere and every joint above it at its place) lies along its parent's, a root of that kind points up. The two sides of a model mirror each other the way Blender mirrors bones. Exporting turns the bones' axes back into the joints' and keeps what the bones are (their axes and lengths), so the next import makes the same bones again, a rig made in Blender included, and an armature scaled under the root (an FBX rig's 0.01) as long as it was shown. A joint's additional rotation (Twin Tech Joint panel) is in the joint's own axes.
- Meshes are what their rigging makes them, the way Blender rigs meshes: a mesh the armature deforms (an Armature modifier) is the skin, the blend skin when it has shape keys; a mesh on a bone (Ctrl+P > Bone or Bone Relative, a **Child Of** constraint, which imports give them, or parented to something on a bone) is a rigid body of the bone's joint; a mesh neither deformed nor on a bone is a rigid body of joint 0. Something following one bone by its Child Of and another by what it's parented to (Ctrl+P > Bone on something imported) is refused: Blender moves it with both, the game with one joint. The checks' **Remove Its Child Of** keeps it on what it's parented to. Something following vertexes (Ctrl+P > Vertex) is refused too: the game moves a part with one joint. What an import puts on bones stays where it is on the model wherever its root is moved.
- Exit points (the points other objects attach to) and collision hulls (the convex meshes the game collides the model with) are typed in the **Twin Tech** panel, on their bones the same way. What follows a bone is placed relative to the bone as Blender shows it, so a body at its joint has no transform of its own and the two sides' mirror like the bones. An exit point shows its joint's own axes whatever way its bone points, they're what the game turns what it holds by: an exit point at its joint is turned from its bone when the bone's axes aren't the joint's. The **Exit Points** and **Collision Hulls** holders imports make only keep them together. The game finds an exit point by its place among them (a character's hand is 0, its head 1), never by its ID: exporting puts them in the order of their IDs and gives them their places as IDs. An object's instances only get as many exit points as the object's **Exit Points** in TT Lab says. The game takes a hull for convex and of 64 vertexes and 64 faces at most: exporting writes a hull that isn't convex as its convex hull, saying so (split it into convex hulls to keep its shape), and refuses one past those counts.

Models, rigid models, meshes, skins and blend skins become a root with their mesh under it.

### The checks

The **Twin Tech Checks** panel (Object properties) lists what keeps the model from being written (objects of a type a model can't have, an armature the game can't take, one material drawing a skin and rigid parts, names past plain ASCII, ...) and what it's written without, each with what selects the objects it's about and fixes it: **Convert to Meshes**, **Make It the Blend Skin**, **Split Into Skin and Rigid**, **Add Emit Colors**. Linked to TT Lab, it also lists what TT Lab found the last time it took the model about the game objects drawing it (a character's exit points, joint IDs and animator, the counts of their headers, the animations their slots play).

### New models

**Add > Twin Tech** (in the 3D viewport's Add menu) makes a new model laid out the way imported ones are:

- **OGI Model**: a root with its bounding box, an armature of joint 0 and a box, a rigid body of joint 0. Rig meshes to the armature the way Blender rigs them, add bones and actions as needed (bones become the next joints). Keep more than one joint, or an exit point: the game draws only the rigid bodies of a model of one joint and no exit points, never its skin (exporting one warns).
- **Skydome**: a root and a dome drawn with the vertex colors of its corners, with a sky's material.
- **Model**: a root and a box with a checkered picture, a mesh the scenery and LODs place.
- **Scenery**: a root with the box new chunks keep their objects in, a 20 unit ground in its **Meshes** and an empty **LODs**, an ambient and a directional light (the arrow points at where the light comes from), the ground again as the **Collision** with a `SURF_DEFAULT_0` material (the collision surface asset a material is named after is its surface) and an empty **Dynamic scenery**. Build on it, meshes anywhere under the root are the scenery's, and **Generate Collision** makes the collision out of them.
- **PS2 Save Icon**: the memory card icon the game's saves get, a 3 unit box with grey vertex colors and a checkered 128x128 texture, one shape and no animation. Its shape keys are the icon's shapes and their values' keys its animation.

**Add to TT Lab's Project** (the Twin Tech Model panel of a game model, skydome or model made in Blender) makes it an asset of the project the running TT Lab has open, in the folder you pick, linked from then on. Its materials and pictures become the model's own: a model imported from another project gets copies of that project's, so the other project's material and picture assets stay as they are. A scenery or a save icon is exported over a scenery's or the save icon's `.tlm` instead.

### Materials

Every material a model's parts draw with is a Twin Tech material: the **Twin Tech Material** panel of the material properties edits everything TT Lab edits of a material, and its node tree draws it the way the game does. A material made in Blender gets the settings the game draws its part with as soon as a part draws with it (a skin's the skinned shader, a rigid part's the unlit one, the render bucket the game's own materials of the kind are in), and the picture wired to its **Base Color** becomes its shader's picture. One material can't draw a skin and rigid parts, the game draws them with other shaders: the checks refuse it, **Split Into Skin and Rigid** gives the rigid parts a copy of their own. Material and picture names are plain ASCII, the game's files keep a byte per character.

The project's materials and pictures are edited in place, every model drawing them shows the change. The panel lists the other models that draw a material, and **Make It This Model's Own** gives the model a copy of its own to change instead. The copy still draws the project's textures: the same button under a shader's project texture (or **Make Its Pictures This Model's Own** on the material) gives it copies of their pictures of its own too, which writing the model makes textures of the project it's written to. Do both to take a model to another project. Pictures keep their size in Blender, TT Lab keeps them at a size the game takes (powers of two of 256 at most).

The panel edits the material's game name, render bucket and up to 4 shaders, each a pass drawn over the ones before it. A shader's sub-panels have its type, picture, filter, coordinates, shading, shadows, fog, context, anti-aliasing, LOD and type parameters; blending (a preset or an own `(A - B) * C + D` formula); the alpha test; the depth test and depth writing; the U and V scrolls with their phases and speeds; and the shader animation. Settings the game doesn't read with the others are grayed out like in TT Lab.

A shader's animation is six tracks (U, V, red, green, blue, alpha). Turn **Animation** on and keyframe a track to animate it (hover it and press I), a track without keys keeps its value. The game's frame `n` is the scene's frame `n * scene fps / animation fps`, and the animation loops over its **Frames**, the last blending into the first; imported animations come with their keys laid out that way and a Cycles modifier. The **From Animation** scrolls take U and V as the texture's offset.

The materials show what the console shows (the decompilation's GS settings and VU1 programs): the texture times the vertex colors, a color of 0x80 drawing the texture at half its PNG's brightness (the PS2 keeps textures at half their PNG's values), the lit types lit by TT Lab's default lights (a third grey ambient light at 4.5 and one from above at 3), the environment maps reading the texture by the half vector of the way to the eye and the normal on the screen, the metallic type by the way to the eye reflected off the surface, the alpha test, blending, the scrolls, and the passes added up in the display's values. A material is its passes emitted over what shows through them: exact where nothing behind shows through and where what's behind is only tinted, colors added to what's behind are added in Blender's linear values. The reflection surface shows what's behind it tinted, without the game's moving of it, the shadows' volumes (Color Only) aren't drawn, and depth tests the preview can't do (Always) draw like the others. Meshes without vertex colors get the grey TT Lab writes for them.

Standard Lit, Lit Environment Map and Lit Reflection Surface light a rigid mesh by its emit colors (the `EmitColor` attribute), not its vertex colors. The PS2 version lights a mesh without them with whatever the model drawn before it left behind, in other colors. When a mesh drawn with one of them has no emit colors, the checks say so, **Add Emit Colors** gives it a copy of its vertex colors, and exporting refuses the model until it has them. The game's own models have them, with their vertex colors' RGB. Painting `Color` afterwards doesn't change them: paint `EmitColor` to light the mesh in other colors. The Xbox version's models have one color, which its shaders light, so they need none.

### Meshes

A mesh holds all parts of the model, the `tt_part` face attribute says which part a face is. Faces given another material become a part of their own. The game's exact values are kept in attributes next to the ones Blender shows (`twin_normal`, `twin_uv`, `twin_joints`, `twin_weights`, `twin_shape_*`, `tt_uv_q` and the alpha flags) and used while Blender's still match them, so don't rename or delete them. A mesh without them still works, it just uses Blender's values everywhere.

- Vertex colors are the `Color` attribute, emit colors `EmitColor`. The game draws the texture times them, 0x80 leaving the texture as the PS2 keeps it (half its PNG's brightness), the materials do the same. The lit shaders take a rigid mesh's emit colors instead of its vertex colors, see Materials.
- A skin's vertex groups are the bones. The game's weights are kept while every joint's share of a vertex stays the same.
- A mesh past 4096 vertexes (the game's biggest skin has 2716) and past what it was imported with is exported decimated, which the export says; a mesh with shape keys is left as it is and only warned about.

### Animations

Every animation of the OGI is an action, with a slot for the armature and one for the blend skin's shape keys. Playing an action on the armature plays its facial part on the shape keys too. Actions keep the game's bytes of the animation they were made from, TT Lab keeps the game's values of everything whose keys still hold them: editing one bone on one frame only changes that.

- Twinsanity lets each joint turn inheriting its parent's scale on or off per animation. Select a bone and use the **Twin Tech Bone Settings Per Animation** panel in the bone properties, it shows the settings for the active action and **Add Current Animation** adds them for an action that has none yet:
  - *Doesn't inherit parent's scale*: the joint ignores its parent's scale. The viewport previews it by switching the bone's `Inherit Scale` between `Full` and `None` as the active action changes, on the armatures of Twin Tech models only: other rigs in the scene keep their bones' settings.
  - *Uses additional rotation*: the game turns the joint's angles in this animation by the joint's additional rotation. Exporting decides it for the joints whose animations aren't the game's own any more, see the rotations below.
- New actions become new animations of the OGI, which game objects can then play: exporting gives a new action, or a copy of one, an ID of its own from 0x8000 up, which it keeps (save the .blend afterwards), so adding or renaming actions never changes what a game object plays.
- An action's frames are its frame range, from its first frame: an imported action has a manual frame range from 0, lengthen or shorten it there. Keys are sampled on every whole frame, the face's too, which always has as many frames as the action.
- Bones turn by the curves of their rotation mode (quaternion, Euler angles or axis and angle), or by the rotation curves the action has when it has none of that mode (actions keyed while the bone turned another way).
- An animation plays at the frames a second it was imported with, a new one at the scene's. The game's animations have 31 at most (all of its own have 25): a faster action is written at a rate it has, every few frames, keeping its length (60 frames a second at 30, every 2nd frame), exporting says so.
- A scaled armature object (FBX rigs come in at 0.01) is fine: its bones' rests and the animations' moves are both written with its scale.
- Only keys are exported: what constraints (IK, Copy Rotation...) and drivers do to bones isn't, exporting warns about such bones. Bake the actions first (Pose > Animation > Bake Action, with Visual Keying).
- Adding bones gives every animation keys for them, imported ones included: every joint of the model reads its animation's keys, and the game's animations only have keys for the joints of their model.
- Rotations are stored as Euler angles with 1/4096 of a turn precision. The game blends a joint from frame to frame angle by angle, each of the three the shorter way round, not along the shortest turn: close to a middle angle of a quarter turn (gimbal lock) that strays far from the turn, and a run's thigh swinging past it twitched every time the run started over. Exporting gives every joint whose animations don't keep the game's values the additional rotation that keeps their angles away from gimbal lock, and turns them by it (in the file, the scene keeps its settings), TT Lab then picks every frame's angles so that the game's blends follow the turns, the last frame's into the first included. TT Lab's viewer plays the animations the way the game blends them.

#### Playing the game's animations on another model

The **Twin Tech Animation Retargeting** panel (Object properties of a model) puts another rig in the model's place, playing its animations:

1. Pick the **Rig**, an armature of the scene, or import one with the button next to it (a model file, like another OGI's). A rig that isn't part of a model is made one.
2. The table pairs every bone of the rig with the joint of the model it stands for. **Guess the Pairs** fills it: the root with the root, bones holding a joint's index or named like a joint with that joint, the others by the way they rest from their parents. Edit any row. A bone pairs with a child of the joint its parent pairs with: the rig's paired bones are a subtree of the model's skeleton. Leave a bone out and everything under it stays out too, those bones become new joints at rest. The panel says what breaks the rule, and retargeting changes nothing until it's fixed. The table is kept with the rig.
3. The shape keys of the mesh the rig deforms pair with the model's the same way (**Pair the Shape Keys**).
4. **Retarget and Replace**: the rig's bones are named and numbered as the joints they stand for (new ones as the next joints), every animation is retargeted to the rig (each bone turns from its own rest as much as its joint does, and moves scaled by how much bigger the rig is) under the model's names and IDs, so the game objects playing it keep working, and the model's exit points and hulls go on the paired bones keeping their offsets, scaled and turned as the rig's bones point from the model's (whatever axes the rig's bones turn about); the rig's own of the same place win. What can't be carried over (joint IDs of joints the rig has no bone for) is listed in the checks. The model is kept hidden in **TT Lab Retarget Sources**, and retargeting the rig again starts from it.

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

`python -m unittest discover -s tests` runs the tests that don't need Blender. The `blender_*.py` scripts run in Blender (`blender --background --factory-startup --python tests/<script>.py -- ...`), each prints what it checked followed by `OK`:

- `blender_roundtrip.py <in.tlm> <out.tlm> [--edit | --customize | --swap | --retarget | --scaled | --collision | --add]` imports and exports the fixtures TT Lab's tests write, which TT Lab's tests check (`TlmFixtureTests`).
- `blender_bone_axes.py [<out.tlm> <edited out.tlm>]` imports a character whose joints are aimed and mirrored the way the game's are (`fixtures/ogi_mirrored.tlm`), checks its bones point down their limbs with the twins mirrored and the middle line's X to the left, that an export imported again (a rig made in Blender too) has the same bones, and exports it unchanged, after edit mode, posed and edited, the exports TT Lab's tests check (`TlmFixtureTests`). It also checks the other ways parts go on bones (Bone Relative, under a body, a Child Of left on a part parented to a bone, taken off a bone), bone lengths through exports, a scaled armature, and saving and syncing in Edit Mode.
- `blender_templates.py <ogi> <scenery> <save icon> <skydome> <model>` makes the templates, checks them and exports them into the fixtures TT Lab's tests read.
- `blender_retarget.py` retargets the fixture model's animations to rigs from a file and from the scene, a rig breaking the subtree rule refused, and the mirrored character's exit points and hull to a rig of rolled bones (where they were) and one of turned forearms (turned with them), checked in the exported file.
- `blender_materials.py`, `blender_emit_colors.py`, `blender_deleted.py` and `blender_reimport.py` check the materials against a project they make up, the emit colors, exporting with deleted parts and importing a model the scene has.
- `blender_link.py` links the fixture model to a TT Lab stood in for by a process of its own (`fake_tt_lab.py`: Blender 4.5 crashes now and then while Python threads of its own process answer) and puts a copy of it in another model's place, `blender_edit_from_tt_lab.py` runs what TT Lab's Edit in Blender runs.
- `blender_use_blend.py` puts models in TT Lab models' places against the stand-in TT Lab, which holds, refuses, conflicts, closes and pushes answers, keeps a revision per model and answers `link_model` per model file as TT Lab would (LINK.md's Revisions): a pending choice holding the scene's models back, answers about the file a model had, a refused replacement written by the save once fixed, saves while no TT Lab has the project open, pictures that can't be read, paths of another system, TT Lab's `use_for` and `released`, undo, conflicts during saves and Take TT Lab's (TT Lab's materials taken with the model, a late answer leaving the model as it was), messages of closed files, stale replacements undone against a live link and pending ones (kept with the blend file), replacements in flight (the blend file saved again once taken) and ones TT Lab's changes don't mark, deleted models and pictures, a file of several candidates, another project's model made this project's while the scene's models of that project (of the same name, so the same URIs) keep their materials, pictures and changes through TT Lab's answers, pushes, imports and saves, copies split by project, the indexed walks, the pictures an export's record confirms, revisions of files read and written without TT Lab, `model_changed`, `moved` and `deleted` of models, Edit in Blender selecting the link's model, Write Blender's Over Them forcing only what it asked about, a replacement TT Lab doesn't link, undoing Take TT Lab's and a use_for, Retarget and Replace keeping the link, released files through undo and a failed save, a save while TT Lab quits, and the sync settings (Only Manually, Prompt to Sync on Save and its Sync saving the blend file again, the Sync with TT Lab button, a painted picture TT Lab saves without a sync). It and the other scripts checking what saving writes set Always on Save.
- `blender_tt_lab_link.py`, `blender_tt_lab_new_model.py` and `blender_tt_lab_use_blend.py` edit, add and put in place models through a real TT Lab: TT Lab's tests run them (`BlenderLinkEndToEndTests`) when `TTL_BLENDER` names a Blender.

## Developing or making an changes

Install [uv](https://github.com/astral-sh/uv) and run `uv sync` to initialize the virtual environment and install the dependencies:

```bash
uv sync
```

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
