# Turian UI

In-game UI built on Guinevere: `.ui` documents, stylesheets, data binding, and screen or world-space panels.

Licensed under MPL-2.0. Version and engine compatibility are in `package.json`.

## Install

```sh
turian-cli brick add org.mass4.turian.ui
```

Or enable `org.mass4.turian.ui` from the Bricks panel.

## Use

Add a `UiDocumentComponent` to a node and point it at a `.ui` document. A document lists its elements, an optional controller type and its stylesheets (`<Style src="menu.pss" />`). Stylesheets are `.pss` files in PSS syntax (`property = value;`).

## Render from the command line

The brick adds `turian-cli ui`, which renders a `.ui` document to a PNG without a scene or GPU. Run it inside a project that installs the brick, or name the project with `--cli-project`:

```sh
turian-cli --cli-project path/to/game ui --file Assets/UI/menu.ui --out menu.png --width 1280 --height 720
```

Bind data with `--data '{"Score": 7}'` or `--data-file data.json`.
