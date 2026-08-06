# Chapter 10: Complete RPG Mini-Game Tutorial

This tutorial walks through building a complete, playable Mini-RPG project from scratch in **RagNext Studio**, featuring rooms, puzzles, item pickups, keypads, and a full interactive combat screen.

---

## 10.1 Project Overview: "The Dungeon of Shadow"

Our mini-game consists of 3 rooms and 1 interactive combat screen:
1. **Dungeon Cell**: Player wakes up locked inside a cell. Must find a hidden key behind a loose brick.
2. **Armory**: Contains a chest with a broadsword and a digital keypad door lock.
3. **Boss Arena**: Triggers a 2D Interactive Fight Screen (`Attack`, `Defend`, `Heal`) against the Dungeon Guard.

---

## 10.2 Step 1: Setting Up Variables

1. Open **Variables** panel and create:
   - `PlayerHP` (Integer, initial value `100`)
   - `MonsterHP` (Integer, initial value `50`)
   - `Gold` (Integer, initial value `0`)
   - `HasKey` (Boolean, initial value `False`)

---

## 10.3 Step 2: Creating Rooms & Objects

1. Create **Dungeon Cell**:
   - Description: *"Damp stone walls surround you. A heavy iron door to the East is locked."*
   - Add Object: `Loose Brick` (Static).
   - Add Action to `Loose Brick` (*"Inspect Brick"*):
     - Condition: `HasKey == False`
       - True: `Set Variable HasKey = True`, `Give Item 'Brass Key'`, `Print Message ("You found a Brass Key behind the brick!")`.

2. Create **Armory** & Exit:
   - Set Cell `East` exit to `Armory` with lock key `Brass Key`.

---

## 10.4 Step 3: Building the Interactive Combat Screen

1. In **Boss Arena**, open **Interactive Screen** tab.
2. Check **Enable Interactive Mode** and set backdrop to `FightSheet.png`.
3. Create 3 Hotspots:
   - **ATTACK Hotspot**:
     - Click **🎨 Edit Hotspot Action Steps**:
     - `Modify Integer: MonsterHP -= 15`
     - `Play Sound: sword_hit.wav`
     - `Print Message ("You slash the guard for 15 damage!")`
     - `Condition: MonsterHP <= 0`:
       - True: `Print Message ("You defeated the guard!")`, `Close Screen`
   - **HEAL Hotspot**:
     - `Modify Integer: PlayerHP += 20`
     - `Play Sound: heal_spell.wav`
   - **RUN Hotspot**:
     - `Print Message ("You fled back to the Armory!")`
     - `Close Screen`

---

## 10.5 Testing & Conclusion

Click **▶ Playtest** to run through your new dungeon! Congratulations—you have mastered game creation with RagNext!

---

*RagNext Creator Manual — Completed for ragnext.com*
