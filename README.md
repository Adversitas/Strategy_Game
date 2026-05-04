# Hexagonal Strategy Game Base

A Unity-based strategy game engine featuring a hexagonal grid system, autonomous unit logic, and a responsive UI.

## Features
- **Hexagonal Grid**: Axial coordinate system with procedural hex sprite generation.
- **Autonomous Construction**: Citizens can be tasked to build facilities and roads with pathfinding and queuing support.
- **Resource System**: Random resource distribution across the map (Food, Wood, Stone, Gold).
- **Responsive UI**: Adaptive menus with high-resolution text rendering for better legibility.
- **Unit Management**: Support for different unit types (Citizens, Couriers, Scouts) with stamina-based movement.

## Setup
1. Open the project in Unity 2021.3+.
2. Ensure the `Resources/Extra` folder contains the necessary sprites for resources.
3. Press Play to see the grid generation in action.

## Controls
- **Left Click**: Select Tile/Unit.
- **Right Click (Empty Tile)**: Open Build Menu.
- **Shift + Left Click (Build Mode)**: Queue multiple construction orders.
- **WASD / Mouse Edge**: Move Camera.
