# 🎬 RagNext Battle System: 3-Part Video Masterclass & Instructional Demos

Welcome to the official video demonstration series for **RagNext Studio**! 

Watch how to build a **complete, custom turn-based RPG combat engine** from scratch. This 3-part video masterclass demonstrates the power of RagNext Studio—combining **interactive canvas screens, inventory items, skill systems, conditional action loops, state switches, and script editor formula mechanics** into a polished, playable game!

::: tip Follow Along Live
Open your copy of **RagNext Studio** alongside these videos to replicate the fight screen layout, action node logic, and damage formula scripting!
:::

---

## 📽️ Part 1: Interactive Combat Screen & Hotspot Setup

In Part 1, we lay the foundation of the battle system by building an interactive 16:9 fight screen inside RagNext Studio. Learn how to configure custom backdrops, position interactive combat hotspots, and attach visual action sequences to player inputs.

<div style="position: relative; padding-bottom: 56.25%; height: 0; overflow: hidden; max-width: 100%; margin: 1.5rem 0; border-radius: 12px; box-shadow: 0 8px 30px rgba(0,0,0,0.3); background: #111;">
  <iframe src="https://www.youtube.com/embed/2OgSBiUkun0" title="RagNext Battle System Demo - Part 1" frameborder="0" allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share" allowfullscreen style="position: absolute; top: 0; left: 0; width: 100%; height: 100%; border-radius: 12px;"></iframe>
</div>

🔗 **Direct Watch Link:** [Watch Part 1 on YouTube](https://www.youtube.com/watch?v=2OgSBiUkun0)

### 🌟 Key Highlights in Part 1
* **16:9 Canvas Layout:** Setting up percentage-based coordinates for responsive combat screens across desktop and mobile devices.
* **Interactive Hotspot Creation:** Designing custom styled buttons (`ATTACK`, `DEFEND`, `SKILLS`, `ITEM`, `FLEE`).
* **Visual Action Triggers:** Attaching audio effects (`sword_swing.wav`, `hit_impact.wav`), screen shake VFX, and dynamic message popovers to player button presses.
* **State Management:** Tracking `PlayerHP` and `EnemyHP` in real-time.

---

## 📽️ Part 2: Inventory Integration, Skill Systems, Switches & Action Loops

Part 2 takes combat to the next level! We integrate the player's inventory directly into the combat screen, construct a multi-ability skill menu, and implement switches and action loops for automated enemy turn logic and status effects.

<div style="position: relative; padding-bottom: 56.25%; height: 0; overflow: hidden; max-width: 100%; margin: 1.5rem 0; border-radius: 12px; box-shadow: 0 8px 30px rgba(0,0,0,0.3); background: #111;">
  <iframe src="https://www.youtube.com/embed/Wrs_zJwySUI" title="RagNext Battle System Demo - Part 2" frameborder="0" allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share" allowfullscreen style="position: absolute; top: 0; left: 0; width: 100%; height: 100%; border-radius: 12px;"></iframe>
</div>

🔗 **Direct Watch Link:** [Watch Part 2 on YouTube](https://www.youtube.com/watch?v=Wrs_zJwySUI)

### 🌟 Key Highlights in Part 2
* **Combat Inventory Menu:** Opening nested interactive item popovers to consume potions (`Health Potion`, `Mana Elixir`) during battle with automatic stack reduction.
* **Skill Cooldown & Mana Switches:** Using boolean and integer switches to enforce skill cooldowns (e.g. *Fireball*, *Shield Bash*, *Ultimate Strike*) and mana costs.
* **Enemy Action Loops:** Building automatic turn resolution loops where the enemy AI randomly evaluates attacks, buffs, or special abilities after the player finishes their turn.
* **Status Effect Triggers:** Handling recurring damage-over-time (Poison/Burn) using action node loops.

---

## 📽️ Part 3: Script Editor Formulas, Dynamic Damage & Battle Resolution

In the epic conclusion, we dive into RagNext Studio's **Script Editor**. See how math expressions, damage formulas, and dynamic variables power realistic damage variance, critical hits, loot distribution, and smooth screen stack transitions upon victory or defeat.

<div style="position: relative; padding-bottom: 56.25%; height: 0; overflow: hidden; max-width: 100%; margin: 1.5rem 0; border-radius: 12px; box-shadow: 0 8px 30px rgba(0,0,0,0.3); background: #111;">
  <iframe src="https://www.youtube.com/embed/PQrLkGkZYVA" title="RagNext Battle System Demo - Part 3" frameborder="0" allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share" allowfullscreen style="position: absolute; top: 0; left: 0; width: 100%; height: 100%; border-radius: 12px;"></iframe>
</div>

🔗 **Direct Watch Link:** [Watch Part 3 on YouTube](https://www.youtube.com/watch?v=PQrLkGkZYVA)

### 🌟 Key Highlights in Part 3
* **Script Editor Math Expressions:** Writing custom math formulas for dynamic damage variance and stat scaling.
* **Critical Hit Logic & Modifiers:** Rolling random percentage checks for damage multipliers and visual flash effects.
* **Victory & Defeat Stack Resolution:** Auto-granting XP and Gold, playing victory fanfare, updating quest variables, and seamlessly closing the interactive combat screen back to the world map.

---

## ⚡ Master Feature Breakdown Table

| Feature Showcase | Featured In | RagNext Studio Tools Used |
| :--- | :--- | :--- |
| **Interactive Fight Screen** | Part 1 | Interactive Canvas, Hotspots, Backdrop Layers |
| **Player Action Buttons** | Part 1 | Hotspot Styles (`TextButton`, `ImageButton`), Sound Triggers |
| **Inventory Combat Items** | Part 2 | Item Entities, Container Slots, Sub-Screens |
| **Skill Cooldowns & Mana** | Part 2 | Variables, Switches, Conditional Logic Nodes |
| **Enemy AI Action Loop** | Part 2 | Action Graph Loops, Turn Tick Triggers |
| **Formula Script Editor** | Part 3 | Script Editor, Dynamic Variables, Math Expressions |
| **Loot & Victory Screen** | Part 3 | Variable Modifiers, Audio Playback, Screen Stack Navigation |

---

::: info Learn More
Want to build your own battle engine step by step? Check out the text tutorial in [Chapter 10: Complete RPG Tutorial](/guide/chapter-10-complete-rpg-tutorial)!
:::
