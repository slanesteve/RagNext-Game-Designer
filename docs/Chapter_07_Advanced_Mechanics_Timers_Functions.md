# Chapter 7: Advanced Game Mechanics: Timers & Global Functions

Advanced games require background systems such as real-time clocks, turn counters, hunger meters, ambient event triggers, and reusable global functions. This chapter details how to configure Timers, Turn Ticks, and Global Functions.

---

## 7.1 Real-Time Timers vs. Turn Ticks

RagNext provides two distinct timing mechanisms:

| Timing System | Trigger Condition | Example Use Case |
| :--- | :--- | :--- |
| **Turn Ticks (`OnTurnTick`)** | Fires every time the player performs an action or moves | Poison damage every turn, hunger meter decrement |
| **Real-Time Timers (`Timer`)** | Fires on a fixed real-time interval (e.g. every 3.0 seconds) | Day/Night clock cycles, continuous monster attacks |

---

## 7.2 Creating Real-Time Timers

### Adding a Timer
1. In the left sidebar, click **Timers**.
2. Click **➕ Add Timer**.
3. Set **Name** (e.g. `TorchBurnTimer`) and **Interval Seconds** (e.g. `10.0`).
4. Click **🎨 Visual Graph Editor** to define what happens when the timer fires:
   - `TorchLight -= 1`
   - If `TorchLight == 0`: `Print Message ("Your torch flickers out! You are in dark darkness.")`

---

## 7.3 Global Functions (`CurrentGame.Functions`)

A **Global Function** is a non-visual background action list stored at the root project level.

### Why Use Global Functions?
- **DRY (Don't Repeat Yourself)**: Avoid duplicating complex damage calculations or inventory refresh logic in multiple rooms.
- **On Close Hooks**: Assign a Global Function to an Interactive Screen's `On Close Action` to handle cleanup.
- **Save/Load Hooks**: Run initialization logic on `OnGameStart` or `OnGameLoad`.

---

*Continue to [Chapter 8: Sound FX, Media & Visual Polish](Chapter_08_Sound_Media_Visual_Polish.md)*
