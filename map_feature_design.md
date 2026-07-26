# Game Map Feature Design Proposal

This document outlines the conceptual design and professional standards for implementing a "Show Map" popup command in the game engine.

---

## 1. Depicting Verticality (Up/Down) & Depth (In/Out)

Vertical movement is one of the hardest aspects to represent on a flat display. 

### Layered Isometric Stack (3D Style)
* **The Concept**: Render floors as floating, semi-transparent isometric layers stacked vertically.
* **Connections**: Draw spiral staircases or dotted vertical lines between levels to show how they connect.
* **Navigation**: When the player climbs up or down, the active view smoothly slides vertically to center on the new floor layer, fading out the inactive floors.

### 2D Schematic with Indicators
If keeping the map 2D:
* **Elevations**: Overlay a small up/down arrow or stair icon on nodes that have vertical exits.
* **In/Out transitions**: Draw nested sub-nodes. For example, entering a house in a city room displays a smaller node nested inside the city node rather than creating a new level.

---

## 2. Depicting Locked and Special Exits

Map lines should convey traversal rules immediately.

* **Locked Exits**: 
  * Draw a dashed red connection line instead of a solid line.
  * Overlay a tiny padlock symbol `🔒` in the center of the line.
  * If the player holds the key in their inventory, the lock icon changes to green or displays an unlocked state `🔓`.
* **One-Way Exits**: Use directional arrowheads (e.g., `A ---> B`) indicating they cannot walk backward.
* **Hidden Exits**: Show a dotted question-mark path that only appears after the exit is discovered by the player.

---

## 3. Theme-Compliant Styling (CSS Variables)

To ensure the map looks built-in regardless of the player's active theme:

* **SVG Vector Art**: Generate the map dynamically using inline SVG elements in the DOM.
* **Style Inheritance**: Use CSS variables for SVG fill, stroke, and typography:
  ```css
  .map-node {
      fill: var(--bg-secondary);
      stroke: var(--accent-color);
      font-family: var(--font-primary);
  }
  .map-active-node {
      fill: var(--accent-color);
      filter: drop-shadow(0 0 8px var(--accent-color));
  }
  ```
* This ensures that switching to high-contrast, light, or dark modes updates the map aesthetics instantly.

---

## 4. Map Scale, Zoom, and Range

* **Fog of War**: Do not reveal unvisited rooms unless the player has purchased/found an in-game physical map item.
* **Local View**: By default, center the active room and show a radius of 3–4 rooms in every direction.
* **Zooming**: In expansive city hubs, support smooth zooming (e.g., using `transform: scale()`) and dragging (panning). High zoom levels display simple node circles, while zooming in reveals room names and descriptions.

---

## 5. Visual Mockup

![3D Holographic Map Mockup](/c/Users/steve/.gemini/antigravity/brain/c2279af5-6198-4948-975b-57b217a07ad4/holographic_game_map_1785027640873.jpg)
