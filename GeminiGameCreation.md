# RagNext Schema & Generation Specifications

When generating or updating JSON project save files for the RagNext Designer, you must strictly adhere to the following serialization contracts, data types, and structural hierarchies to ensure a successful load via the native `System.Text.Json` pipeline.

---

## 1. Graph Preservation Hierarchy (`$id` & `$ref`)

The backend model utilizes modern `.NET` polymorphic metadata and native reference handling rules (`ReferenceHandler.Preserve`).

* **Declaration Order:** Every unique structural entity (Objects, Rooms, Characters, Variables) must be fully declared with its complete metadata block and a unique metadata `"$id"` string property (e.g. `"1"`, `"2"`, etc.) the *very first time* the serializer runs across it.
* **Array Packaging:** Collections are serialized as metadata wrapper objects containing `$id` and a flat array under the `"$values"` key:
"rooms": {
"$id": "33",
"$values": [ ... ]
}
* **Global References:** The global objects dictionary array `"objects": { "$id": "21", "$values": [...] }` must reside near the top of the JSON payload. This ensures all individual items populate the global reference registry buffer before they are referred to via `"$ref"` down inside player inventories, character sheets, or room arrays.
* **Referencing Syntax:** Subsequent appearances of an item must only use a cross-reference link envelope pointing back to the declared metadata ID: `{"$ref": "ID_NUMBER"}` (e.g. `{"$ref": "5"}`).

---

## 2. Rigid ID Data Typing (GUID Compliance)

* **Entities:** Every Room, Object, Character, and Variable instance must use a systematically formatted 128-bit Global Unique Identifier (`System.Guid`) string for its primary `"id"` property.
* **Format:** Lowercase v4 string layout (e.g., `"f58fea91-f7b9-48e2-a30c-b2b96bdae85a"`). Human-readable tracking names (like `"room_living_room"`) will trigger immediate parsing exceptions.

---

## 3. Spatial Context & Inventory Arrays

* **Room Object Mapping:** The `"objectIds"` property within a Room object instance is an `ObservableCollection<Guid>`. It must be formatted as a flat metadata array wrapper populated exclusively by pure GUID literal strings:
"objectIds": {
"$id": "12",
"$values": [
"a5722547-8535-4700-bb47-5ca69fb0abcc"
]
}
* **Inventory/Attributes Collections:** Must also be formatted as objects with `$id` and `$values` arrays of the target models to fit the `ReferenceHandler.Preserve` pipeline.

---

## 4. Polymorphic Action Steps (`$type` & Visual Node Layouts)

Actions reside on game model nodes inside a `GameAction` wrapper under `"actions"`. Each action maintains a sequential `ObservableCollection<ActionStep>` titled `"steps"`.

### Base Visual & Metadata Properties

Because actions are visually rendered inside a node-graph canvas interface, **every action step object** contains base canvas layout variables that must be tracked alongside the logic:

* `"label"`: string (Optional custom user-facing node note/rename, can be null).
* `"x"`: double (The horizontal X coordinate position on the visual designer canvas).
* `"y"`: double (The vertical Y coordinate position on the visual designer canvas).
* `"width"`: double (Optional node visual width tracker, can be null).
* `"height"`: double (Optional node visual height tracker, can be null).

### Type Discriminators (`$type`)

Polymorphic types are handled natively via the `.NET` engine using the `TypeDiscriminatorPropertyName = "$type"`. The tables below map out the precise string type discriminators and properties matching the active `RagsCore.Actions` library definitions:

### A. Conditions (ActionStep Kind = 0 / "Condition")

Conditions evaluate state loops and must contain both `"trueBranch"` and `"falseBranch"` collection wrappers (`{"$id": "X", "$values": [...]}`) to hold nested `ActionStep` flows.

| Type Name | `$type` Discriminator | Core Properties | C# Class Mapping |
| --- | --- | --- | --- |
| **Variable: Equals String** | `var.equals` | `"name"`: string, `"value"`: string, `"caseInsensitive"`: bool | `VariableEqualsCondition` |
| **Player in room** | `player.inRoom` | `"roomId"`: string | `PlayerInRoomCondition` |
| **Room has object** | `room.hasObject` | `"roomId"`: string, `"objectId"`: string | `RoomHasObjectCondition` |
| **Player: In Same Room As** | `player.sameRoom` | `"characterId"`: string | `PlayerInSameRoomAsCondition` |
| **Item: Held By Player** | `item.heldByPlayer` | `"itemId"`: string | `ItemHeldByPlayerCondition` |
| **Item: Not Held By Player** | `item.notHeldByPlayer` | `"itemId"`: string | `ItemNotHeldByPlayerCondition` |
| **Variable: Comparison** | `var.compare` | `"name"`: string, `"comparison"`: string (`"="`, `"!="`, `">"`, `">="`, `"<"`, `"<="`), `"value"`: string | `VariableComparisonCondition` |
| **Variable: Comparison To Variable** | `var.compareVar` | `"nameA"`: string, `"comparison"`: string, `"nameB"`: string | `VariableComparisonToVariableCondition` |
| **Character: Gender** | `char.gender` | `"characterId"`: string, `"gender"`: string | `CharacterGenderCondition` |
| **Character: In Room** | `char.inRoom` | `"characterId"`: string, `"roomId"`: string | `CharacterInRoomCondition` |
| **Item: In Room** | `item.inRoom` | `"itemId"`: string, `"roomId"`: string | `ItemInRoomCondition` |
| **Player: Gender** | `player.gender` | `"gender"`: string | `PlayerGenderCondition` |
| **Item: Held By Character** | `item.heldByChar` | `"itemId"`: string, `"characterId"`: string | `ItemHeldByCharacterCondition` |
| **Item: In Object** | `item.inObject` | `"itemId"`: string, `"containerObjectId"`: string | `ItemInObjectCondition` |
| **Item: Not In Object** | `item.notInObject` | `"itemId"`: string, `"objectId"`: string | `ItemNotInObjectCondition` |
| **Room: Exit Is Locked** | `room.isExitLocked` | `"roomId"`: string, `"direction"`: string | `IsRoomExitLockedCondition` |

### B. Commands (ActionStep Kind = 1 / "Command")

Commands perform mutating runtime operations across your game world state.

| Type Name | `$type` Discriminator | Core Properties | C# Class Mapping |
| --- | --- | --- | --- |
| **Variable: Set** | `var.set` | `"name"`: string, `"value"`: string | `SetVariableCommand` |
| **Move player to room** | `player.moveTo` | `"roomId"`: string | `MovePlayerToRoomCommand` |
| **Object: Move to Room** | `room.addObject` | `"roomId"`: string, `"objectId"`: string | `AddObjectToRoomCommand` |
| **Remove object from room** | `room.removeObject` | `"roomId"`: string, `"objectId"`: string | `RemoveObjectFromRoomCommand` |
| **Object: Display Description** | `object.displayDescription` | `"objectId"`: string | `ObjectDisplayDescriptionCommand` |
| **Object: Move to Character** | `object.moveToCharacter` | `"objectId"`: string, `"characterId"`: string | `ObjectMoveToCharacterCommand` |
| **Object: Move to Inventory** | `object.moveToInventory` | `"objectId"`: string | `ObjectMoveToInventoryCommand` |
| **Object: Move Inside Object** | `object.moveInsideObject` | `"objectId"`: string, `"containerObjectId"`: string | `ObjectMoveInsideObjectCommand` |
| **Display Text** | `general.displayText` | `"text"`: string | `DisplayTextCommand` |
| **Add A Comment** | `general.addComment` | `"commentText"`: string | `AddCommentCommand` |
| **Media: Play Sound Effect** | `media.playSound` | `"soundId"`: string, `"volume"`: double, `"loop"`: bool | `PlaySoundEffectCommand` |
| **Media: Stop Sound Effect** | `media.stopSound` | `"soundId"`: string, `"stopAllLooping"`: bool | `StopSoundEffectCommand` |
| **Player: Set Name** | `player.setName` | `"name"`: string | `PlayerSetNameCommand` |
| **Player: Set Description** | `player.setDescription` | `"description"`: string | `PlayerSetDescriptionCommand` |
| **Player: Set Gender** | `player.setGender` | `"gender"`: string | `PlayerSetGenderCommand` |
| **Character: Set Gender** | `char.setGender` | `"characterId"`: string, `"gender"`: string | `CharacterSetGenderCommand` |
| **Variable: Set Numeric Randomly** | `var.setRandom` | `"name"`: string, `"minimum"`: double, `"maximum"`: double | `SetNumericRandomlyCommand` |
| **Variable: Increment** | `var.inc` | `"name"`: string, `"value"`: string | `VariableIncrementCommand` |
| **Variable: Decrement** | `var.dec` | `"name"`: string, `"value"`: string | `VariableDecrementCommand` |
| **Variable: Set to Variable** | `var.setToVar` | `"name"`: string, `"sourceName"`: string | `VariableSetToVariableCommand` |
| **Character: Move To Room** | `char.moveToRoom` | `"characterId"`: string, `"roomId"`: string | `CharacterMoveToRoomCommand` |
| **Media: Display Multimedia** | `media.displayMultimedia` | `"mediaId"`: string | `DisplayMultimediaCommand` |
| **Character: Display Portrait** | `char.displayPortrait` | `"characterId"`: string, `"portraitId"`: string | `CharacterDisplayPortraitCommand` |
| **Character: Set Portrait Media** | `char.setPortraitMedia` | `"characterId"`: string, `"mediaId"`: string | `CharacterSetPortraitMediaCommand` |
| **Player: Set Portrait Media** | `player.setPortraitMedia` | `"mediaId"`: string | `PlayerSetPortraitMediaCommand` |
| **Room: Set Exit** | `room.setExit` | `"roomId"`: string, `"direction"`: string, `"destinationRoomId"`: string | `SetRoomExitCommand` |
| **Room: Disable Exit** | `room.disableExit` | `"roomId"`: string, `"direction"`: string | `DisableRoomExitCommand` |
| **Room: Lock Exit** | `room.lockExit` | `"roomId"`: string, `"direction"`: string | `LockRoomExitCommand` |
| **Room: Unlock Exit** | `room.unlockExit` | `"roomId"`: string, `"direction"`: string | `UnlockRoomExitCommand` |
| **Character: Damage / Heal** | `char.damage` | `"characterId"`: string, `"amount"`: int | `DamageCharacterCommand` |
| **Character: Set State** | `char.setState` | `"characterId"`: string, `"state"`: string | `SetCharacterStateCommand` |
| **Character: Set Attribute** | `char.setAttribute` | `"characterId"`: string, `"attributeName"`: string, `"value"`: string | `SetCharacterAttributeCommand` |
| **Player: Set Attribute** | `player.setAttribute` | `"attributeName"`: string, `"value"`: string | `SetPlayerAttributeCommand` |
| **Timer: Set Attribute** | `timer.setAttribute` | `"timerId"`: string, `"attributeName"`: string, `"value"`: string | `SetTimerAttributeCommand` |
| **Item: Set Attribute** | `item.setAttribute` | `"itemId"`: string, `"attributeName"`: string, `"value"`: string | `SetItemAttributeCommand` |
| **Game: Trigger Turn Tick** | `general.triggerTurnTick` | None | `TriggerTurnTickCommand` |
| **General: End Game** | `general.endGame` | `"finalMessage"`: string | `EndGameCommand` |
| **General: Prompt Player Input** | `general.promptInput` | `"promptName"`: string, `"promptText"`: string, `"inputType"`: string (`"Text"`, `"Objects"`, `"Characters"`, `"Custom"`), `"customOptions"`: string, `"storeVariableName"`: string | `PromptPlayerInputCommand` |
| **General: Open Container** | `general.openContainer` | `"objectId"`: string | `OpenContainerCommand` |
| **General: Close Container** | `general.closeContainer` | `"objectId"`: string | `CloseContainerCommand` |
| **General: Call Function** | `general.callFunction` | `"functionId"`: string | `CallFunctionCommand` |
| **Start Dialogue** | `general.startDialogue` | `"dialogueId"`: string, `"characterLines"`: string, `"choices"`: Collection | `StartDialogueCommand` |
| **Action: Add Custom Choice** | `general.addCustomChoice` | `"promptName"`: string, `"choiceText"`: string, `"variableName"`: string | `AddCustomChoiceCommand` |
| **Action: Clear Custom Choice** | `general.clearCustomChoice` | `"promptName"`: string | `ClearCustomChoiceCommand` |
| **Action: Remove Custom Choice** | `general.removeCustomChoice` | `"promptName"`: string, `"choiceText"`: string | `RemoveCustomChoiceCommand` |

---

## 5. Container Settings Specifications

Container hierarchies utilize specific property tags matched explicitly across the core project loops:

* `"isContainer"`: bool (Enables a bag, chest, or locker type entity).
* `"containerOpen"`: bool (Tracks runtime visibility states).
* At runtime, items shifted inside another container utilize an internal property dictionary marker tracking `"ParentContainerId"` mapped directly to the host container object's GUID string layout.

---

## 6. Concrete Serialization Example

{
"$id": "1",
"id": "0d3432a9-aee8-4966-b60b-519ad4b6700f",
"title": "Echoes of the Horizon (Part 1)",
"author": "Steve",
"version": "1.0.0",
"player": {
"$id": "2",
"genders": {
"$id": "3",
"$values": [ "Male", "Female", "Non-binary", "Other" ]
},
"id": "1a21dc92-3cf8-4e6e-b23f-6294046d61dd",
"name": "Bilbo",
"description": "A brave adventurer.",
"gender": "Male",
"inventory": {
"$id": "4",
"$values": []
},
"attributes": {
"$id": "5",
"$values": []
},
"actions": {
"$id": "6",
"$values": []
}
},
"rooms": {
"$id": "7",
"$values": [
{
"$id": "8",
"id": "2d82da22-e68f-402e-b118-89ad609d85e4",
"name": "Scrap Yard Office",
"description": "A cluttered, fluorescent-lit front office.",
"objectIds": {
"$id": "9",
"$values": [ "a5722547-8535-4700-bb47-5ca69fb0abcc" ]
},
"exits": {
"$id": "10",
"North": "81fea9f1-f7b9-48e2-a30c-b2b96bdae85e"
},
"attributes": {
"$id": "11",
"$values": []
},
"actions": {
"$id": "12",
"$values": []
}
}
]
},
"objects": {
"$id": "21",
"$values": [
{
"$id": "5",
"id": "a5722547-8535-4700-bb47-5ca69fb0abcc",
"name": "Crystalline Power Matrix",
"description": "A glowing crystal cluster housing massive amounts of power.",
"attributes": {
"$id": "22",
"$values": []
},
"properties": {
"$id": "23"
},
"actions": {
"$id": "24",
"$values": [
{
"$id": "25",
"$type": "general.displayText",
"text": "The matrix hums quietly against your hand.",
"label": "Log Discovery",
"x": 120.0,
"y": 240.0,
"width": 150.0,
"height": 80.0
}
]
}
}
]
},
"characters": {
"$id": "26",
"$values": [
{
"$id": "27",
"id": "3c3e39a1-8bf2-411a-8c88-2976b978b7a6",
"name": "Mr. Vance",
"properties": {
"$id": "28",
"Gender": "Male",
"Health": "100"
}
}
]
},
"variables": {
"$id": "29",
"$values": []
}
}