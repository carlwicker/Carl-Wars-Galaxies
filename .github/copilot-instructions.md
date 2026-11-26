Game Design Document (GDD) — Player-Driven Cube Sandbox MMO
1. Vision & Core Pillars
A player-driven 3D sandbox MMO inspired by Star Wars Galaxies (Pre-NGE), built using primitive cube geometry rather than high-poly 3D models. Every player, vehicle, structure, and droid is constructed from scaled cube grids, keeping rendering lightweight and modular while powering a deep, interconnected economy and skill-based sandbox.

Player-Driven Economy: Virtually every weapon, armor piece, vehicle, droid, consumable, and building is crafted by players from dynamic, shifting resource spawns.   
SWG Infinity

Skill-Box Freedom (No Rigid Classes): Players mix and match skill trees freely up to a 250 Skill Point cap across combat, crafting, entertaining, medical, and exploration disciplines.

Interdependent Social Loops: Combatants rely on Doctors and Entertainers to heal Battle Fatigue and Wounds, Crafters rely on Scouts and Artisans for high-quality resources, and cities rely on Politicians and Merchants to thrive.   
SWG Infinity

Modular Primitive Aesthetic: Entities use clean, dimensioned cube blocks (e.g., 1x1x4 hover bikes, 5x15x5 heavy droids) with physical component slots for core functional cubes (Engine Cubes, Power Cubes).

2. Technical Architecture & Current Prototype Status
Engine & Networking Stack
Engine: Unity 6, Universal Render Pipeline (URP), Unity Input System.

Current Networking (Prototype): Netcode for GameObjects (NGO) + Unity Relay + Anonymous Authentication (NetworkStartHelper.cs), supporting 1–8 players per session with host-local JSON persistence (HostInventoryStore.cs).

Target Networking (MMO Scale): Sharded Dedicated Headless Servers (e.g., 100-player planetary/zone shards) utilizing Area of Interest (AOI) network culling and a central cloud persistence database (PostgreSQL / DynamoDB) to store player skill trees, inventories, structures, and vendor inventories.

Current Prototype Implementation Matrix
System	Script(s) / Asset(s)	Current Functionality	MMO Evolution Path
Player & Camera	
IsometricMovement.cs (Legacy Name)


CameraFollow.cs

3D camera-relative WASD movement, orbiting camera, zoom controls, named characters.	Rename to PlayerMovement.cs; add combat stance toggles, action queuing, and HAM status overlays.
Social & Chat	PlayerChat.cs	Networked text chat, player nameplates, overhead speech bubbles.	Add spatial proximity channels, group/guild chat, and radial context menus (~ / right-click) for trading, teaching skills, and inspecting.
Inventory	PlayerInventory.cs	24-slot grid, max stack 99; pick up, drop, split, combine, and consume ("Use").	Expand capacity (e.g., 80-slot base + craftable cube backpacks); bind "Use" to consumable buffs, stims, and tools.
Vehicles & Droids	
RideableBike.cs → Refactor


BikeInventory.cs → Refactor

Primitive cube vehicles (1x1x4 bike, 1x3x5 car, 5x15x5 big droid) with dedicated Engine Cube and Power Cube slots, cargo slots, and active hover physics when turned ON or ridden.	Link Engine/Power Cube stats to craftable resource quality (speed, hover height, battery drain, cargo capacity).
World & Spawning	
SampleScene.unity


WorldItemSpawner.cs


EngineCubeSpawner.cs

Uneven terrain, primitive structures, collectible resource cubes, glowing toggles, radial menus.	Replace static cube spawners with Dynamic Resource Concentration Maps and player-placed Harvesters.
Persistence	HostInventoryStore.cs	Host-local JSON saving player inventories and world pickup states.	Migrate to server-authoritative database persistence with periodic batched writes.
3. Modular Cube Vehicle & Droid System
(Merging active Unity prototype design with Pre-NGE Vehicle/Droid mechanics)

All vehicles and droids are built from scaled cube primitives (Width x Height x Length) and operate as physical world entities (deeds/callables or parked in-world) rather than abstract mounts.

Archetype Specifications
Archetype	Cube Dimensions (W×H×L)	Dedicated Slots	Cargo Slots	Functional Role
Hover Bike	1 x 1 x 4	
1x Engine Cube


1x Power Cube

4	Fast personal reconnaissance and light courier transport.
Hover Car / Speeder	1 x 3 x 5	
1x Engine Cube


1x Power Cube

12–16	Multi-passenger/utility transport and resource hauling.
Big Droid (Heavy Unit)	5 x 15 x 5	
1x Engine Cube


1x Power Cube

24+	Autonomous or piloted heavy platform; supports combat, mobile crafting, or heavy cargo modules.
Core Operational Rules
Component Requirements: A vehicle or droid cannot be powered ON unless both its Engine Cube Slot and Power Cube Slot contain valid cubes.

Bidirectional Transfer: Players can open the vehicle's radial menu to transfer items—including Engine and Power cubes—between their personal inventory and the vehicle's slots. Removing either core cube immediately shuts down the unit.

Hover State Machine:

Powered ON (Unoccupied): Actively hovers above terrain, idling in place.

Ridden / Piloted (Occupied): Maintains active hover physics while responding to player movement input.

Powered OFF / Unpowered: Disengages hover thrusters and settles onto the terrain mesh.

Crafter Integration (Planned): Artisan/Droid Engineer players craft Engine Cubes and Power Cubes using harvested metals and energy resources. Higher resource stats (e.g., Conductivity, Overall Quality) yield faster top speeds, lower power drain, and higher durability.   
SWG Nexus

4. Core Pre-NGE Gameplay Systems (Roadmap)
A. Character Progression: The 250-Point Skill Web
No Character Levels: Players start in one of 6 Novice professions (Artisan, Brawler, Marksman, Medic, Scout, Entertainer).

XP-to-Skill-Box Advancement: Performing actions grants specific XP types (e.g., Pistol XP, Surveying XP, Medical XP, Dance XP). Once a player accumulates enough XP, they purchase the next box in a 4-branch skill tree.

Hybrid Builds: With 250 Skill Points total, players can master ~2.5 professions (e.g., Master Rifleman + Combat Medic, or Master Weaponsmith + Merchant), creating unique hybrid roles.

B. Combat & The HAM (Health, Action, Mind) System
Instead of a single health bar, characters have three primary attribute pools, each subdivided into three secondary stats:

Health (Strength, Constitution) — Physical damage; healed by Medics/Doctors.

Action (Quickness, Stamina) — Physical special attacks and movement costs.

Mind (Focus, Willpower) — Mental/ranged special attacks; cannot be healed by Medics—requires Entertainers (Musicians/Dancers) in cantinas or camps.

Wounds & Battle Fatigue:

Taking damage inflicts Wounds (black bars) that permanently reduce a pool's maximum cap until treated in a hospital/cantina.

Battle Fatigue accumulates during combat, reducing the effectiveness of all healing until cleared by watching/listening to Entertainers.

Incapacitation vs. Death: Reaching 0 in any HAM pool incapacitates the player. Repeated incapacitations or a "deathblow" sends the player to a Cloning Facility (incurring equipment decay and wounds).

C. Dynamic Resources & Deep Crafting
Shifting Resource Spawns: Every planet generates temporary resource deposits (Metals, Chemicals, Flora, Energy, Gas) with randomized attributes (0–1000 scale): Overall Quality (OQ), Conductivity (CD), Unit Toughness (UT), Shock Resistance (SR), and Malleability (MA).   
SWG Infinity

Surveying & Harvesters: Players use Survey Tools to locate high-concentration hotspots (>85%) and place automated Harvester Structures powered by energy and maintenance credits.

Assembly & Experimentation:

Load a schematic with specific resource types.   
SWG Nexus

Roll Assembly to create a prototype.

Spend Experimentation Points to push specific attributes (e.g., pushing Conductivity on a Power Cube or Damage on a Carbine).

Convert successful prototypes into Factory Schematics for mass production in player-built factories.

D. Player Housing, Vendors & Player Cities
Freeform Placement: Architects craft deeds for small/medium/large cube houses, factories, and civic buildings that can be placed openly across buildable terrain.

Player-Run Economy: Merchants place automated Vendor Terminals inside their homes/shops, list items on regional Bazaar Terminals, and set custom prices.

Player Cities: When enough players declare residency near a placed City Hall, the settlement ranks up (Outpost 
→
→ Metropolis), unlocking civic structures (Shuttleports, Cloning Centers, Cantinas, Banks) and city-wide specialization buffs (e.g., Industrial Center for faster factory speeds, Enhancement Center for longer Doctor/Entertainer buffs).   
Star Wars Galaxies Restoration

5. Development Phases (Prototype to MMO)
Phase 1 — Core Prototype Stabilization (Current):

Complete refactor of RideableBike / BikeInventory into RideableVehicle / VehicleInventory (1x1x4, 1x3x5, 5x15x5).

Verify hover physics when Turned ON vs. Ridden across uneven terrain.

Re-enable EngineCubeSpawner.cs and add PowerCubeSpawner.cs for end-to-end testing.

Phase 2 — Resource & Crafting Loop:

Replace static WorldItemSpawner.cs cubes with a procedural Resource Concentration Grid and a basic Survey Tool.

Implement basic Crafting Tool UI allowing players to craft Engine Cubes, Power Cubes, and Vehicle Deeds from gathered resources.

Phase 3 — HAM Combat, Wounds & Interdependence:

Replace simple health/consume placeholders with the Health / Action / Mind pool system, Wounds, and Battle Fatigue.

Implement basic Medic healing items and Entertainer area-of-effect aura buffs.

Phase 4 — Dedicated Server & Persistence Migration:

Transition from Unity Relay (NetworkStartHelper.cs) and local JSON (HostInventoryStore.cs) to headless dedicated server shards and a persistent SQL/NoSQL world database.

Add player structure placement, vendor terminals, and skill-box progression trees.