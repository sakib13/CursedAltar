# PHASE 1: Development Without Hardware

**Goal:** Build the complete VR experience on Quest 3S with simulated ESP32 input. When this phase is done, the full game works standalone — grab, ray-cast, candle reaction, buzzer trigger — all testable without any hardware.

---

## GAME STORY

You are the last paranormal investigator sent to a cursed cabin in the woods. Every investigator before you has disappeared. Your job: find the source of the curse and contain it.

You enter the cabin. It is dark. A single candle flickers on the table. A door rattles on its own. Something is wrong.

As you reach toward the candle on the table, its flame grows violent. The closer your hand gets, the more the room reacts. At close range, a cursed skull materializes on the table.

You must grab the skull and place it back on the altar (the table) to seal the curse. When you do, a sound tears through the real world — the buzzer fires. The room calms. The curse is sealed. For now.

---

## GAMEPLAY FLOW (Start to Finish)

The player puts on the Quest 3S headset. They are standing inside a dark cabin. The only light source is a dim candle flickering on a table in front of them. The room is silent except for a low ambient drone.

**Interaction 1 — Ray-cast the Door:**
The player notices a closed door to their side. They aim their controller at the door and pull the trigger. The door shakes violently and a loud creak/rattle sound plays. The door does not open. Scratch marks appear. This tells the player something is wrong — they are trapped.

**Interaction 2 — Reach toward the Candle (Ultrasonic Sensor):**
After the door interaction, the candle on the table begins pulsing more noticeably, drawing the player's attention. The player reaches their real hand toward the table. As their hand gets closer (detected by the ultrasonic sensor on the real table), the candle flame grows in intensity — dim at first, then brighter, then aggressively bright. The room begins to feel more unsettling with each step closer. When the player's hand is very close (under 15cm), the candle hits maximum intensity and a cursed skull suddenly appears on the table next to it.

**Interaction 3 — Grab the Skull and Place it on the Table (Grab + Buzzer):**
The skull is now sitting on the table. The player reaches out with their controller, grips the skull using the grip button, and picks it up. They can hold it, look at it, move it around. To seal the curse, they must place the skull back down on the table. When the skull is released near the table surface, the real-world buzzer fires — a harsh, eerie tone from the physical table beside the player. After 1.5 seconds the buzzer stops, the candle dims back to normal, the room lighting slowly warms up, and text appears: "The room is sealed. For now." The experience ends.

**Total duration: approximately 1-2 minutes per playthrough.**

---

## STEP-BY-STEP

### Step 1: Create Unity Project
**Who:** You (Supervisor)
**Action:**
1. Open Unity Hub
2. Create new project using Unity 6 with the "3D (URP)" template
3. Name it "CursedAltar"
4. Wait for project to open

**Expected result:** Empty Unity 6 URP project open in the editor.

---

### Step 2: Install Meta XR SDK
**Who:** You (Supervisor)
**Action:**
1. Go to Window > Package Manager
2. Click "+" > "Add package by name" or go to My Assets
3. Search and import **Meta XR All-in-One SDK** (latest version, v85) from the Unity Asset Store
   - Asset Store link: https://assetstore.unity.com/packages/tools/integration/meta-xr-all-in-one-sdk-269657
   - This will install: Meta XR Core SDK, Meta XR Interaction SDK Essentials, Meta XR Interaction SDK OVR Integration, and all dependencies
4. After import, go to **Meta > Tools > Project Setup Tool**
5. Apply all recommended settings (this auto-configures XR Plugin Management, Android build target, Vulkan graphics API, etc.)
6. Go to Edit > Project Settings > Player > Android tab:
   - Verify Minimum API Level is set to Android 10 (API 29) or higher
   - Verify Scripting Backend is IL2CPP
   - Verify Target Architectures is ARM64 only

**Expected result:** Meta XR SDK installed, project configured for Quest standalone builds.

---

### Step 3: Import Cabin Environment
**Who:** You (Supervisor)
**Action:**
1. Go to Window > Asset Store (or open browser to the asset link)
2. Search "Cabin Environment" by Gregory Seguru (or use: https://assetstore.unity.com/packages/3d/environments/cabin-environment-98014)
3. Add to "My Assets" if not already added
4. In Package Manager > My Assets, find "Cabin Environment" and click Import
5. Import all files

**Expected result:** Cabin asset appears in the Project window under Assets.

---

### Step 4: Import a Free Skull Asset
**Who:** You (Supervisor)
**Action:**
1. Find any free skull 3D model from the Unity Asset Store
2. Import it into the project
3. If you cannot find one, let me know — I will provide an alternative approach using a Unity primitive with a label

**Expected result:** Skull 3D model available in the Project window.

---

### Step 5: Set Up the Cabin Scene
**Who:** You (Supervisor)
**Action:**
1. Open the demo scene from the Cabin Environment asset (look in the asset's folder for a scene file)
2. Delete any existing camera in the scene (the Meta camera rig will replace it)
3. Open **Meta > Tools > Building Blocks** panel
4. Drag the **Camera Rig** Building Block into the scene
   - This adds an OVRCameraRig prefab to the scene
   - Alternatively, for full interaction support, search the project for the **OVRCameraRigInteraction** prefab (located in the Interaction SDK package) and drag it into the scene instead — this comes pre-configured with grab interactors, ray interactors, and hand interactors on both controllers
5. Position the camera rig inside the cabin where you want the player to stand, facing the table
6. If you used the basic Camera Rig block (not OVRCameraRigInteraction), also drag in the **Controller Tracking** Building Block
7. Darken the scene:
   - Select the Directional Light, reduce intensity to 0.1-0.2
   - Change light color to a dark blue tint (RGB: 100, 100, 160)

**Expected result:** You can build to Quest, put on the headset, and stand inside the dark cabin looking at the table.

---

### Step 6: Make the Door Interactable (Ray-cast)
**Who:** You (Supervisor) + Me (Code)
**Action — You:**
1. Select the door object in the cabin scene hierarchy
2. Add a **Box Collider** component to the door (if it doesn't have one)
3. Add a **Rigidbody** component, set "Is Kinematic" to true
4. Add a **ColliderSurface** component to the door — in its "Collider" field, drag the Box Collider you just added
5. Add a **RayInteractable** component to the door — in its "Surface" field, drag the ColliderSurface component
6. Add an **InteractableUnityEventWrapper** component to the door — in its "Interactable View" field, drag the RayInteractable component
7. Add an **AudioSource** component to the door (no clip assigned yet — we will assign in script or you can assign a door rattle/creak sound clip)

**Action — Me:**
I will write `DoorRayCast.cs`. You attach it to the door object.

**What the script does:**
- Listens for the InteractableUnityEventWrapper's `WhenSelect` UnityEvent (triggered when player points ray at door and pulls trigger)
- Plays a door rattle sound
- Shakes the door briefly using a small rotation animation in code
- Has a cooldown so it can't be spammed

**Setup — You (after attaching script):**
- In the InteractableUnityEventWrapper component on the door, under the `WhenSelect` event, click "+" and drag the door object, then select `DoorRayCast > OnDoorSelected()`

**Expected result:** Player points at door, pulls trigger, door rattles and creaks.

---

### Step 7: Set Up the Candle (Ultrasonic Target)
**Who:** You (Supervisor) + Me (Code)
**Action — You:**
1. Locate the candle or lantern object on the table in the cabin scene
2. If there is no candle in the cabin asset, create one:
   - Create an empty GameObject named "Candle"
   - Add a Point Light as a child, color orange/yellow, range 3, intensity 1
   - Position it on the table
3. Make sure the candle has a Point Light component (either existing or the one you added)

**Action — Me:**
I will write `CandleController.cs`. You attach it to the candle object.

**What the script does:**
- Receives distance data (from ESP32 in Phase 2, from simulation in Phase 1)
- Maps distance to candle light intensity:
  - Far (>50cm): intensity 0.5 (dim flicker)
  - Medium (30-50cm): intensity 1.5 (brighter, starts pulsing)
  - Close (15-30cm): intensity 3.0 (bright, fast pulse)
  - Very close (<15cm): intensity 5.0 — triggers skull activation
- Simulation mode: holding the left controller's primary button (X button) gradually decreases the simulated distance (simulates hand approaching sensor). Works both in editor and on Quest standalone.

**Expected result:** Holding X button makes the candle grow brighter. At max, the skull activates.

---

### Step 8: Set Up the Skull (Grab Object)
**Who:** You (Supervisor) + Me (Code)
**Action — You:**
1. Place the skull 3D model on the table, next to the candle
2. Set the skull as inactive in the Inspector (uncheck the checkbox at the top) — it will be activated by the candle script when distance threshold is reached
3. Add a **Rigidbody** component to the skull (Use Gravity: true, Is Kinematic: false)
4. Add a **Box Collider** or **Mesh Collider** to the skull
5. Add grab interaction using one of these methods:
   - **Method A (Building Block):** In the Building Blocks panel, drag the **Grab Interaction** block onto the skull object. This creates a child object with GrabInteractable + HandGrabInteractable + Grabbable components auto-configured.
   - **Method B (Right-click):** Right-click the skull in the hierarchy > **Interaction SDK > Add Grab Interaction** > use the Wizard > click "Fix All" > "Create"

**Action — Me:**
I will write `CursedSkull.cs`. You attach it to the skull object.

**What the script does:**
- Detects when the skull is grabbed (subscribes to `WhenSelectingInteractorViewAdded` on the GrabInteractable)
- Detects when the skull is released near the table (checks if skull Y position and XZ distance are close to the table's position)
- When placed on the table:
  - Sends "BUZZER:ON" to ESP32 (Phase 2), or plays a buzzer sound effect in VR (Phase 1 simulation)
  - After 1.5 seconds, sends "BUZZER:OFF"
  - Triggers end sequence: room lighting slowly returns to normal over 2 seconds

**Expected result:** Skull appears when candle reaches max. Player grabs skull with grip button, places it on table. Buzzer simulated, room calms.

---

### Step 9: ESP32 Connection Manager (with Simulation Mode)
**Who:** Me (Code)
**Action — You:** Create an empty GameObject named "NetworkManager" and attach the script.

**Action — Me:**
I will write `ESP32Connection.cs`.

**What the script does:**
- Has a public boolean: `simulationMode` (default: true for Phase 1)
- When `simulationMode = true`:
  - No TCP connection attempted
  - Distance values are simulated via left controller X button (hold to decrease distance, release to increase)
  - Buzzer commands are logged to console and play a local audio clip
- When `simulationMode = false` (Phase 2):
  - Connects to ESP32 via TCP at a configurable IP and port
  - Receives "DIST:<value>" messages and forwards to CandleController
  - Sends "BUZZER:ON" / "BUZZER:OFF" when CursedSkull triggers it
- Reconnects automatically if connection drops

**Expected result:** In Phase 1, everything works with controller simulation. In Phase 2, flip one boolean to false, enter ESP32 IP, and it connects to real hardware.

---

### Step 10: Add Audio
**Who:** You (Supervisor)
**Action:**
1. Find free horror ambient audio (freesound.org or any free source)
2. You need 3 audio clips:
   - Ambient loop (low drone, creepy atmosphere) — assign to an AudioSource on an empty GameObject
   - Door rattle/creak — assign to the Door's AudioSource
   - Buzzer simulation sound (a harsh tone) — assign to an AudioSource on the skull or NetworkManager
3. Import the audio files into Unity and assign them to the relevant AudioSource components

**Expected result:** The scene has ambient sound, door creaks on interaction, buzzer sound plays on skull placement.

---

### Step 11: Build and Test on Quest 3S
**Who:** You (Supervisor)
**Action:**
1. Connect Quest 3S to PC via USB
2. Enable Developer Mode on the Quest (if not already)
3. File > Build Settings > Switch Platform to Android
4. Click Build and Run
5. Put on the headset and test:
   - Can you look around the cabin?
   - Does pointing at the door and pulling trigger make it rattle?
   - Does the candle flicker?
   - Does holding the X button make the candle grow brighter and eventually activate the skull?
   - Can you grab the skull when it appears and place it on the table?

**Expected result:** Full game loop working on Quest 3S without any hardware.

---

## FILES I DELIVER IN PHASE 1

| File | Purpose |
|---|---|
| `ESP32Connection.cs` | Network manager with simulation mode |
| `CandleController.cs` | Candle light reaction to distance data |
| `CursedSkull.cs` | Skull grab detection, table placement, buzzer trigger |
| `DoorRayCast.cs` | Door rattle on ray-cast interaction |

---

## PHASE 1 CHECKLIST

- [ ] Unity 6 project created (URP)
- [ ] Meta XR All-in-One SDK installed (v85)
- [ ] Project Setup Tool applied all recommended settings
- [ ] Cabin Environment imported
- [ ] Skull asset imported
- [ ] OVRCameraRigInteraction (or Camera Rig + Controller Tracking Building Blocks) placed inside cabin
- [ ] Door set up with Box Collider + ColliderSurface + RayInteractable + InteractableUnityEventWrapper + DoorRayCast.cs
- [ ] Candle set up with Point Light + CandleController.cs
- [ ] Skull set up with Rigidbody + Collider + Grab Interaction Building Block + CursedSkull.cs (inactive by default)
- [ ] NetworkManager object created with ESP32Connection.cs (simulation mode ON)
- [ ] 3 audio clips imported and assigned
- [ ] Scene lighting darkened
- [ ] APK built and tested on Quest 3S
- [ ] Full game loop works: door rattle → candle reacts to simulated distance → skull appears → grab skull → place on table → buzzer simulation → room calms
