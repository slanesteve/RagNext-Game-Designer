# Chapter 5: Visual Action Graph & Event Engine

The Visual Action Graph is the logic engine of RagNext. It allows game creators to design visual node graphs for event handling, puzzle conditions, audio playback, room transitions, and state changes without code.

---

## 5.1 Node Graph Overview

When editing an Action or Hotspot in RagNext Studio, clicking **🎨 Visual Graph Editor** opens the full-screen canvas:

```
+-------------------------------------------------------------------------------+
| Visual Action Graph: "Search Bookshelf"                                       |
+-------------------------------------------------------------------------------+
| [ Trigger: UserClicked ]                                                      |
|           |                                                                   |
|           v                                                                   |
| [ Condition: Variable 'FoundKey' == False ]                                   |
|       /                      \                                                |
|   (True)                   (False)                                            |
|     |                         |                                               |
|     v                         v                                               |
| [ Set Variable: FoundKey=True ] [ Print Message: "Just dusty books." ]        |
| [ Give Item: BrassKey ]                                                       |
| [ Play Sound: secret_latch.wav ]                                              |
+-------------------------------------------------------------------------------+
```

---

## 5.2 Action Triggers

Actions execute when their designated trigger fires:

| Action Trigger | Firing Mechanism |
| :--- | :--- |
| `UserClicked` | Player clicks narrative verb text choice or hotspot |
| `OnGameStart` | Executes once when a new game session starts |
| `OnGameLoad` | Executes immediately after loading a saved game session |
| `OnTurnTick` | Fires on every player turn tick |
| `OnPlayerEnter` | Fires when player enters the room containing the action |
| `OnPlayerExit` | Fires when player leaves the room containing the action |
| `OnObjectTaken` | Fires when player picks up a specific item |

---

## 5.3 Commands vs. Conditions

The node graph contains two fundamental node types:

### 1. Commands (Actions / Mutators)
Nodes that execute state changes sequentially:
- **Set Variable / Modify Variable**: Update numbers or strings.
- **Play Sound / Music**: Trigger SFX or audio tracks.
- **Give Item / Take Item**: Transfer objects into player inventory.
- **Move Player**: Transport player to a new room.
- **Show Dialog / Print Message**: Display story text or NPC dialogue.
- **Show Interactive Screen / Close Screen**: Open visual GUI overlays or return.

### 2. Conditions (Branching / Evaluators)
Nodes that evaluate logic and split execution into **True Branch** and **False Branch**:
- **Variable Comparison**: `PlayerHP > 0`
- **Player Has Item**: `Player.Inventory contains IronKey`
- **Player In Room**: `Player.CurrentRoom == Courtyard`
- **Character In Room**: `WizardGlendor.Room == Courtyard`

---

## 5.4 Global Functions & Reusable Action Templates

If an action sequence needs to be called from multiple places (e.g. *Calculate Damage*, *Update Quest Tracker*, *Game Over Reset*), create it under **Global Functions** (`CurrentGame.Functions`).

Any action node or hotspot can invoke a Global Function using the **Execute Function** node step!

---

*Continue to [Chapter 6: Interactive Screens & Hotspots](Chapter_06_Interactive_Screens_Hotspots.md)*
