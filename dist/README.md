# Prebuilt KittenRemoteControl (drop-in)

This folder contains a **prebuilt** copy of the mod so you can install it without building.

## Install
Copy the `KittenRemoteControl` folder here into your game's mod directory, e.g.:

```
<Game>/Content/KittenRemoteControl/
```

Contents:
- `KittenRemoteControl.dll` — the mod
- `KittenRemoteControl.deps.json`
- `mod.toml`

`0Harmony.dll`, `StarMap.API.dll` and the game's own DLLs are provided by the StarMap loader / game
at runtime, so they are intentionally **not** included here.

## Build info
- Target: **net9.0** (binds against the game's bundled runtime under Wine/CrossOver).
- Rebuild from source with `dotnet build KittenRemoteControl/KittenRemoteControl.csproj -c Release`
  (see the top-level README). This binary may lag behind `main`; rebuild if in doubt.
