# Chapter 8: Sound FX, Media & Visual Polish

Visual presentation and sound design transform interactive fiction into immersive experiences. This chapter covers importing media assets, audio playback, ambient sound loops, screen VFX transitions, and theme styling.

---

## 8.1 Importing Media Assets

RagNext supports a wide range of audio and visual file formats:

- **Image Formats**: `.png`, `.jpg`, `.jpeg`, `.webp`
- **Audio Formats**: `.wav`, `.mp3`, `.ogg`

### Asset Management
1. In RagNext Studio, click **Media Assets** in the left navigation.
2. Click **➕ Import Assets** and select your media files.
3. Media assets are copied into your project directory and assigned unique Asset IDs.

---

## 8.2 Audio Playback & Music Channels

RagNextPlayer features a dedicated dual-channel audio system:

1. **Background Music Channel**: Loops ambient music tracks (cross-fading between room transitions).
2. **Sound FX Channel**: Plays one-shot sound effects (sword swings, door clicks, spell casts).

### Action Graph Audio Commands
- **Play Sound**: `PlaySound("sword_slash.wav")`
- **Play Background Music**: `PlayMusic("dungeon_theme.ogg", loop=true)`
- **Stop Music / Fade Out**: Stops audio playback smoothly.

---

## 8.3 Screen Shake & Transition VFX

Add visual impact to combat hits or dramatic moments:

- **Trigger Screen Shake**: Shakes the game viewport for a set duration (e.g., `0.5s`, `intensity=2`).
- **Fade Transitions**: Cross-fades room artwork and backdrop images smoothly.

---

*Continue to [Chapter 9: Building, Testing & Publishing Your Game](Chapter_09_Building_Testing_Publishing.md)*
