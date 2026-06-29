# Variables and State

State tracking in RagNext is powered by a rich, type-aware Variable system. You define variables globally under the **Variables** sidebar tab.

---

## Supported Variable Types

### 1. Simple Types
* **String:** Text values (e.g., `PlayerName = "Alex"`).
* **Number:** Numeric integers or floating-point decimals. Supported by **Increment** and **Decrement** commands.
* **Bool:** Logical `true` or `false` switches.

### 2. DateTime Operations
RagNext natively supports temporal simulation:
* **DateTime type:** Store standard timestamps (`yyyy-MM-ddTHH:mm:ss`).
* **Addition / Subtraction:** The **Increment** and **Decrement** commands automatically parse relative durations (e.g., incrementing a DateTime variable by `15m`, `1h`, or `3d` shifts the timestamp).
* **Conditions:** Compare components (e.g., check if the current hour is `>= 18` for night cycles) or check if a timestamp is in the past/future relative to system time.

### 3. Array / List Variables
Use grid variables to track database tables, inventories, or complex group states:
* In the Variables editor, click **Add Column** to append attributes to a list (e.g., a `CharacterStatus` array tracking properties like `InitialTalk = true`).
* Iterate over list variables using **For Each Loop** branch conditions in the Visual Script Editor.

---

## Inline Template Resolvers

Variables can be dynamically read and printed inside dialogue, descriptions, or name labels using curly brace notation:

* `{player.Name}`: Resolves the player's name.
* `{player.currentRoomId}`: Returns the active room GUID.
* `{MyScore}`: Prints the value of the global variable `MyScore`.
* `{char.CharacterGuid.displayedPortraitId}`: Returns the active portrait asset GUID.

### 4. Wear Slot Resolvers
If items are configured with wear slots (e.g. `Feet`, `Torso`), you can resolve who is wearing what:
* `{this.WearSlot}`: Returns the slot name assigned to the focus item.
* `{player.wornIn.SlotName}` / `{player.wornSlot.SlotName}`: Returns the **Name** of the item currently worn in that slot (e.g. `{player.wornIn.Feet}` -> `"Kitten Heels"`), or empty if none is worn.
* **Recursive templates**: e.g., `{player.wornIn.{this.WearSlot}}`.

### 5. Loop Scope Variables
Inside the body of a **For Each Loop** block, you can resolve the current row's properties:
* `{loop.Name}`: Returns the name of the current loop item.
* `{loop.attributes.AttributeName}`: Returns the custom attribute value (e.g., `{loop.attributes.Girliness}`). If the item does not have that attribute, it resolves cleanly to an empty string (`""`).
