# Chapter 10: Complete RPG Mini-Game Tutorial

In this final chapter, we will build a complete, playable Mini-RPG game from start to finish in **RagNext Studio**.

This hands-on project ties together everything you have learned: creating rooms, connecting exits, placing items and containers, writing variables, building action graph puzzles, creating an interactive combat screen with hotspot buttons, and publishing your standalone game!

---

## 10.1 Tutorial Overview: "The Dungeon of Shadow"

Our game features 3 rooms and 1 interactive combat screen:

```mermaid
flowchart LR
    A["Room 1: Dungeon Cell<br/>(Find Brass Key behind loose brick)"] -->|East Exit (Locked)| B["Room 2: Armory<br/>(Find Sword in chest)"]
    B -->|North Exit| C["Room 3: Boss Arena<br/>(Triggers Interactive Fight Screen)"]
    C --> D["Interactive Combat Screen<br/>(ATTACK, HEAL, FLEE Hotspots)"]
```

---

## 10.2 Step 1: Create a New Project & Variables

1. Launch **RagNext Studio** and click **➕ New Game**.
2. Title: `The Dungeon of Shadow`, Author: `Your Name`, Version: `1.0.0`.
3. Go to **⚙️ Variables & Timers** and create four variables:
   - `PlayerHP` (Integer, Initial Value = `100`)
   - `MonsterHP` (Integer, Initial Value = `50`)
   - `Gold` (Integer, Initial Value = `0`)
   - `FoundKey` (Boolean, Initial Value = `False`)

---

## 10.3 Step 2: Build Room 1 — Dungeon Cell

1. Select the default starting room in **📁 Rooms** and name it `Dungeon Cell`.
2. Type description:
   > *"Damp stone walls surround you in the dark cell. Water drips from the ceiling. A heavy iron door leads East."*
3. Go to **🎒 Objects** and click **➕ Add Object**:
   - Name: `Loose Brick` (Uncheck *Is Collectible* — Static Scenery).
   - Place in `Dungeon Cell`.
4. Add Action to `Loose Brick` (*"Inspect Brick"*):
   - Open **🎨 Visual Graph Editor**.
   - Add Condition: `FoundKey == False`
     - **True Branch**:
       - `Set Variable: FoundKey = True`
       - `Give Item: Brass Key`
       - `Play Sound: stone_slide.wav`
       - `Print Message: "You pull out the loose brick and discover a Brass Key!"`
     - **False Branch**:
       - `Print Message: "The brick cavity is empty."`

---

## 10.4 Step 3: Build Room 2 — Armory & Door Lock

1. Click **➕ Add Room** and name it `Armory`.
2. Description:
   > *"Weapon racks line the stone walls. An old iron chest rests against the corner. A archway leads North."*
3. Connect `Dungeon Cell` East exit to `Armory` (Check **Two-Way Exit**).
4. Lock the East exit: Check **Locked** and select locking key item `Brass Key`.
5. Create Object `Iron Chest` in `Armory` (Check **Is Container**).
   - Put Object `Broadsword` (`IsWearable = True`, Slot = `MainHand`) inside `Iron Chest`.

---

## 10.5 Step 4: Build Room 3 — Boss Arena & Interactive Combat Screen

1. Click **➕ Add Room** and name it `Boss Arena`. Connect `Armory` North to `Boss Arena`.
2. Open **Interactive Screen** tab on `Boss Arena`:
   - Check **Enable Interactive Mode**.
   - Set Backdrop to `combat_arena.jpg`.

### Adding Combat Hotspots
1. Click **➕ Add Hotspot** $\rightarrow$ Name: `ATTACK` (`TextButton`, Label = `⚔️ ATTACK`).
   - Click **`🎨 Edit Hotspot Action Steps`**:
     - `Modify Integer: MonsterHP -= 15`
     - `Play Sound: sword_hit.wav`
     - `Print Message: "You attack the Dungeon Guard for 15 damage!"`
     - Condition: `MonsterHP <= 0`:
       - **True**: `Modify Integer: Gold += 100`, `Print Message: "Victory! You defeated the guard and claimed 100 Gold!"`, `Close Screen`.
2. Click **➕ Add Hotspot** $\rightarrow$ Name: `HEAL` (`TextButton`, Label = `🧪 HEAL`).
   - Action steps: `Modify Integer: PlayerHP += 20`, `Play Sound: heal.wav`, `Print Message: "You drink a potion and recover 20 HP!"`.
3. Click **➕ Add Hotspot** $\rightarrow$ Name: `FLEE` (`TextButton`, Label = `🏃 FLEE`).
   - Action steps: `Print Message: "You flee back to the Armory!"`, `Close Screen`.

---

## 10.6 Step 5: Publish Your Standalone Game!

1. All your edits are automatically saved in real-time by Studio!
2. Click **🚀 Publish** in the Top Toolbar.
3. Choose your target platform (Windows, macOS, Linux, or WebGL), check **Create Compressed .ZIP File**, and click **Publish Game Now**.

🎉 **Congratulations!** You have built and published a complete, interactive RPG game with RagNext!
