# Command Reference

Below is the complete reference of script commands supported by the RagNext Game Engine.

## Action

### Action: Add Custom Choice

*Adds a dynamic choice button to a player prompt and binds it to a target variable.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Prompt Name | `PromptName` | ComboBox |
| Choice Text | `String` | Text |
| Target Variable | `Variable` | ComboBox |

### Action: Clear Custom Choice

*Clears all custom choices from a player prompt.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Prompt Name | `PromptName` | ComboBox |

## Character

### Character: Display Description

*Appends the character's description text to the story feed.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Character | `Character` | ComboBox |

### Character: Move To Room

*Moves a character to a target room.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Character | `Character` | ComboBox |
| Destination Room | `Room` | ComboBox |

### Character: Move Inventory To Player

*Transfers all items from a character's inventory to the player.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Character | `Character` | ComboBox |

### Character: Move To Object

*Moves a character inside a container object.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Character | `Character` | ComboBox |
| Object | `GameObject` | ComboBox |

### Character: Set Portrait Media

*Changes the active portrait image of a character.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Character | `Character` | ComboBox |
| Media | `Media` | ComboBox |

### Character: Set Action To Active/Inactive

*Activates or deactivates a specific character script action.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Action Name | `String` | Text |
| Active | `Bool` | Checkbox |

### Character: Set Attribute

*Sets a custom string attribute value on a character.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Character | `Character` | ComboBox |
| Attribute Name | `String` | Text |
| Value | `String` | Text |

### Character: Set Description

*Changes the story description text of a character.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Description | `String` | TextArea |

### Character: Set Gender

*Sets the gender of a character.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Gender | `String` | ComboBox |

### Character: Set Display Name

*Sets the visible display name of a character.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Name | `String` | Text |

## General

### General: Call Function

*Invokes a global scripting function by ID.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Function | `Function` | ComboBox |

### Add A Comment

*Inserts a designer note or comment inside the action graph (ignored at runtime).*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Comment Text | `String` | TextArea |

### Debug Text

*Outputs a test message to the developer console log.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Message | `String` | TextArea |

### Display Text

*Appends standard text narrative to the gameplay story feed.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Text | `String` | TextArea |

### Prompt Player Input

*Displays an input popup dialog to prompt user text/option entry.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Prompt Name | `String` | Text |
| Prompt Text | `String` | TextArea |
| Input Type | `String` | ComboBox |
| Custom Options | `String` | Text |
| Store Variable | `Variable` | ComboBox |

## Media

### Media: Display Layered Picture

*Draws a layered composite picture on the multimedia panel.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Picture File | `Media` | ComboBox |

### Media: Display Multimedia

*Draws an image or plays audio on the gameplay screen.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Media File | `Media` | ComboBox |

### Media: Set Background Music

*Plays a music asset on loop as the active background track.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Music File | `Media` | ComboBox |

### Media: Stop Background Music

*Fades out and stops the active background music.*

No parameters.

### Media: Play Sound Effect

*Plays a one-shot sound effect at a designated volume.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Sound File | `Media` | ComboBox |
| Volume | `Number` | Number |

## Item

### Item: Display Description

*Appends an item's description text to the story feed.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Item | `Item` | ComboBox |

### Item: Move To Character

*Moves an item to a character's inventory.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Character | `Character` | ComboBox |

### Item: Move To Inventory

*Moves an item to the player's inventory.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Item | `Item` | ComboBox |

### Item: Move Inside Object

*Places an item inside a container object.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Item | `Item` | ComboBox |
| Container Object | `GameObject` | ComboBox |

### Item: Move To Room

*Places an item in a specific room location.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Item | `Item` | ComboBox |
| Room | `Room` | ComboBox |

### Item: Set Attribute

*Sets a custom string attribute value on an item.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Item | `Item` | ComboBox |
| Attribute Name | `String` | Text |
| Value | `String` | Text |

### Item: Open Container

*Opens an item container so its contents can be accessed.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Container Object | `Item` | ComboBox |

### Item: Close Container

*Closes an item container.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Container Object | `Item` | ComboBox |

## Player

### Player: Display Description

*Appends the player's description text to the story feed.*

No parameters.

### Player: Move Inventory To Character

*Transfers the player's entire inventory to a character.*

No parameters.

### Player: Move Inventory To Room

*Drops the player's entire inventory into the current room.*

No parameters.

### Player: Move To Room

*Teleports the player to a target room.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Destination Room | `Room` | ComboBox |

### Player: Move To Character

*Teleports the player to the room where a specific character is located.*

No parameters.

### Player: Move To Object

*Teleports the player to the room where a specific object is located.*

No parameters.

### Player: Set Attribute

*Sets a custom string attribute value on the player.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Attribute Name | `String` | Text |
| Value | `String` | Text |

### Player: Set Description

*Changes the story description text of the player.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Description | `String` | TextArea |

### Player: Set Name

*Changes the player's character name.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Name | `String` | Text |

### Player: Set Gender

*Sets the player's gender.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Gender | `String` | ComboBox |

### Player: Set Portrait Media

*Changes the active portrait image of the player.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Media File | `Media` | ComboBox |

## Room

### Room: Display Description

*Appends the current room's description text to the story feed.*

No parameters.

### Room: Display Picture

*Displays the current room's assigned background image.*

No parameters.

### Room: Move Items To Player

*Moves all loose items in the current room to the player's inventory.*

No parameters.

### Room: Set Description

*Changes the story description text of a room.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Description | `String` | TextArea |

### Room: Set Picture

*Changes the background picture asset of a room.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Picture | `Media` | ComboBox |

### Room: Lock Exit

*Locks a room exit direction, preventing traversal.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Room | `Room` | ComboBox |
| Direction | `Direction` | ComboBox |

### Room: Unlock Exit

*Unlocks a room exit direction, allowing traversal.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Room | `Room` | ComboBox |
| Direction | `Direction` | ComboBox |

## UI

### StatusBar: Set Visible/Invisible

*Shows or hides the engine's gameplay status bar.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Visible | `Bool` | Checkbox |

## Timer

### Timer: Execute Timer

*Forces a background timer to trigger immediately.*

No parameters.

### Timer: Reset Timer

*Resets a timer's tick count back to zero.*

No parameters.

### Timer: Set Attribute

*Sets a custom string attribute value on a timer.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Timer | `Timer` | ComboBox |
| Attribute Name | `String` | Text |
| Value | `String` | Text |

### Timer: Set Timer To Active/Inactive

*Starts or stops a background interval timer.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Active | `Bool` | Checkbox |

## Variable

### Variable: Display Data

*Outputs the formatted value of a variable to the story feed.*

No parameters.

### Variable: Set

*Assigns a value to a global variable (supports numbers, booleans, and date/times).*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Variable | `Variable` | ComboBox |
| Value | `String` | Text |

### Variable: Increment

*Adds a numeric value or time duration offset to a variable.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Variable | `Variable` | ComboBox |
| Value | `String` | Text |

### Variable: Decrement

*Subtracts a numeric value or time duration offset from a variable.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Variable | `Variable` | ComboBox |
| Value | `String` | Text |

### Variable: Set Numeric Randomly

*Assigns a random integer within a range to a variable.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Minimum | `Number` | Number |
| Maximum | `Number` | Number |

## System

### End The Game

*Ends gameplay and displays a final summary message to the player.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Final Message | `String` | TextArea |


