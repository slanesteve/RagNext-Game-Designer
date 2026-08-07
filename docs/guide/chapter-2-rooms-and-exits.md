# Chapter 2: World Building — Rooms, Exits & Environmental Atmosphere

Rooms are the foundational building blocks of your game world. Whether you are building an expansive castle, a spooky mansion, a futuristic space station, or a small cozy study, every location the player visits is a **Room**.

In this chapter, you will learn how to create rooms, craft evocative descriptions, link rooms together with compass exits, set up locked doors, assign background artwork, and add atmospheric ambient weather effects.

---

## 2.1 Understanding Rooms in RagNext

In RagNext, a Room is more than just a text description. A Room acts as a container for:

1. **Room Identity & Description**: The title and story text shown to players when they arrive.
2. **Background Artwork & Audio**: Visual artwork images and ambient soundscapes.
3. **Compass Exits**: Links to neighboring rooms (North, South, East, West, etc.).
4. **Room Objects**: Items, furniture, and props resting in the room.
5. **Room Verbs & Actions**: Interactive story choices available to the player (*"Examine Bookcase"*, *"Pull Lever"*).
6. **Interactive Screen Overlays**: Optional visual GUI panels with clickable hotspots.

```
+-------------------------------------------------------------------------------+
| ROOM: Grand Library                                                           |
+-------------------------------------------------------------------------------+
| [ Artwork: library_bg.jpg ]                                                   |
|                                                                               |
| Towering oak bookshelves line the walls, filled with ancient leather-bound    |
| tomes. Sunlight streams through a stained-glass window to the East.           |
|                                                                               |
| Objects in Room: [ Ancient Scroll ]  [ Brass Desk Lamp ]                      |
| Compass Exits:   [ East: Sunroom ]   [ South: Entrance Hall ]                 |
| Actions Available: [ Inspect Stained-Glass Window ]  [ Search Bookshelves ]   |
+-------------------------------------------------------------------------------+
```

---

## 2.2 Step-by-Step: Creating Your First Room

Let's walk through creating a room in RagNext Studio:

### Step 1: Open the Rooms Workspace
1. In the top view rail or left sidebar, click **📁 Rooms**.
2. The Left Sidebar will display your list of existing rooms.

### Step 2: Add a New Room
1. Click the **➕ Add Room** button at the bottom of the room list.
2. A new room entry appears in the list (e.g. `New Room`).

### Step 3: Configure Room Properties
Select the room in the list to reveal its properties in the main workspace:

- **Room Name**: Type a memorable title (e.g. `Overgrown Courtyard`).
- **Description**: Type your story description in the text box.
  > *Pro Tip*: Use sensory details—describe the sights, sounds, smells, and atmosphere to draw your player into the story!

---

## 2.3 Linking Rooms with Compass Exits

To let players travel between rooms, you link them using compass directions: **North (N)**, **South (S)**, **East (E)**, **West (W)**, **Northeast (NE)**, **Northwest (NW)**, **Southeast (SE)**, **Southwest (SW)**, **Up**, **Down**, **In**, and **Out**.

```
                   [ North ]
        [ Northwest ]  |  [ Northeast ]
                       |
[ West ] --------------+-------------- [ East ]
                       |
        [ Southwest ]  |  [ Southeast ]
                   [ South ]

         [ Up ] / [ Down ]  |  [ In ] / [ Out ]
```

### Step-by-Step: Connecting Room A to Room B
1. Select **Room A** (e.g. `Courtyard`).
2. Scroll down to the **Exits** section in the Room Inspector.
3. Find the direction you want to link (e.g. **North**).
4. Click the dropdown menu and select **Room B** (e.g. `Great Hall`).
5. Check **Two-Way Exit** (Recommended).
   > *What Two-Way Exit does*: Checking this automatically sets Room B's **South** exit back to Room A, saving you from having to link the return path manually!

---

## 2.4 Creating Locked Exits & Door Locks

Not all doors should open immediately! Creating locked exits is a great way to build puzzles and guide player progression.

### Locking an Exit
1. In the room's **Exits** inspector, find the exit direction you wish to lock (e.g. **North**).
2. Check the **Locked** checkbox next to that exit.
3. Below the checkbox, select a **Locking Key Item** (e.g. `IronKey`), or leave it unassigned if the exit will be unlocked by a puzzle action script.

### How Door Locks Behave for Players
- When a player attempts to go North through a locked exit without the key, RagNextPlayer displays a locked message:
  > *"The heavy iron door to the North is locked tight."*
- If the player picks up the `IronKey` in their inventory and attempts the exit again, the door unlocks smoothly!

---

## 2.5 Room Background Artwork & Media Assets

Visual artwork elevates your text story into a graphic adventure.

### Adding Background Artwork to a Room
1. Make sure you have imported your artwork into **Media Assets** (see Chapter 8).
2. Select your room in Studio.
3. In the **Background Picture** dropdown, select your image asset (e.g. `courtyard_art.png`).
4. RagNextPlayer will automatically render the artwork image above the room description during gameplay!

---

## 2.6 Adding Atmospheric Weather & Screen Effects

RagNext Studio includes built-in atmospheric overlay particle effects and screen shake settings:

### Atmospheric Overlays (`AmbientOverlay`)
Select an ambient weather or atmospheric overlay from the room inspector:
- **Rain**: Adds falling rain particle animations over the room image.
- **Snow**: Adds gentle falling snow particle effects.
- **Fog / Mist**: Adds drifting fog layers for creepy dungeons or graveyards.
- **Ember Particles**: Adds floating fire embers for volcano or tavern fireplace scenes.
- **Particle Intensity**: Drag the slider (`0.1` to `3.0`) to control how heavy the weather effect appears!

### Screen Shake Effects (`ScreenEffect`)
You can set a subtle environmental shake effect:
- **Subtle Rumble**: Great for crumbling ruins or engine rooms.
- **Heavy Earthquake**: Used for collapsing caves or boss encounters.

---

## Chapter 2 Hands-On Exercise

Create a 3-room mini-map in Studio:

1. **Room 1**: `Overgrown Courtyard` (Rain weather overlay enabled).
2. **Room 2**: `Entrance Hall` (Connected North from Courtyard).
3. **Room 3**: `Locked Treasury` (Connected East from Entrance Hall, locked with `GoldKey`).
4. Link `Courtyard` North to `Entrance Hall` as a two-way exit.
