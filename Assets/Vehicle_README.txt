Vehicle Prototype
=================

The sample scene contains a bike, car and large droid made from unit cubes.
Dimensions are width x height x length (X x Y x Z): 1 x 1 x 4, 1 x 3 x 5
and 5 x 15 x 5.
Orange engine cubes and blue power cubes are scattered around the terrain.

Left-click a cube to select it, then left-click it again within the capsule's
action circle and choose Pick up. Select and click a vehicle the same way, then
choose Inventory. Select a player item, then click a vehicle slot to transfer
one item. Click a filled vehicle slot with no player item selected to take one.
The bike has 4 cargo slots, the car has 16 and the large droid has 24, in
addition to one engine slot and one power slot each.

Enter an unoccupied vehicle whether it is on or off, even with empty cube
slots. To drive it, install one matching cube in each dedicated slot and turn
it on from its menu or press T while riding. WASD drives when it is on; E
dismounts. The vehicle remains on and hovering until switched off, a required cube is
removed, or the power cube runs out of energy. The energy bar above the vehicle
shows its remaining charge. Energy drains slowly while powered on, even when
parked, and drains faster with distance driven. An empty cube remains installed
but cannot power the vehicle; replace it with a charged cube. A full cube lasts
about 33 minutes at idle; driving uses an additional 0.005% per meter.
Press T or use its menu to switch power while riding. Dismount before accessing
the vehicle inventory.
While hovering, vehicles gently sway and cast a blue glow onto the ground.
The glow and sway fade as the vehicle powers on or off.
Powered vehicles also kick up fast-moving cube dust beneath and around them;
the cubes grow and fade as they disperse.
The dust is visual only, appears for each client, and stops when power is lost.

The host saves vehicle position, direction, power state, dedicated slots and
cargo in its existing host-inventory.json. Cube energy follows the cube through
inventory, cargo, and dropped pickups. Energy is saved at least every five
seconds while in use and when switching power or transferring a cube. A transfer saves the player and
vehicle inventories together. Vehicle state is local to the host, not cloud
storage.

Resource surveying and crafting test
-------------------------------------
Resource hotspots are generated from a UTC daily seed when the scene loads and
shift when the scene is loaded on a later UTC day. Press V to survey the nearest
hotspot; the result shows concentration, Overall
Quality (OQ), and Conductivity (CD). Pick up the resource cubes at the hotspot
to gather stacks with those OQ and CD values.

Select and click the crafting terminal within the action circle. Choose Engine
Cube and select at least 5 metal and 3 petrochemical resources, or choose Power
Cube and select at least 5 metal and 3 energy resources. Then choose Assemble.

Assembly has a 90% success chance. Failure consumes the selected resources.
A successful prototype has five experimentation points. Select it in the
terminal and use Experiment: each point has a 75% chance to increase speed by
4 percentage points, otherwise speed decreases by 5. Finish creates an Engine
Cube; unused points are lost. Its speed percentage multiplies the normal
vehicle speed when installed. Engine speed is derived from weighted resource
OQ and CD. Power Cubes are crafted directly; their OQ is retained when
installed. Pre-existing engine cubes retain normal speed. The host saves
prototypes, crafted stats, and installed engine and power quality. Resource
pickup IDs include the UTC date, so gathered deposits reset when the map shifts.

In the player inventory, left-click a slot to select it and left-click the
selected slot again to open its item actions.
