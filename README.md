# Roll and Render

A 3D tabletop map builder in Unity 6, the groundwork for a D&D map builder.
Place, rotate and delete blocks on a big grid, 1 cell = a 5 ft square.
Tech demo for now, not a full app.

![demo](docs/demo.gif)

## What I solved

- **Block picking w/o colliders** - a grid raycast (DDA) walks the ray cell by cell to find the block you're pointing at. No physics colliders, so it scales as the world grows and loads faster
- **Chunked world** - 400 x 400 cells split into 16 x 16 x 16 chunks, drawn w/ GPU instancing in batches of 1023
- **Only what you can see** - fully buried blocks are skipped when a chunk rebuilds
- **No stutter on edits** - chunk rebuilds are spread over frames w/ a cell budget, the old chunk keeps drawing until the new one is ready
- **No seams** - editing a block on a chunk edge rebuilds the neighbor chunk too
- **Placement that feels right** - aiming near the top of a block's side snaps to the top, and looking down fills the hole below instead of floating
- **Ghost preview** - shows where the block will go, Q / E rotates it

## Controls

| Key | Does |
| --- | --- |
| W A S D | Move |
| Middle mouse | Orbit the camera |
| Scroll | Zoom |
| Left click | Place block |
| Right click | Delete block |
| Q / E | Rotate |

## Built with

Unity 6 (6000.3.5f2) · URP · Cinemachine 3 · Input System · Shader Graph (grid shader)

## Where to look

- `Assets/Scripts/Kernel.cs` - picking, placement rules, the DDA raycast
- `Assets/Scripts/WorldBuilding/TileWorld.cs` - chunks, rebuilds, instanced drawing
- `Assets/Scripts/Player.cs` - camera movement, orbit and zoom

Open `Assets/Scenes/SampleScene.unity` to try it.
