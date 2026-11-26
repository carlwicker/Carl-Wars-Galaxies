Vehicle Prototype
=================

The sample scene contains a bike, car and large droid made from unit cubes.
Dimensions are width x height x length (X x Y x Z): 1 x 1 x 4, 1 x 3 x 5
and 5 x 15 x 5.
Orange engine cubes and blue power cubes are scattered around the terrain.

Collect a cube by right-clicking it and choosing Pick up. Right-click a vehicle
and choose Inventory. Select a player item, then click a vehicle slot to transfer
one item. Click a filled vehicle slot with no player item selected to take one.
The bike has 4 cargo slots, the car has 16 and the large droid has 24, in
addition to one engine slot and one power slot each.

Install one matching cube in each dedicated slot, then use the vehicle menu to
turn it on and ride it. WASD drives, E dismounts, and the vehicle remains on
and hovering until switched off or a required cube is removed. Dismount before
accessing the inventory or switching the vehicle off.
While hovering, vehicles gently sway and cast a blue glow onto the ground.
The glow and sway fade as the vehicle powers on or off.

The host saves vehicle position, direction, power state, dedicated slots and
cargo in its existing host-inventory.json. A transfer saves the player and
vehicle inventories together. Vehicle state is local to the host, not cloud
storage.
