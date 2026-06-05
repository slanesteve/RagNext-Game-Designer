# Command Reference

Below is the complete reference of script commands supported by the RagNext Game Engine.

## Action

### Action: Add Custom Choice

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Prompt Name | `PromptName` | ComboBox |
| Choice Text | `String` | Text |
| Target Variable | `Variable` | ComboBox |

### Action: Clear Custom Choice

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Prompt Name | `PromptName` | ComboBox |

## Character

### Character: Display Description

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Character | `Character` | ComboBox |

### Character: Move To Room

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Character | `Character` | ComboBox |
| Destination Room | `Room` | ComboBox |

### Character: Move Inventory To Player

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Character | `Character` | ComboBox |

### Character: Move To Object

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Character | `Character` | ComboBox |
| Object | `GameObject` | ComboBox |

### Character: Set Portrait Media

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Character | `Character` | ComboBox |
| Media | `Media` | ComboBox |

### Character: Set Action To Active/Inactive

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Action Name | `String` | Text |
| Active | `Bool` | Checkbox |

### Character: Set Attribute

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Character | `Character` | ComboBox |
| Attribute Name | `String` | Text |
| Value | `String` | Text |

### Character: Set Description

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Description | `String` | TextArea |

### Character: Set Gender

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Gender | `String` | ComboBox |

### Character: Set Display Name

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Name | `String` | Text |

## General

### General: Call Function

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Function | `Function` | ComboBox |

### Add A Comment

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Comment Text | `String` | TextArea |

### Debug Text

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Message | `String` | TextArea |

### Display Text

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Text | `String` | TextArea |

### Prompt Player Input

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Prompt Name | `String` | Text |
| Prompt Text | `String` | TextArea |
| Input Type | `String` | ComboBox |
| Custom Options | `String` | Text |
| Store Variable | `Variable` | ComboBox |

## Media

### Media: Display Layered Picture

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Picture File | `Media` | ComboBox |

### Media: Display Multimedia

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Media File | `Media` | ComboBox |

### Media: Set Background Music

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Music File | `Media` | ComboBox |

### Media: Stop Background Music

No parameters.

### Media: Play Sound Effect

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Sound File | `Media` | ComboBox |
| Volume | `Number` | Number |

## Item

### Item: Display Description

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Item | `Item` | ComboBox |

### Item: Move To Character

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Character | `Character` | ComboBox |

### Item: Move To Inventory

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Item | `Item` | ComboBox |

### Item: Move Inside Object

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Item | `Item` | ComboBox |
| Container Object | `GameObject` | ComboBox |

### Item: Move To Room

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Item | `Item` | ComboBox |
| Room | `Room` | ComboBox |

### Item: Set Attribute

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Item | `Item` | ComboBox |
| Attribute Name | `String` | Text |
| Value | `String` | Text |

### Item: Open Container

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Container Object | `Item` | ComboBox |

### Item: Close Container

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Container Object | `Item` | ComboBox |

## Player

### Player: Display Description

No parameters.

### Player: Move Inventory To Character

No parameters.

### Player: Move Inventory To Room

No parameters.

### Player: Move To Room

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Destination Room | `Room` | ComboBox |

### Player: Move To Character

No parameters.

### Player: Move To Object

No parameters.

### Player: Set Attribute

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Attribute Name | `String` | Text |
| Value | `String` | Text |

### Player: Set Description

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Description | `String` | TextArea |

### Player: Set Name

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Name | `String` | Text |

### Player: Set Gender

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Gender | `String` | ComboBox |

### Player: Set Portrait Media

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Media File | `Media` | ComboBox |

## Room

### Room: Display Description

No parameters.

### Room: Display Picture

No parameters.

### Room: Move Items To Player

No parameters.

### Room: Set Description

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Description | `String` | TextArea |

### Room: Set Picture

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Picture | `Media` | ComboBox |

### Room: Lock Exit

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Room | `Room` | ComboBox |
| Direction | `Direction` | ComboBox |

### Room: Unlock Exit

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Room | `Room` | ComboBox |
| Direction | `Direction` | ComboBox |

## UI

### StatusBar: Set Visible/Invisible

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Visible | `Bool` | Checkbox |

## Timer

### Timer: Execute Timer

No parameters.

### Timer: Reset Timer

No parameters.

### Timer: Set Attribute

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Timer | `Timer` | ComboBox |
| Attribute Name | `String` | Text |
| Value | `String` | Text |

### Timer: Set Timer To Active/Inactive

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Active | `Bool` | Checkbox |

## Variable

### Variable: Display Data

No parameters.

### Variable: Set

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Variable | `Variable` | ComboBox |
| Value | `String` | Text |

### Variable: Increment

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Variable | `Variable` | ComboBox |
| Value | `String` | Text |

### Variable: Decrement

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Variable | `Variable` | ComboBox |
| Value | `String` | Text |

### Variable: Set Numeric Randomly

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Minimum | `Number` | Number |
| Maximum | `Number` | Number |

## System

### End The Game

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Final Message | `String` | TextArea |


