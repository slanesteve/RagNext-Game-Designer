# Condition Reference

Below is the complete reference of conditional branch checks supported by the RagNext Game Engine.

## Character

### Character: Attribute Check

*Checks if a character's custom attribute matches an expected value.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Character | `Character` | ComboBox |
| Attribute Name | `String` | Text |
| Expected Value | `String` | Text |

### Character: Gender

*Checks if a character is a specific gender.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Character | `Character` | ComboBox |
| Gender | `String` | ComboBox |

### Character: In Room

*Checks if a character is currently in a specific room.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Character | `Character` | ComboBox |
| Room | `Room` | ComboBox |

## Item

### Item: Attribute Check

*Checks if an item's custom attribute matches an expected value.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Item | `Item` | ComboBox |
| Attribute Name | `String` | Text |
| Expected Value | `String` | Text |

### Item: Held By Character

*Checks if a character has a specific item in their inventory.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Item | `Item` | ComboBox |
| Character | `Character` | ComboBox |

### Item: Held By Player

*Checks if the player has a specific item in their inventory.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Item | `Item` | ComboBox |

### Item: In Object

*Checks if an item is inside a specific container object.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Item | `Item` | ComboBox |
| Container Object | `GameObject` | ComboBox |

### Item: In Room

*Checks if a loose item is placed in a specific room.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Item | `Item` | ComboBox |
| Room | `Room` | ComboBox |

### Item: Not Held By Player

*Checks if the player does not have a specific item.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Item | `Item` | ComboBox |

### Item: Not In Object

*Checks if an item is not inside a specific container object.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Item | `Item` | ComboBox |
| Object | `GameObject` | ComboBox |

## Player

### Player: Attribute Check

*Checks if the player's custom attribute matches an expected value.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Attribute Name | `String` | Text |
| Expected Value | `String` | Text |

### Player: Gender

*Checks if the player is a specific gender.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Gender | `String` | ComboBox |

### Player: In Room

*Checks if the player is currently in a specific room.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Room | `Room` | ComboBox |

### Player: In Same Room As

*Checks if the player is in the same room location as a specific character.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Target Character | `Character` | ComboBox |

## Room

### Room: Attribute Check

*Checks if a room's custom attribute matches an expected value.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Room | `Room` | ComboBox |
| Attribute Name | `String` | Text |
| Expected Value | `String` | Text |

### Room: Is Exit Locked

*Checks if a specific exit direction in a room is locked.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Room | `Room` | ComboBox |
| Direction | `Direction` | ComboBox |

## Timer

### Timer: Is Active

*Checks if a background timer is currently active.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Timer | `Timer` | ComboBox |

## Variable

### Variable: Comparison

*Compares a global variable's value against a static value.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Variable | `Variable` | ComboBox |
| Comparison | `Operator` | ComboBox |
| Value | `String` | Text |

### Variable: Comparison To Variable

*Compares the values of two global variables.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Variable A | `Variable` | ComboBox |
| Comparison | `Operator` | ComboBox |
| Variable B | `Variable` | ComboBox |

### Variable: DateTime Part Comparison

*Compares a single component (minute, second, hour, day, month, year) of a datetime variable against a number.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Variable | `Variable` | ComboBox |
| DateTime Component | `String` | ComboBox |
| Comparison | `Operator` | ComboBox |
| Expected Value | `Number` | Number |

### DateTime: Is Past

*Checks if a datetime variable's value represents a time in the past.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Variable | `Variable` | ComboBox |

### DateTime: Is Future

*Checks if a datetime variable's value represents a time in the future.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Variable | `Variable` | ComboBox |

### DateTime: Compare Two Variables

*Compares two datetime variables against each other.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Variable A | `Variable` | ComboBox |
| Comparison | `Operator` | ComboBox |
| Variable B | `Variable` | ComboBox |

### DateTime: Compare Difference

*Compares the timespan difference between two datetime variables against a duration (e.g. 5 minutes).*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Variable A | `Variable` | ComboBox |
| Variable B | `Variable` | ComboBox |
| Comparison | `Operator` | ComboBox |
| Duration | `String` | Text |

### DateTime: Compare Constant

*Compares a datetime variable against a static timestamp constant.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Variable | `Variable` | ComboBox |
| Comparison | `Operator` | ComboBox |
| Constant Value | `String` | Text |

### DateTime: Is Valid

*Checks if a variable contains a valid, parseable datetime string.*

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Variable | `Variable` | ComboBox |


