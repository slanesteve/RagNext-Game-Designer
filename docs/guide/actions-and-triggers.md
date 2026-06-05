# Visual Action Scripts & Triggers

In RagNext, interactive gameplay, branching dialogues, and game-state mutations are powered by **Action Scripts**. 

Instead of writing complex code, you design these scripts visually using the node-based canvas editor directly integrated into the RagNext Workspace.

---

## 🎨 The Node Editor Canvas

Each action script starts with an event and branches outward through conditional checks and command executions:

* **Trigger (Start Node)**: The entry point that listens for specific in-game events.
* **Condition Nodes**: Purple decision boxes that evaluate variables, items, or attributes. They direct the execution path along `True` or `False` outputs.
* **Command Nodes**: Blue execution blocks that carry out operations like displaying narration, changing portraits, playing sound effects, moving entities, or ending the game.

![Visual Action Graph Canvas Editor](/action-graph.png)

---

## ⚡ Supported Action Triggers

Every script is bound to a specific trigger. When that event occurs in the engine runtime, the corresponding script fires automatically.

Here is the complete reference of triggers available in the designer:

| Trigger Event | Target Context | Description |
| :--- | :--- | :--- |
| **UserClicked** | Manual Choice | Fires when the player explicitly selects a custom button or choices prompt. |
| **OnGameStart** | Engine Initialization | Triggers once when a new adventure session starts. |
| **OnGameLoad** | Session Load | Triggers immediately when a saved game session is loaded. |
| **OnTurnTick** | Global Turn | Fires on every turn tick globally across the entire game. |
| **OnPlayerEnter** | Room Movement | Fires when the player character walks into a room. |
| **OnPlayerExit** | Room Movement | Fires just before the player character leaves a room. |
| **OnCharacterEnter** | NPC Movement | Fires when any non-player character enters a room. |
| **OnCharacterExit** | NPC Movement | Fires when any non-player character exits a room. |
| **OnRoomTick** | Room Turn | Fires on every turn tick, but only if the player is currently inside that specific room. |
| **OnInteract** | Entity Interaction | Fires when the player interacts directly with a room, item, or character. |
| **OnCharacterTick** | NPC Turn | Fires on every turn tick for a specific character (useful for NPC behaviors). |
| **OnCharacterKilled**| NPC Lifecycle | Fires when an NPC's status changes to defeated or killed. |
| **OnObjectExamined** | Inventory/Object | Fires when a player inspects a container object or item. |
| **OnObjectTaken** | Inventory Transaction | Fires when the player picks up an item and adds it to their inventory. |
| **OnObjectDropped** | Inventory Transaction | Fires when the player drops an item from their inventory into a room. |
