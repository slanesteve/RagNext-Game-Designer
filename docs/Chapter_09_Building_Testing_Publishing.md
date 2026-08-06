# Chapter 9: Building, Testing & Publishing Your Game

Once your game world is crafted, RagNext makes playtesting and multiplatform publishing straightforward. This chapter covers live playtesting, exporting game bundles, and publishing for desktop, mobile, and web.

---

## 9.1 Live Playtesting in Studio

Click **▶ Playtest** in the top action bar of RagNext Studio to launch the embedded Unity runtime engine inside the IDE.

### Playtest Features
- **Real-Time Log Output**: View action executions, variable updates, and command routing live.
- **Hot-Reloading**: Edit room descriptions, hotspot coordinates, or variables and see changes take effect instantly.

---

## 9.2 Exporting Game Bundles

When publishing your game, Studio exports a standalone **Game Bundle**:

- `data.json`: Comprehensive serialized game data file (Rooms, Objects, Variables, Hotspots, Actions).
- `MediaAssets/`: Audio files and artwork images.

---

## 9.3 Multiplatform Deployment

RagNext Player compiles into standalone native executables:

| Target Platform | Package Format |
| :--- | :--- |
| **Windows** | `.exe` standalone installer / zip |
| **macOS** | `.app` application bundle |
| **Linux** | Universal binary |
| **Mobile (iOS / Android)** | `.ipa` / `.apk` standalone app |
| **WebGL (Web Browser)** | HTML5 bundle hostable on web servers or GitHub Pages |

---

*Continue to [Chapter 10: Complete RPG Mini-Game Tutorial](Chapter_10_Complete_RPG_Tutorial.md)*
