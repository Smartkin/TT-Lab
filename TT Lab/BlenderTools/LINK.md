# The Blender link

The Blender add-on links a scene's models to the TT Lab that has their project open. Linked, what Blender makes and changes of the models'
materials and pictures is the project's at once (unsaved, an undo step of TT Lab's), what TT Lab changes of them comes into Blender (an
undo step of Blender's), and syncing writes the models through TT Lab: the Twin Tech Model panel's Sync with TT Lab, or saving the blend
file as the extension's preference says (Prompt to Sync on Save, the default, asks once the blend file is saved; Always on Save; Only
Manually). Every save has TT Lab save what Blender made and changed of the materials and pictures. Without a link syncing writes a model
into its own file with what it made or changed of the materials and pictures (TLM.md), which TT Lab takes when it reads the file.

## Finding TT Lab

A running TT Lab listens on a free port of the loopback (127.0.0.1) and writes a discovery file into its state folder, `blender` under
`$XDG_STATE_HOME/tt-lab` (`~/.local/state/tt-lab` when it isn't set) on Linux and `%LOCALAPPDATA%\TT Lab\Blender` on Windows, named
`link-<pid>.json`, readable by its user only:

```json
{ "protocol": 1, "port": 51234, "token": "<64 hex digits>", "pid": 1234, "executable": "/opt/TT Lab/TT Lab", "version": "2.0.0",
  "addon_version": "1.0.0", "project": { "uuid": "<UUID>", "path": "/projects/Mod", "name": "Mod" } }
```

`project` is `null` while no project is open. TT Lab writes the file again when the project changes and deletes it when it quits; the
add-on leaves out the file of a process that's gone (a TT Lab that crashed), asking Windows for the process's exit code. The
add-on links a model whose file is in a project to the TT Lab with that project open, by its UUID, else by its folder. A Blender of the
Flatpak reads the host's state folder.

## Frames

Every message is a frame: the header's length and the payload's length as unsigned 32 bit little endian numbers, the header (JSON in
UTF-8) and the payload (bytes, a model file or a PNG, often none). A header has a `type`, an `id` (the sender's, counting up) and, for
an answer, `re`, the `id` of the message it answers. An answer that failed is `{ "type": "error", "re": <id>, "text": "<why>" }`.

## From Blender

| Type | Fields | Payload | Answer |
|---|---|---|---|
| `hello` | `token`, `protocol`, `blend` (the blend file's UUID), `path` (the blend file, empty while it isn't saved), `blender` (Blender's version), `addon` (the add-on's) | | `welcome`: `project` (like the discovery file's), `version` (TT Lab's; the add-on version it comes with is the discovery file's); an `error` and the socket closed for a wrong token or protocol |
| `link_model` | `path` (the model's data file), `materials` and `pictures` (the URIs of the materials Blender's model draws and of their pictures), `revision` (what Blender has of the model, see Revisions) | | `model`: `uri`, `revision` (TT Lab's), `changed` (TT Lab's model isn't what Blender has), `materials` (every material TT Lab's model or Blender's draws: `uri`, `name`, `data`, `blender_id`), `pictures` (every texture they draw: `uri`, `name`, `hash`, the SHA-256 of its PNG, `blender_id`), `missing` (the URIs of `materials` and `pictures` the project has no material or texture of) |
| `unlink_model` | `uri` | | `ok`: the Blender has no model of it any more (its model was given another model's file) |
| `get_model` | `uri` | | `model`: `uri`, `revision`, the model's file as TT Lab has it (its unsaved changes too) as the payload: Take TT Lab's, and Import the Model Again of a linked model |
| `get_picture` | `uri` | | `picture`: `name` (the texture's), the PNG as the payload |
| `create_picture` | `owner` (the model's URI), `blender_id`, `name` | PNG | `created`: `uri`. The model's picture of the Blender ID when it has one already, which takes the PNG |
| `update_picture` | `uri` | PNG | `ok` |
| `create_material` | `owner`, `blender_id`, `name`, `data`, `images` (like TLM.md's, `blender_id` and `uri` without `png`) | | `created`: `uri`. The model's material of the Blender ID when it has one already, which takes the data |
| `update_material` | `uri`, `data`, `changed`, `shader_sources`, `images` (TLM.md) | | `ok` |
| `rename` | `uri`, `name` (plain ASCII, a picture's without its file's extension) | | `ok`: `uri` |
| `check_model` | `uri` | model file | `checked`: `problems` (`text`, `refused`, `objects`) |
| `write_model` | `uri`, `revision` (what Blender has of the model), `force`, `replacing` (a model of the blend file put in the model's place, with `name`, its name, and `blend`, the blend file) | model file | `ok`: `revision`, `problems`; `conflict`: `revision`, `changed`, when TT Lab's model isn't what the revision says Blender has and `force` isn't set; `refused`: `text`, `problems`, what keeps TT Lab from taking it (a file it can't read, what a character drawing it needs). A file of what TT Lab has already is no step and saves nothing. With `replacing` the other Blenders linking the model are told it's `released` |
| `save` | `uris` (the materials and pictures Blender made or changed since it was saved last) | | `ok` |
| `drop` | | | `ok` |
| `blend_saved` | `path` (the blend file), `models` (the URIs of the models the blend file has as TT Lab has them: linked, not changed in TT Lab since, not waiting to take another model's place), `released` (the data files of this project's models the blend file had models of and doesn't any more), `released_projects` (the UUIDs of other projects the blend file has no model of any more) | | `ok`: Edit in Blender opens it for the models listed, and no longer for the released ones it opened it for, nor for any model of the released projects (on every TT Lab the blend file is linked to) |
| `open_asset` | `uri` | | `ok` |
| `open_project` | `project` (its folder), `path` (a model's data file) | | `ok`: TT Lab opens the project, asking about its own unsaved changes first, then the model |
| `new_model_targets` | | | `targets`: `folders` (`uri`, `label`), where Create Asset makes models |
| `create_model` | `name`, `folder` | model file (OGI, skydome or mesh) | `created`: `uri`, `path` (its data file, which Blender links by) |
| `ping` | | | `ok` |

A sync writes the models: `write_model` for every model, then `save`. A save of the blend file that doesn't sync sends only `save` (what
Blender made and changed of materials and pictures, which TT Lab has already), so `drop` doesn't take back what the blend file keeps: a
picture taken back was taken for changed in TT Lab when the blend file was opened again. A sync's prompt saves the blend file again
once TT Lab answered, so it keeps the revisions. A model's file names its
materials by their URIs (TT Lab has them as Blender shows them), only one the link didn't make yet comes with its settings and pictures
(TLM.md). What `link_model` says is `missing` (deleted in TT Lab while Blender was closed, another project's) Blender makes again, the
model's own. `drop` (the blend file closed without saving) takes back what Blender made or changed since it was saved: the materials and
pictures it made go, the ones it changed lose its steps since anyone else's (see below). A closed socket without `drop` does the same.

What Blender makes is the project's at once (in the project tree, marked as Blender's), unsaved: it has no files until `save`. What it
changes goes into the asset's document as a step of its history, "From Blender: ..." (the asset's tab once it loaded, else a document of
no tab that the tab takes over when it opens, with its history), a picture as its data replaced whole, which the step keeps before and
after as its PNG. A model written is a step of the model's document too, which keeps the model before it: undone, TT Lab has the model as
it was, unsaved. A document with unsaved changes gets the model as one more unsaved step and its file stays what it was until TT Lab saves
it. While a step of an open document keeps a model file drawing a material or picture made in Blender, TT Lab doesn't delete it as unused.
`drop` undoes the dropping Blender's steps since anyone else's last one (TT Lab's, another Blender's), what a tab saved meanwhile stays; a
document of no tab that let go of the oldest of them (it keeps 8 steps of data replaced whole, a tab 20) reads the asset's files again.

## Revisions

A model's revision is what tells TT Lab and Blender whether they have the same model. TT Lab's is the SHA-256 of the model's data file while
no open document has unsaved changes of it (the model's own, its chunk's scene, one changing it through a link), else of its data as TT
Lab writes it, the file's once it's saved: steps that leave the model as it was (a chunk's scene moving instances, a rename) leave it, and
so does saving what a Blender wrote. Blender keeps the revision TT Lab last answered or told it, `file:<SHA-256>` of a file it read without
TT Lab, or `exported:<SHA-256>` of what it wrote into the model's file without TT Lab, which stays TT Lab's model while the file is still what
TT Lab wrote again of it when it took it (`TakenExports`). A revision of no kind is the model as saved (an older add-on's). `link_model`
says whether TT Lab's model is what Blender has (`changed`), `write_model` refuses to write over another (`conflict`) unless forced.

## From TT Lab

| Type | Fields | Payload |
|---|---|---|
| `material_changed` | `uri`, `data` | |
| `picture_changed` | `uri`, `hash` | |
| `renamed` | `uri`, `new_uri`, `name` | |
| `deleted` | `uri` | |
| `model_changed` | `uri`, `revision` | |
| `moved` | `uri`, `new_uri`, `path` (its data file now) | |
| `edit_in_blender` | `uri` | |
| `use_for` | `model` (a model's data file), `asset_type` (its model file's type), `name` | |
| `released` | `uri`, `name`, `by` (the model that took its place and its blend file, "Reimu of reimu.blend") | |
| `closing` | | |

TT Lab tells a linked Blender what changes of the assets its models use once it stays the same for 0.3 s (an edit, an undo, a save, a
rename in the project tree, changes discarded), never what that Blender itself changed. A material's `data` is all of TT Lab's, which
Blender merges with what it changed since (`link_state.relink`). A picture's `hash` is the SHA-256 of the PNG Blender sent while the
texture is still what TT Lab made of it (the texture's `BlenderPng` and `BlenderPicture` parameters; the game's textures are 256 pixels
across at most, Blender keeps its picture), else of TT Lab's PNG. `deleted` is of a material or a picture the Blender's models draw: Blender
makes it again, the model's own (a material made in Blender, a picture without its texture); of a model, the Blender's model stands for no
file any more. `model_changed` says TT Lab's revision of the model changed (once per revision, never for what that Blender wrote): the add-on
marks the model changed in TT Lab when it isn't the revision it has, and its next sync asks what to do with it (overwrite, take TT Lab's through
`get_model`, or leave it). `moved` is a model moved in TT Lab (another folder or package): the add-on's model is the model of its new URI
and file, also once an undo gave it the old file back. `closing` comes before TT Lab quits or opens another project: what Blender made or changed that wasn't saved goes, and the add-on's
syncs write its models into their own files from then on.

URIs are their project's, and two projects of one name have the same ones: of the materials and pictures of a URI the add-on goes by the
one the scene's models of the link's project draw, else one no other project's model draws (left over by a deleted model, a model of no
file's), never another project's (`materials.ProjectBlocks`, a copy of the project, by its UUID, being the project; a model of no file is of
no project). Importing a model shares a material or picture of a URI with the scene's models of its project only. A material Blender
couldn't read from the project's files (drawn magenta, no settings of its own) takes TT Lab's settings as they are.

`edit_in_blender` is TT Lab's Edit in Blender of a model this Blender has linked already: the add-on selects it. `use_for` is TT Lab's
Use an Existing Blend File of the blend file this Blender has open (its `hello`'s or last `blend_saved`'s `path`): the add-on puts its model
of the type in the model's place, like the command line below. `released` says another Blender's model took the place of this Blender's
model of `uri`: the add-on's model stands for no file any more, so its saves can't take the place back.

## Without a linked TT Lab

Open in TT Lab connects to any running TT Lab and sends `hello` and `open_project`. With none running it starts TT Lab with
`<project folder> --open-file <model file>`, by the command TT Lab leaves in `tt-lab.json` in its state folder (`{"command": [...],
"version": ...}`, through `flatpak-spawn --host` inside the Flatpak). Add to TT Lab's Project does the same with `new_model_targets` and
`create_model` for a model made in Blender, what it draws of another project made its own first (like Use an Existing Blend File); a model
that stood for another TT Lab model sends `unlink_model` for it and releases its file, and links as the new model with no revision. TT Lab's Edit in Blender starts Blender with the model's blend file, or, the first time, with
`--python-expr` running `ttt.edit_from_tt_lab` with the model's data file and the blend file after `--` (the model imported into an empty
file saved where the user put it, the model's file left as it is); it remembers each model's blend file in `blend-files.json` and the add-on version it installed into each Blender in
`installed-addons.json`, both in its state folder. Use an Existing Blend File starts Blender with the blend file picked (in the blend file's
folder), `--python-expr` and, after `--`, the model's data file, its model file's type and its name (arguments, so any path survives). The
expression runs `ttt.tt_lab_request` at once, which keeps the scene's models of the type from linking until one is picked, then, once
Blender's window is up, `ttt.use_for_tt_lab_model` (the user picks one of several): it's given the model's file, the other model of the
file stands for none (`unlink_model`) and the file it had is `released`. Another project's model (another UUID) draws that project's
materials and pictures, which become the model's own (made again in this project, copied when the scene's other models draw them too)
whatever this project has at their URIs; a model of no file, or of another system's file, is the project's it's put in. A replacement
TT Lab doesn't link (it has no model of the file) is a model of no file then, which no save writes into that file. Once it's linked, and what it draws is the project's, Blender sends `write_model` with
`replacing` and `force`, then `save`, and saves the blend file. A replacement TT Lab refuses is said why and waits: syncing
once it's fixed writes it as a replacement. Saved while no TT Lab has the project open, the model's file is left alone until one does.

## What the blend file keeps

The scene keeps the blend file's UUID and the data files released that no TT Lab was told of yet (`ttt_released`). Each model root keeps
its URI, its kind, its revision (`ttt_revision`, see Revisions), whether TT Lab changed it since (`ttt_changed_in_tt_lab`) and, put in
another model's place and not taken yet, that model's name and data file (`ttt_replaces`, waiting while the root still has that file).
What TT Lab said of a model is kept by the root's `ttt_uid` too: undo putting back another model of the name (the one before Take TT
Lab's) gives it what it had, and a model of the name that isn't the one linked (its file imported again) links its own.
Each material and image keeps its URI, its Blender ID and the snapshot of what TT Lab had last (the material's `base` and `known`, the
image's `ttt_png_confirmed`). Linking again merges three ways with the snapshot as the base: what Blender changed since goes to TT Lab,
what TT Lab changed comes into Blender.
