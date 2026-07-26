# Implementation Plan - Interactive Holographic Game Map

Create a modular, high-performance interactive 3D holographic map overlay inside the game player. The map will render room layouts dynamically using SVGs, apply isometric tilting and glow effects using CSS, and support dragging and zooming.

---

## Open Questions

Before we begin coding tomorrow, it would be helpful to clarify:
1. **Room Data Structure**: How is room data currently stored in your engine? (e.g., does each room have `exits` with coordinate offsets like `x, y, z` or direction names like `north`, `south`, `up`?)
2. **Framework / Integration**: Is your game player built in vanilla HTML/JS, or is it utilizing a framework? Knowing this helps determine where to structure the modular script.

---

## Proposed Changes

We will build the map as a self-contained module so it doesn't clutter your existing player core logic.

### Map Component

#### [NEW] [map.css](file:///c:/Users/steve/source/repos/EpubToMp3/map.css)
* Contain classes for the overlay modal container (blur backdrop, close buttons).
* Define the isometric rotation and stacked floor styles:
  ```css
  .floor-layer {
      transform: rotateX(60deg) rotateZ(-45deg);
      transform-style: preserve-3d;
  }
  ```
* Define neon lighting effects, theme-variable support, and the keyframe pulse animations.

#### [NEW] [map.js](file:///c:/Users/steve/source/repos/EpubToMp3/map.js)
* **Graph Generator**: A class that traverses visited rooms starting from the player's current location, building a local adjacency list.
* **SVG Constructor**: Dynamically creates SVG circles (nodes), connecting paths (lines), and icons (padlocks for locked doors, stairs for verticality).
* **Interactivity Engine**: Captures mouse/touch inputs for dragging (panning) and wheel scrolling (zooming) using CSS transforms.
* **Control UI**: Handles rendering the close button and instructions.

#### [MODIFY] [player.js](file:///c:/Users/steve/source/repos/EpubToMp3/player.js) (or equivalent engine controller)
* Expose a global `ShowMap()` action.
* Register a hotkey (like `M`) or interface button to open the map overlay.
* Bind the map modal state so that it pauses general input/story progression until dismissed.

---

## Verification Plan

### Manual Verification
1. Load a multi-floor test game with locked doors.
2. Open the map using the hotkey/command and verify it centers on the current room with a pulsing glow.
3. Test vertical movement: climb up/down and confirm the map view slides to focus on the active floor.
4. Drag and zoom the map to confirm smooth interaction.
5. Verify path colors match the theme (e.g., locked doors show lock icons).
