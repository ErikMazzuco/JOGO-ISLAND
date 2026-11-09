# Stylized Tropical Island Nature Pack: Free Sample (v1.0)

**16 free assets** from the Stylized Tropical Island Nature Pack: hand-painted, low-poly tropical nature for adventure,
survival and pirate games. Real-world scale, one mesh and one material per asset, Godot-ready GLBs plus FBX and .blend.

| Category | Sample assets |
|---|---|
| Trees | Tree_Palm_Tall_Curved_01, Tree_Broadleaf_Round_01, Tree_Sapling_Palm_01 |
| Rocks | Rock_Boulder_Grey_Round_01, Rock_Mossy_01, Rock_Small_Round_01 |
| Cliffs | Cliff_Straight_01 |
| Foliage | Bush_Round_01, Grass_Clump_Medium_02, Fern_Arching_01, Flower_Hibiscus_01, Plant_Banana_01 |
| Coastal | Driftwood_Log_01, Shell_Conch_01, BeachGrass_Clump_01, Coral_Brain_02 |

The **full pack has 105 assets**: 14 trees, 26 rocks, 4 modular cliff pieces, 37 grasses, bushes, flowers, ferns, vines and seaweeds,
and 24 coastal props (sea arch, sea stacks, reef, coral, shells, driftwood, tidepools). It also includes 10 seamless terrain
textures, 9 water-shader maps, and a placement manifest for procedural scattering.

## What's included
| Folder | Contents |
|---|---|
| `Models/<Category>/` | One GLB per asset. Textures are shared through `../../Textures/Atlases/`. |
| `FBX/<Category>/` | One FBX per asset, using the same atlases. |
| `Blender/Tropical_Island_Nature_FreeSample.blend` | The sample assets plus two terrain material tiles (Blender 5.2) |
| `Textures/Atlases/` | The 4 shared atlases (2048²). They are the same atlases as the full pack, so the full pack drops in alongside. |
| `Textures/Terrain/` | 2 seamless ground textures: golden sand and tropical grass |
| `Metadata/assets.json` | Placement data for the sample assets: zones, slope, spacing, collision and MultiMesh hints |
| `Previews/` | The sample board and the cover |

## Quick start
- **Keep the folder layout.** The GLBs and FBXs find the atlases by relative path.
- **Godot 4:** copy the folder into your project. Default import settings work.
- **Unity:**
  - Copy the folder into `Assets/` and use the FBX files, or import the GLBs with glTFast.
  - Set the foliage material to **Render Face: Both**.
- **Unreal:** import the FBX files with *Import Textures* on, and tick **Two Sided** on the foliage material.
- **Conventions:**
  - Metres, real-world size.
  - +Y up in GLB and FBX.
  - Origins sit at the bottom centre, at ground level.
  - `M_Foliage_Atlas` is double-sided; the others are single-sided. All are opaque.

Made by Zenthrall Games.
