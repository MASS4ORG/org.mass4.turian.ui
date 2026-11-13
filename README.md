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

## Keyboard and gamepad navigation

Buttons, image buttons, toggles, sliders, dropdowns, text fields and tab headers receive focus. Disabled controls and elements with `focusable="false"` are skipped. Set `<UI initial-focus="play" cancel="OnBack">` to choose the initial control and an unclaimed Cancel handler; otherwise the first enabled control receives focus. Style the current control with `:focus` in PSS.

Arrow keys, D-pad and the left stick navigate spatially. Enter/Space or the south face button submits; Escape or the east face button cancels. Tab/Shift+Tab follows document order. Override neighbors by element name with `focus-left`, `focus-right`, `focus-up`, `focus-down`, `focus-next` and `focus-previous`; a missing or disabled neighbor falls back to automatic navigation. For example, `<Button name="play" focus-down="options" click="OnPlay">Play</Button>` links to the control named `options`.

Sliders use their own axis to change value and the perpendicular axis to leave the control. Dropdowns use up/down for selection and left/right to leave while closed; Cancel dismisses an open dropdown before the document handles it. Tab headers submit normally, and the shoulder buttons select the previous/next tab.

Assign `UiDocumentComponent.NavigationActions` to a custom `InputActionsAsset` to rebind navigation. Define a `UI` map with `Navigate` (Vector2, positive Y down), `Submit`, `Cancel`, `PreviousTab` and `NextTab` (Button). `UiNavigationActions.CreateDefault()` provides editable defaults. A composited overlay uses its highest-sort-order panel's map; world panels use their own. Each runtime copies the map's mutable action state, and `UiRuntime.Navigation.Actions` exposes its rebinding service. Text fields retain their editing keys while focused.

## Render from the command line

The brick adds `turian-cli ui`, which renders a `.ui` document to a PNG without a scene or GPU. Run it inside a project that installs the brick, or name the project with `--cli-project`:

```sh
turian-cli --cli-project path/to/game ui --file Assets/UI/menu.ui --out menu.png --width 1280 --height 720
```

Bind data with `--data '{"Score": 7}'` or `--data-file data.json`.
