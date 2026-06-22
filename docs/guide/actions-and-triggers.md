# Actions and Triggers

Actions are visual script graphs that drive the narrative logic, state mutations, and player choices in your game. 

---

## Action Structure

Every Action consists of an **Action Start** node connected to a series of command or condition nodes:

### 1. Dialogue Node
Renders text prompts or story narratives to the player. It can display static text, or evaluate dynamic inline variables wrapped in curly braces (e.g., `Hello, {player.Name}!`).

### 2. Command Node
Performs state mutations and game actions. Key commands include:
* **Variable: Set / Increment / Decrement:** Mutate numeric, string, or boolean variables.
* **Character: Move To Room:** Updates NPC locations.
* **Room: Lock / Unlock Exit:** Toggle access to direction exits dynamically (e.g., locking "North" until a key is found).
* **Media: Play Sound Effect / Video:** Play audio clips or videos.

### 3. Condition Node
Branches the visual flow based on boolean statements.
* Evaluates variables, gender, inventory holdings, or room locations.
* Sockets: Has a green **True** branch socket and a red **False** branch socket to route execution flow.

### 4. Switch Node
Evaluates a target expression and matches it against multiple customizable cases (multi-way branching).

---

## Trigger Events

An Action's execution is initiated by a **Trigger Event**. You define this on the **Action Start** node:

| Trigger Event | Description |
| :--- | :--- |
| **OnGameStart** | Runs once when a new game session begins (e.g., character select prompts). |
| **OnPlayerEnter** | Runs immediately when the player enters the host room. |
| **OnPlayerExit** | Runs when the player leaves the host room. |
| **UserClicked** | Appears as an interactive action button (e.g., "Talk To", "Examine", "Open Door") in the player UI when the host room or character is active. |

---

## AI-Assisted Scripting

Inside the Visual Script Editor, you can use the **✨ AI Assistant** button.
1. Click the button and type a natural language prompt (e.g., *"If player has key, unlock the north exit of Milestone 42, else display 'It's locked'"*).
2. The AI compiles the instructions and automatically connects the corresponding Condition, Display Text, and Unlock Exit nodes on your canvas.
