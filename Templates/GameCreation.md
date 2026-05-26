# RagNext Schema & Generation Specifications

When generating or updating JSON project save files for the RagNext Designer, you must strictly adhere to the following serialization contracts, data types, and structural hierarchies to ensure a successful load via the `System.Text.Json` pipeline.

## 1. Graph Preservation Hierarchy ($id & $ref)
The backend model utilizes `ReferenceHandler.Preserve` configuration rules.
- **Declaration Order:** Every unique structural entity (Objects, Rooms, Characters, Variables) must be fully declared with its complete metadata block and a unique metadata `"$id"` string property the *very first time* the serializer runs across it.
- **Collection Placement:** The global objects dictionary array `"objects": { "$id": "2", "$values": [...] }` must reside at the very top of the JSON payload. This ensures all individual items populate the global reference registry buffer before they are referred to via `"$ref"` down inside player inventories or room arrays.
- **Referencing Syntax:** Subsequent appearances of an item must only use a cross-reference link envelope: `{"$ref": "ID_NUMBER"}`.

## 2. Rigid ID Data Typing (GUID Compliance)
- **Entities:** Every Room, Object, Character, and Variable instance must use a systematically formatted 128-bit Global Unique Identifier (`System.Guid`) string for its primary `"id"` property.
- **Format:** Lowcase v4 string layout (e.g., `"f58fea91-f7b9-48e2-a30c-b2b96bdae85a"`). Human-readable tracking names (like `"room_living_room"`) will trigger immediate parsing exceptions.

## 3. Spatial Context & Inventory Arrays
- **Room Object Mapping:** The `"objectIds"` property within a Room object instance is an `ObservableCollection<Guid>`. It must be formatted as a flat metadata array wrapper populated exclusively by pure GUID literal strings:
  ```json
  "objectIds": {
    "$id": "41",
    "$values": [
      "f58fea91-f7b9-48e2-a30c-b2b96bdae85a"
    ]
  }