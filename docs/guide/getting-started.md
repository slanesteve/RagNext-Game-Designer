# Getting Started with RagNext

Welcome to the **RagNext Game Designer** documentation! RagNext is a modern, node-based storytelling and simulation engine designed to build highly interactive text-adventure and simulation RPG games.

RagNext consists of two main components:
1. **RagNext Studio (Designer):** A cross-platform visual scripting desktop tool built on Avalonia UI.
2. **RagNext Player (Runtime):** A high-performance simulation runner integrated with Unity to compile and render your interactive worlds on Desktop and Web platforms.

---

## Workspace Tour

When you launch RagNext Studio, the designer interface is divided into three key areas:

### 1. The Left Navigation Sidebar
Quick access to all design aspects of your game:
* **Rooms:** Define the physical spaces, descriptions, exits, and room-specific actions.
* **Characters:** Create NPCs, define their attributes, and manage dialog triggers.
* **Game Objects:** Manage items, quest objects, and containers.
* **Game Variables:** Maintain game state (scores, flags, timers, lists).
* **Action Library:** Create global templates or actions.
* **Publisher:** Prepare and build standalone executables.

### 2. The Central Workspace / Canvas
The primary area where you design layouts, edit room properties, and write visual scripts. The **Visual Script Editor** uses a modern, zoomable node grid where you connect flow sockets (execution paths) and evaluate logic.

### 3. The Live Status Bar
Located at the top right of the application header, the **Save Status Indicator** keeps track of file state in real-time. RagNext implements **Pure Autosave**, meaning any modification you make on characters, variables, or rooms is instantly committed to your active project JSON file. 

* 🟢 **Saved:** The project is synchronized with your disk.
* 🔄 **Saving changes...:** A background disk write is currently executing.

---

## Creating Your First Game

1. Open RagNext Studio and select **📂 Launcher** or **📂 Load Existing Save** to create or import a project workspace.
2. Under **Rooms**, click **➕ Add Room**. Name your starting room (e.g., `Highway 9 - Milestone 42`).
3. Set your player's start location by choosing the room name in the `Starting Room` dropdown.
4. Set up exits and navigation routes, then navigate to the **Publisher** page to build your first standalone application.
