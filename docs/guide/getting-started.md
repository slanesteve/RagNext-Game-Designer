# Getting Started with RagNext

Welcome to the **RagNext Game Designer**! This guide will introduce you to the core components of the editor and help you create your very first narrative action step script.

## Core Concepts

The RagNext editor organizes your adventure game using five principal structures:

1. **Rooms**: The physical locations within your game (e.g., *Small Bedroom*, *Transit Station*).
2. **Characters**: Non-player characters (NPCs) that live in your rooms (e.g., *Professor Vance*, *Keith*).
3. **Objects/Items**: Objects placed in rooms or held in player/NPC inventories (e.g., *Worn T-Shirt*, *Keycard*).
4. **Variables**: Global variables that store state, numbers, flags, or time offsets (e.g., `HeroAge`, `SampleTime`).
5. **Timers**: Background ticking entities that trigger action events at specific turn intervals.

![RagNext Editor Database & Properties](/properties-panel.png)

---

## Creating Action Scripts

Actions define the behavior and gameplay logic of your entities. An Action is built using a **Visual Action Graph** containing:

* **Start Node**: Defines the trigger event (e.g. *User Clicked*, *Room Entered*, *Turn Tick*).
* **Command Nodes**: Actions that modify the state, play audio, move entities, or display text.
* **Condition Nodes**: Branches that check properties (e.g. gender, attributes, variables) and execute a `True` or `False` branch.

![Visual Action Graph Canvas Editor](/action-graph.png)

---

## Dynamic Date & Time System

RagNext features a robust, type-aware datetime system. 

### Initializing Datetimes
When you create a Global Variable with the `Date & Time` type hint, it will automatically validate your input format. If left empty, it will default to `DateTime.Now`.

### Offsets & Operations
You can increment or decrement datetime variables using the **Increment** (`var.inc`) or **Decrement** (`var.dec`) nodes. Specify the offset value using human-readable units:
* `10 seconds` (or `10s`)
* `5 minutes` (or `5m`)
* `2 hours` (or `2h`)
* `1 day` (or `1d`)

### Autocomplete & Placeholders
In description boxes and text nodes, you can output formatted date/time strings using double curly braces:
* `{variables.MyTime}` $\rightarrow$ Defaults to a player-friendly format (e.g., `October 31, 2026 8:00 AM`).
* `{variables.MyTime:date}` $\rightarrow$ Outputs only the date (`October 31, 2026`).
* `{variables.MyTime:time}` $\rightarrow$ Outputs only the time (`8:00 AM`).
* `{variables.MyTime:datetime}` $\rightarrow$ Outputs the raw ISO-8601 string.

