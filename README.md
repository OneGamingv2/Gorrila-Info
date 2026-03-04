# GorillaInfo

A simple Gorilla Tag checker mod I made for finding cheaters.

It shows player info, lets you scan for common mod flags, and adds nametags + quick actions in a hand menu.

## What it does

- Hand menu with pages (Main, Misc, Settings, Actions, Lobby, Music)
- Player info (name, platform, FPS, world scale, color, account date)
- Mod scan + mod list display
- Lock-on and nametags
- Lobby player select buttons
- Basic media controls page

## Build

This project targets **.NET Framework 4.7.2**.

```bash
dotnet build GorillaInfo.csproj -c Debug
```

Output DLL:

- `bin/Debug/net472/GorillaInfo.dll`

## Install

1. Make sure BepInEx is installed for Gorilla Tag.
2. Copy `GorillaInfo.dll` to:
   - `Gorilla Tag/BepInEx/plugins/`
3. Start the game.

## Controls

- Open/close menu: `Left Y`
- Use finger sphere to press menu buttons
- Gun/target features: use the menu settings/actions

## Notes

- If copy fails, close Gorilla Tag first (DLL may be locked while game is running).
- This is a free personal project and still being tuned.

## Credits

- Built for Gorilla Tag modding with BepInEx + Unity/Photon game APIs.



# This product is not affiliated with Gorilla Tag or Another Axiom LLC and is not endorsed or otherwise sponsored by Another Axiom LLC. Portions of the materials contained herein are property of Another Axiom LLC. © 2026 Another Axiom LLC.