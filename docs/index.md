# 📖 RagNext Creator Manual & Documentation Portal

Welcome to the official **RagNext & RagNext Studio Manual**, hosted at [ragnext.com](https://ragnext.com). This comprehensive guide covers everything from your first room to complex RPG battle engines, visual graph actions, interactive screens, and multiplatform publishing.

---

## 📚 Table of Contents

### [Chapter 1: Getting Started & RagNext Architecture](Chapter_01_Getting_Started.md)
- What is RagNext & RagNext Studio?
- System Requirements & Installation
- Understanding the Studio Workspace Shell
- Navigation Rail, Entity Tree, Property Inspector & Focus Canvas Mode (`⤢`)
- RagNext Architecture & Runtime Engine Overview

### [Chapter 2: World Building: Rooms, Exits & Navigation](Chapter_02_World_Building_Rooms_Exits.md)
- Creating & Organizing Rooms
- Primary Exits & Compass Navigation (N, S, E, W, Up, Down, In, Out)
- Conditional Exits & Lock Mechanics
- Room Backgrounds & Media Assets
- Room Enter / Exit Action Triggers

### [Chapter 3: Game Objects, Characters & Inventories](Chapter_03_Objects_Characters_Inventories.md)
- Static, Grabable, Wearable, and Container Objects
- Inventory Management & Player Equipment Slots
- Creating NPCs & Characters
- Contextual Interaction Popovers & Object Verbs

### [Chapter 4: Game Variables, Expressions & Dynamic Text](Chapter_04_Variables_Expressions_Dynamic_Text.md)
- Variable Types: String, Integer, Boolean
- Initializing & Modifying Variables
- Math & String Manipulations
- Dynamic Text Templating Tags (`{variables.PlayerHP}`, `{player.Name}`)
- Conditional Evaluation & Expressions

### [Chapter 5: Visual Action Graph & Event Engine](Chapter_05_Visual_Action_Graph.md)
- Overview of the Node Graph Editor
- Action Triggers: `UserClicked`, `OnPlayerEnter`, `OnTurnTick`, `OnGameStart`
- Commands vs. Conditions (Branching True/False Logic)
- Reusable Action Templates & Global Functions

### [Chapter 6: Interactive Screens & Hotspots](Chapter_06_Interactive_Screens_Hotspots.md)
- Interactive Mode Setup on Rooms & Game Objects
- 16:9 Canvas Aspect Ratio & Percentage Coordinates
- Hotspot Styling: Invisible Hitboxes, Text Buttons, Image Buttons
- Self-Contained Inline Action Steps (`🎨 Edit Hotspot Action Steps`)
- Nested Interactive Screens & Automatic Stack Navigation (`Close` / `Return`)

### [Chapter 7: Advanced Game Mechanics: Timers & Global Functions](Chapter_07_Advanced_Mechanics_Timers_Functions.md)
- Turn Ticks vs. Real-Time Interval Timers
- Creating Shared Global Functions (`CurrentGame.Functions`)
- Game Save & Load Hooks (`OnGameStart`, `OnGameLoad`)
- State Persistence & Variable Isolation

### [Chapter 8: Sound FX, Media & Visual Polish](Chapter_08_Sound_Media_Visual_Polish.md)
- Audio Asset Import & Playback (`Play Sound`, `Play Music`, Volume Controls)
- Transition VFX & Screen Shakes
- Dark Theme Customization & Micro-Animations

### [Chapter 9: Building, Testing & Publishing Your Game](Chapter_09_Building_Testing_Publishing.md)
- Live Playtesting in Studio
- Exporting Game Data Bundles (`.json` & assets)
- Multiplatform Deployment (Windows, macOS, Mobile, Web)

### [Chapter 10: Complete RPG Mini-Game Tutorial](Chapter_10_Complete_RPG_Tutorial.md)
- Step-by-Step walkthrough of building a complete Mini-RPG from scratch
- Dungeons, Keypads, Chests, and a Full Combat Screen Engine

---

*RagNext Manual Version 2.0 — Published for ragnext.com via GitHub Pages.*
