# GRAVE SHIFT

A one-thumb graveyard survival shooter for the browser (Unity 6 WebGL). Live: https://graveshift.vercel.app

## The hook: LANTERN
- The graveyard is **pitch dark**. Your lantern's light is everything:
  - **Your range.** Every weapon only targets and hits what the light reaches; shots fizzle in the dark.
  - **Your life clock.** The lantern burns down, faster as the night goes on. Let it run dry and the dark bites you every second.
- **Oil** turns up out in the dark every 6 to 9 seconds, and tough undead (brutes, mages, vampires, bosses) often drop it. Each can widens the light. Level-ups make it flare a little, and Lantern Aura levels raise its maximum.
- Undead outside the light show only their **glowing eyes**.
- An oil gauge sits under your health bar.
- Analytics: `oil_pickup` (value = cans this run), `dark_bite`.
- Code: `Lantern.cs` (radius, darkness sheet, eyes, oil spawns). The darkness is a ring mesh with a soft hole, floating above every model at y = 6.5 and lined up on the camera-to-hero ray. Weapon reach is `Lantern.Reach`, used in `Arsenal`.

## Play
- Move with one thumb (or WASD/arrows); aiming and firing are automatic.
- Survive 8:00 until dawn. Bosses arrive through the night. Pick upgrades on level-up, and spend gold in the shop between runs.

## Build
```bash
"C:/Program Files/Unity/Hub/Editor/6000.6.3f1/Editor/Unity.exe" -batchmode -nographics -projectPath unity -executeMethod GraveBuild.WebGL -quit -logFile build.log
node tools/serve.mjs 8082
```
`?bot=1` is an autoplay attract mode; it also goes for oil when the lantern is low.

Assets: Kenney Graveyard Kit, Mini Characters, Mini Dungeon, Blaster Kit; KayKit Skeletons and Halloween Bits (all CC0).
