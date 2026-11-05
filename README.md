# Turian UI

In-game UI built on Guinevere: `.ui` documents, stylesheets, data binding, and screen or world-space panels.

Licensed under MPL-2.0. Version and engine compatibility are in `package.json`.

## Install

```sh
turian-cli brick add org.mass4.turian.ui
```

Or enable `org.mass4.turian.ui` from the Bricks panel.

## Use

Add a `UiDocumentComponent` to a node and point it at a `.ui` document. A document lists its elements, an optional controller type and its stylesheets (`<Style src="menu.uss" />`). Stylesheets use PSS syntax in `.uss` files.
