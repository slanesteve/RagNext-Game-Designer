# Condition Reference

Below is the complete reference of conditional branch checks supported by the RagNext Game Engine.

## Character

### Character: Attribute Check

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Character | `Character` | ComboBox |
| Attribute Name | `String` | Text |
| Expected Value | `String` | Text |

### Character: Gender

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Character | `Character` | ComboBox |
| Gender | `String` | ComboBox |

### Character: In Room

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Character | `Character` | ComboBox |
| Room | `Room` | ComboBox |

## Item

### Item: Attribute Check

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Item | `Item` | ComboBox |
| Attribute Name | `String` | Text |
| Expected Value | `String` | Text |

### Item: Held By Character

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Item | `Item` | ComboBox |
| Character | `Character` | ComboBox |

### Item: Held By Player

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Item | `Item` | ComboBox |

### Item: In Object

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Item | `Item` | ComboBox |
| Container Object | `GameObject` | ComboBox |

### Item: In Room

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Item | `Item` | ComboBox |
| Room | `Room` | ComboBox |

### Item: Not Held By Player

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Item | `Item` | ComboBox |

### Item: Not In Object

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Item | `Item` | ComboBox |
| Object | `GameObject` | ComboBox |

## Player

### Player: Attribute Check

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Attribute Name | `String` | Text |
| Expected Value | `String` | Text |

### Player: Gender

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Gender | `String` | ComboBox |

### Player: In Room

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Room | `Room` | ComboBox |

### Player: In Same Room As

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Target Character | `Character` | ComboBox |

## Room

### Room: Attribute Check

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Room | `Room` | ComboBox |
| Attribute Name | `String` | Text |
| Expected Value | `String` | Text |

### Room: Is Exit Locked

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Room | `Room` | ComboBox |
| Direction | `Direction` | ComboBox |

## Timer

### Timer: Is Active

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Timer | `Timer` | ComboBox |

## Variable

### Variable: Comparison

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Variable | `Variable` | ComboBox |
| Comparison | `Operator` | ComboBox |
| Value | `String` | Text |

### Variable: Comparison To Variable

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Variable A | `Variable` | ComboBox |
| Comparison | `Operator` | ComboBox |
| Variable B | `Variable` | ComboBox |

### Variable: DateTime Part Comparison

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Variable | `Variable` | ComboBox |
| DateTime Component | `String` | ComboBox |
| Comparison | `Operator` | ComboBox |
| Expected Value | `Number` | Number |

### DateTime: Is Past

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Variable | `Variable` | ComboBox |

### DateTime: Is Future

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Variable | `Variable` | ComboBox |

### DateTime: Compare Two Variables

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Variable A | `Variable` | ComboBox |
| Comparison | `Operator` | ComboBox |
| Variable B | `Variable` | ComboBox |

### DateTime: Compare Difference

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Variable A | `Variable` | ComboBox |
| Variable B | `Variable` | ComboBox |
| Comparison | `Operator` | ComboBox |
| Duration | `String` | Text |

### DateTime: Compare Constant

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Variable | `Variable` | ComboBox |
| Comparison | `Operator` | ComboBox |
| Constant Value | `String` | Text |

### DateTime: Is Valid

| Parameter | Type | UI Input |
| :--- | :--- | :--- |
| Variable | `Variable` | ComboBox |


