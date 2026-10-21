# Turian UI

In-game UI based on Guinevere: UI documents, USS stylesheets, binding, images and screen or world panels.

Licensed under MPL-2.0. See `package.json` for version and engine compatibility.

## Install

```sh
turian-cli brick add org.mass4.turian.ui
```

Or enable `org.mass4.turian.ui` from the Bricks panel.

## Source and baked packages

The regular brick contains editable code under `Runtime/` and/or `Editor/`; its assembly definitions let any Turian client compile it without an engine checkout. Build and test projects live under the opaque `Brick~/` roles. Keep committed `.meta` identities when editing or publishing.

`turian-cli brick pack . --precast --twin --out .bricks` compiles the code, imports the assets and writes the regular brick plus its `.bake` twin. The source archive omits generated payloads; the twin includes the assemblies and verified import cache, with no C# source. `brick publish . --store <store> --key <key> --precast --twin` performs the same producer steps before upload; protected production CI signs and uploads the qualified archive bytes.

Consumers can enable `brick twins on --project <project>`, then `brick swap org.mass4.turian.ui source --project <project>` to download and embed writable originals at the same version. Swapping back to `twin` preserves the edited fork in the project trash. GLSL edits in HZB require rebuilding its shader payload with the build project before packing.
