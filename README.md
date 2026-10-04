# <img src="TT%20Lab/Images/tt_lab_logo.png" alt="TT Lab's icon" width="48"> Twin Tech Lab

Twin Tech Lab (TT Lab) is a modding toolkit for **Crash Twinsanity**. It unpacks the game's disc files into a project of editable assets, lets you change them in an editor with a 3D viewport, and builds them back into files the game reads: the PS2 version's archive and ISO, or the Xbox version's disc image. Levels can be played in PCSX2 straight from the editor.

Both versions of the game are supported, the PS2 version most of all. Xbox support is newer and less tested. TT Lab comes without any game data: creating a project takes the files of your own copy of the game.

The repository holds the editor (`TT Lab`), the library that reads and writes the game's formats and its behaviour scripting language (`TwinTech`), and a Blender add-on for the game's models (`TT Lab/BlenderTools`).

# Features

- **Projects made from the game's discs**: every level chunk, object, model, texture, script and sound becomes an asset of the project, with the PS2 and Xbox versions side by side.
- **Level editor**: a 3D viewport that draws the level the way the game does (its lighting, materials, skies and particles), where objects, triggers, cameras, paths, AI navigation, chunk links, particle emitters and lights are placed, moved, turned and scaled. Selections can be saved as prefabs and placed in other levels.
- **Asset editors**: game objects, materials and their shader animations, textures, fonts, PSM pictures, the game's text in its own fonts, sounds and their loops, particle systems, cameras, collision surfaces, the memory card icon and more, all with undo and redo.
- **AgentLab**: the game's behaviour scripts decompiled into a readable language and compiled back byte for byte, with code completion, parameter hints, live error checking and jumping between behaviours.
- **Blender add-on (Twin Tech Tools)**: imports and exports models, skins, animations, sceneries, collision and the memory card icon, keeps whatever wasn't edited exactly as the game had it, starts new models from templates, makes a level's collision out of its meshes and retargets animations to other skeletons.
- **Hot reload**: assets changed outside TT Lab, like a script saved in a text editor or a model exported from Blender, reload in the editors showing them.
- **Building**: PS2 archives and ISO images, Xbox disc images, build profiles that pick the levels to build, and a cache that only rebuilds what changed.
- **Playing in PCSX2** from a level's viewport, rebuilt and reloaded after saving if you like, with a level select added to the PAL release's pause menu.
- **Music and videos** opened with the twinstudio tools (twinmusic and twinpss).

# Building

TT Lab runs on Windows and Linux, on a graphics driver with OpenGL 4.6. You need:

- the [.NET SDK](https://dotnet.microsoft.com/download), version 8 or newer;
- Python 3, to package the Blender add-on and download the twinstudio tools;
- Blender 4.5 or newer, for the add-on.

Build and run the editor:

```bash
git clone https://github.com/Smartkin/TT-Lab.git
cd TT-Lab
dotnet build "TT Lab.sln"
dotnet run --project "TT Lab/TT Lab.csproj"
```

Windows and Linux code is picked by the operating system doing the build, so build TT Lab on the system it's going to run on.

The Blender add-on ships next to TT Lab when it's packaged before building. This writes `TT Lab/BlenderTools/dist/twin_tech_tools-<version>.zip`, which Blender installs from Edit > Preferences > Add-ons > Install from Disk:

```bash
python "TT Lab/BlenderTools/scripts/package_addon.py"
```

[The add-on's README](TT%20Lab/BlenderTools/README.md) explains how to work on it.

The twinstudio tools (twinmusic for the MH/MB music archives, twinpss for the PSS videos) come from their [releases](https://github.com/Smartkin/twinstudio/releases). Tools > Install the tools in TT Lab downloads them, or fetch them before building so they're copied next to TT Lab:

```bash
python scripts/fetch_tools.py --os Linux   # or --os Windows
```

Run the tests (they need no game data):

```bash
dotnet test "TT Lab.Tests/TT Lab.Tests.csproj"
cd "TT Lab/BlenderTools"
python -m unittest discover -s tests
```

A release build is a single executable, with the native libraries, the Blender add-on and the tools next to it:

```bash
dotnet publish "TT Lab/TT Lab.csproj" -c Release -r linux-x64 --self-contained false -o publish   # win-x64 on Windows
python scripts/fetch_tools.py --os Linux --out publish/tools
```
