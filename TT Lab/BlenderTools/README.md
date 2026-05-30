# Blender Twin Tech Tools Add-on

Created by using [blender-addon-template](https://github.com/b0o/blender-addon-template) by Maddison Hellstrom

This is a toolkit for working and creating Twin Tech Lab(TT-Lab) graphics in Blender. It uses [uv](https://github.com/astral-sh/uv) to manage dependencies and virtual environments.

## Getting Started

Install [uv](https://github.com/astral-sh/uv) and run `uv sync` to initialize the virtual environment and install the dependencies:

```bash
uv sync
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
