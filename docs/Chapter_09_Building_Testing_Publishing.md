# Chapter 9: Packaging & Publishing Standalone Games

When your game is complete and ready for the world to play, RagNext Studio makes it easy to build standalone, branded game packages for your players.

In this chapter, you will learn how to back up and share project packages (`.ragpkg`), use the standalone Publish Engine (`PublishEngine`), and deploy your game to Windows, macOS, Linux, and WebGL (Web Browsers).

---

## 9.1 Project Packages (`.ragpkg`) vs. Published Standalone Games

Before publishing, it is important to understand the difference between a **Designer Project Package** and a **Published Game**:

```
+-----------------------------------------------------------------------------------+
| 📦 DESIGNER PROJECT PACKAGE (.ragpkg)                                              |
| Used to backup your project or share with co-creators.                            |
| Opens inside RagNext Studio for editing.                                          |
+-----------------------------------------------------------------------------------+
                                        |
                                        | (Publish Engine)
                                        v
+-----------------------------------------------------------------------------------+
| 🚀 PUBLISHED STANDALONE GAME (Windows .exe, Mac .app, Linux, WebGL HTML5)         |
| Ready for players! Contains the standalone player engine, game JSON, and media.   |
| Plays directly without needing RagNext Studio!                                    |
+-----------------------------------------------------------------------------------+
```

---

## 9.2 Step-by-Step: Exporting & Backup Packages (`.ragpkg`)

To back up your project or send it to another creator:

1. In the Top Toolbar of RagNext Studio, click **📦 Export Package**.
2. Select a save folder on your computer and name your file (e.g. `MyAdventure_v1.ragpkg`).
3. Click **Save**. Studio packages all rooms, objects, action graphs, variables, and media assets into a single compressed `.ragpkg` package file.

> [!NOTE]
> **Importing Packages**: If you receive a `.ragpkg` file from another creator, launch Studio and click **📥 Import Package**. Select the file, and Studio will automatically extract the complete game workspace for you.

---

## 9.3 Step-by-Step: Publishing Standalone Games (`PublishEngine`)

When you are ready to release your game to the public on itch.io, Steam, or your website `ragnext.com`:

### Step 1: Open the Publish Window
1. In the Top Toolbar, click the **🚀 Publish** button.
2. The **Standalone Publish Panel** will open.

```
+-------------------------------------------------------------------------------+
| STANDALONE PUBLISH PANEL                                                      |
+-------------------------------------------------------------------------------+
| Game Title:   [ My Epic RPG                      ]                            |
| Author Name:  [ Jane Creator                     ]                            |
| Version:      [ 1.0.0                            ]                            |
|                                                                               |
| Target Platform:                                                              |
| [x] Windows (.exe)    [ ] macOS (.app)    [ ] Linux    [ ] WebGL (Browser)   |
|                                                                               |
| Destination Folder: [ C:/Users/Jane/Documents/RagNext_Published/MyEpicRPG  ]  |
| [x] Create Compressed .ZIP File for Distribution                              |
|                                                                               |
| [ 🚀 PUBLISH GAME NOW ]                                                       |
+-------------------------------------------------------------------------------+
```

### Step 2: Configure Publishing Settings
- **Game Title**: The official name displayed on your game executable window.
- **Author Name**: Your creator or studio name.
- **Version**: Version number (e.g. `1.0.0`).
- **Target Platform**:
  - **Windows**: Produces a standalone Windows folder with executable (`.exe`).
  - **macOS**: Produces a native Apple `.app` bundle.
  - **Linux**: Produces an executable Linux binary.
  - **WebGL (Web Browser)**: Produces an HTML5 web package hostable on `ragnext.com` or itch.io!
- **Destination Folder**: Select where on your computer the published game folder should be generated.
- **Create Compressed .ZIP File**: Check this to automatically generate a `.zip` archive ready for upload!

### Step 3: Publish Game
Click **🚀 Publish Game Now**. The Publish Engine takes the precompiled **RagNext Player** runtime, injects your game bundle and media assets, and generates your standalone game!

---

## Chapter 9 Hands-On Exercise

1. Open your project in RagNext Studio.
2. Export a backup package `Backup_Project.ragpkg` using **📦 Export Package**.
3. Open the **🚀 Publish** panel.
4. Select **Windows** (or your OS) as the target platform and check **Create Compressed .ZIP File**.
5. Click **Publish Game Now**. Open your destination folder and verify your published standalone game!

---

*Continue to [Chapter 10: Complete RPG Mini-Game Tutorial](Chapter_10_Complete_RPG_Tutorial.md)*
