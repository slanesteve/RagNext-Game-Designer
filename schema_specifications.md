# RagNext Schema & Generation Specifications

When generating or updating JSON project save files for the RagNext Designer, you must strictly adhere to the following serialization contracts, data types, and structural hierarchies to ensure a successful load via the `System.Text.Json` pipeline.

---

## 1. Graph Preservation Hierarchy (`$id` & `$ref`)
The backend model utilizes `ReferenceHandler.Preserve` configuration rules.
- **Declaration Order:** Every unique structural entity (Objects, Rooms, Characters, Variables) must be fully declared with its complete metadata block and a unique metadata `"$id"` string property (e.g. `"1"`, `"2"`, etc.) the *very first time* the serializer runs across it.
- **Array Packaging:** Collections are serialized as metadata wrapper objects containing `$id` and a flat array under the `"$values"` key:
  ```json
  "rooms": {
    "$id": "33",
    "$values": [ ... ]
  }
  ```
- **Global References:** The global objects dictionary array `"objects": { "$id": "51", "$values": [...] }` must reside at the very top of the JSON payload. This ensures all individual items populate the global reference registry buffer before they are referred to via `"$ref"` down inside player inventories or room arrays.
- **Referencing Syntax:** Subsequent appearances of an item must only use a cross-reference link envelope: `{"$ref": "ID_NUMBER"}` (e.g. `{"$ref": "5"}`).

---

## 2. Rigid ID Data Typing (GUID Compliance)
- **Entities:** Every Room, Object, Character, and Variable instance must use a systematically formatted 128-bit Global Unique Identifier (`System.Guid`) string for its primary `"id"` property.
- **Format:** Lowercase v4 string layout (e.g., `"f58fea91-f7b9-48e2-a30c-b2b96bdae85a"`). Human-readable tracking names (like `"room_living_room"`) will trigger immediate parsing exceptions.

---

## 3. Spatial Context & Inventory Arrays
- **Room Object Mapping:** The `"objectIds"` property within a Room object instance is an `ObservableCollection<Guid>`. It must be formatted as a flat metadata array wrapper populated exclusively by pure GUID literal strings:
  ```json
  "objectIds": {
    "$id": "44",
    "$values": [
      "a5722547-8535-4700-bb47-5ca69fb0abcc"
    ]
  }
  ```
- **Inventory/Attributes collections**: Must also be formatted as objects with `$id` and `$values` arrays of the target models.

---

## 4. Polymorphic Action Steps (`$type` & Serialization Contracts)

Actions reside on Player, Room, GameObject, or Character nodes inside `"actions"`. An Action contains `"nodes"`, which is an `ObservableCollection<ActionStep>`.
Polymorphic types must strictly define the `"$type"` parameter (acting as the type discriminator) along with their corresponding properties:

### Conditions (ActionStep Kind = 0)
Conditions contain `trueBranch` and `falseBranch` array wrappers (`{"$id": "X", "$values": [...]}`).

| Type Name | `$type` | Properties | Description |
|---|---|---|---|
| **Variable: Equals String** | `var.equals` | `"name"`: string (Var Name), `"value"`: string (Comparison value), `"caseInsensitive"`: bool | Check if variable equals string value |
| **Player in room** | `player.inRoom` | `"roomId"`: string (GUID of target room) | Check if player is in room |
| **Room has object** | `room.hasObject` | `"roomId"`: string, `"objectId"`: string | Check if room contains object |
| **Player in same room as** | `player.sameRoom` | `"characterId"`: string | Check if player is in room with NPC |
| **Item held by player** | `item.heldByPlayer` | `"objectId"`: string | Check if item is in player inventory |
| **Item not held by player** | `item.notHeldByPlayer`| `"objectId"`: string | Check if item is absent from player |
| **Variable: Compare Numeric** | `var.compare` | `"name"`: string, `"comparisonType"`: string (`"=="`, `"<"`, `">"`, `"<="`, `">="`), `"value"`: double | Compare variable to double number |
| **Character Gender** | `char.gender` | `"characterId"`: string, `"gender"`: string | Check NPC gender |
| **Character in room** | `char.inRoom` | `"characterId"`: string, `"roomId"`: string | Check if NPC is in room |
| **Item in room** | `item.inRoom` | `"objectId"`: string, `"roomId"`: string | Check if item is in room |
| **Player Gender** | `player.gender` | `"gender"`: string | Check protagonist gender |
| **Item held by character** | `item.heldByChar` | `"objectId"`: string, `"characterId"`: string | Check NPC inventory |
| **Item in container** | `item.inObject` | `"objectId"`: string (contained item), `"containerId"`: string (parent chest/bag) | Check if item is in container |
| **Item not in container** | `item.notInObject` | `"objectId"`: string, `"containerId"`: string | Check if item is absent from container |
| **Variable Compare to Var** | `var.compareVar` | `"name1"`: string, `"comparisonType"`: string, `"name2"`: string | Compare variable values |

### Commands (ActionStep Kind = 1)
Commands perform mutating operations when executed.

| Type Name | `$type` | Properties | Description |
|---|---|---|---|
| **Variable: Set** | `var.set` | `"name"`: string, `"value"`: string | Set variable value |
| **Move player to room** | `player.moveTo` | `"roomId"`: string (GUID or variable replacement) | Move player to room (starts transitions) |
| **Add object to room** | `room.addObject` | `"roomId"`: string, `"objectId"`: string | Add object to room's inventory |
| **Remove object from room**| `room.removeObject`| `"roomId"`: string, `"objectId"`: string | Remove object from room's inventory |
| **Display Text** | `general.displayText`| `"text"`: string | Print standard narrative text to logs |
| **Add A Comment** | `general.addComment` | `"commentText"`: string | Developer comment |
| **Media: Play Sound Effect** | `media.playSound` | `"soundId"`: string (media GUID), `"volume"`: double (100.0) | Play sound effect |
| **Player: Set Name** | `player.setName` | `"name"`: string | Set protagonist name |
| **Player: Set Description** | `player.setDescription`| `"description"`: string | Set protagonist description |
| **Player: Set Gender** | `player.setGender` | `"gender"`: string | Set protagonist gender |
| **Variable: Set Randomly** | `var.setRandom` | `"name"`: string, `"minimum"`: double, `"maximum"`: double | Set variable to random range double |
| **Character Move To Room** | `char.moveToRoom` | `"characterId"`: string, `"roomId"`: string | Move NPC to room |
| **Media: Display Multimedia**| `media.displayMultimedia`| `"mediaId"`: string (media GUID) | Set room/scene visual artwork |
| **Character Display Portrait**| `char.displayPortrait`| `"characterId"`: string, `"mediaId"`: string | Display NPC portrait visual |
| **Character Set Portrait Media`| `char.setPortraitMedia`| `"characterId"`: string, `"mediaId"`: string | Update NPC starting portrait |
| **Player Set Portrait Media**| `player.setPortraitMedia`| `"mediaId"`: string | Set protagonist starting portrait |
| **Variable: Increment** | `var.inc` | `"name"`: string, `"value"`: double | Add value to double variable |
| **Variable: Decrement** | `var.dec` | `"name"`: string, `"value"`: double | Subtract value from double variable |
| **Variable: Set to Variable** | `var.setToVar` | `"targetName"`: string, `"sourceName"`: string | Assign variable values |
| **Room: Set Exit** | `room.setExit` | `"roomId"`: string, `"direction"`: string, `"targetRoomId"`: string | Enable/set exit link |
| **Room: Disable Exit** | `room.disableExit` | `"roomId"`: string, `"direction"`: string | Remove exit link direction |
| **End The Game** | `general.endGame` | `"finalMessage"`: string | End game and show restart screen |
| **Prompt Player Input** | `general.promptInput` | `"promptText"`: string, `"inputType"`: string (`"Text"`, `"Object"`, `"Character"`, `"Custom"`), `"customOptions"`: string (comma-delimited), `"storeVariable"`: string | Prompt user input popup |
| **Item: Open Container** | `general.openContainer`| `"objectId"`: string (GUID of container object) | Set container status to open |
| **Item: Close Container** | `general.closeContainer`| `"objectId"`: string (GUID of container object) | Set container status to closed |

---

## 5. Container Settings Specifications
Any object declared in the global `"objects"` list can be structured as a container using the following parameters:
- `"isContainer"`: bool (Set `true` to declare a bag/chest container).
- `"containerOpen"`: bool (Set `true` if starting open).
- `"containedObjectIds"`: Metadata collection holding GUIDs of contained items. Contained items should NOT be placed in the room's `"objectIds"`, as they are parsed and loaded dynamically beneath parent containers at runtime.
  ```json
  "isContainer": true,
  "containerOpen": false,
  "containedObjectIds": {
    "$id": "60",
    "$values": [
      "a1a2a3a4-b1b2-c1c2-d1d2-e1e2e3e4e5e6"
    ]
  }
  ```

---

## 6. Concrete Serialization Example
```json
{
  "$id": "1",
  "id": "0d3432a9-aee8-4966-b60b-519ad4b6700f",
  "title": "A Test Adventure",
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
    "bPromptForName": false,
    "gender": "Male",
    "startingRoom": null,
    "inventory": {
      "$id": "4",
      "$values": [
        {
          "$ref": "5"
        }
      ]
    },
    "attributes": {
      "$id": "6",
      "$values": [
        {
          "$id": "7",
          "name": "Health",
          "value": "100"
        }
      ]
    },
    "actions": {
      "$id": "8",
      "$values": []
    }
  },
  "mediaAssets": {
    "$id": "9",
    "$values": []
  },
  "rooms": {
    "$id": "10",
    "$values": [
      {
        "$id": "11",
        "id": "2d82da22-e68f-402e-b118-89ad609d85e4",
        "name": "Living Room",
        "description": "A cozy, warm room.",
        "objectIds": {
          "$id": "12",
          "$values": []
        },
        "exits": {
          "$id": "13",
          "North": "81fea9f1-f7b9-48e2-a30c-b2b96bdae85e"
        },
        "attributes": {
          "$id": "14",
          "$values": []
        },
        "actions": {
          "$id": "15",
          "$values": []
        }
      },
      {
        "$id": "16",
        "id": "81fea9f1-f7b9-48e2-a30c-b2b96bdae85e",
        "name": "Dark Cellar",
        "description": "An icy cellar.",
        "objectIds": {
          "$id": "17",
          "$values": []
        },
        "exits": {
          "$id": "18",
          "South": "2d82da22-e68f-402e-b118-89ad609d85e4"
        },
        "attributes": {
          "$id": "19",
          "$values": []
        },
        "actions": {
          "$id": "20",
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
        "attributes": {
          "$id": "22",
          "$values": []
        },
        "media": {
          "$id": "23",
          "$values": []
        },
        "actions": {
          "$id": "24",
          "$values": []
        },
        "id": "a5722547-8535-4700-bb47-5ca69fb0abcc",
        "name": "A Ring",
        "description": "A thin gold band.",
        "isCollectible": true,
        "properties": {
          "$id": "25"
        }
      }
    ]
  },
  "characters": {
    "$id": "26",
    "$values": []
  },
  "variables": {
    "$id": "27",
    "$values": []
  },
  "createdAt": "2026-05-25T12:00:00Z"
}
```
