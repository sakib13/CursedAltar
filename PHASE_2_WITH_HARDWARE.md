# PHASE 2: Development With Hardware

**Prerequisite:** Phase 1 is fully complete. The VR game works on Quest 3S in simulation mode.

**Goal:** Replace simulated input with real ESP32 hardware. Ultrasonic sensor controls the candle. Grabbing the skull fires the real buzzer. Record video. Write README.

---

## HARDWARE LIST

| Component | Source |
|---|---|
| ESP32-C3 Mini (with headers) | Purchase from Electrokit (95 SEK) or borrow from lab |
| HC-SR04 Ultrasonic Sensor | Borrow from lab or purchase from Electrokit (49 SEK) |
| Passive Piezo Buzzer | Borrow from lab or purchase from Electrokit (29 SEK) |
| Breadboard | Purchase or borrow |
| Jumper wires (6-7) | Purchase or borrow |
| 2 resistors (1K ohm + 2K ohm) | Borrow from lab resistor stock |
| USB-C cable | You already own |
| USB power bank (22.5W) | You already own |

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

### Step 1: Install Arduino IDE
**Who:** You (Supervisor)
**Action:**
1. Download and install Arduino IDE from https://www.arduino.cc/en/software
2. Open Arduino IDE
3. Go to File > Preferences
4. In "Additional Boards Manager URLs" add:
   ```
   https://raw.githubusercontent.com/espressif/arduino-esp32/gh-pages/package_esp32_index.json
   ```
5. Go to Tools > Board > Boards Manager
6. Search "esp32" and install "esp32 by Espressif Systems"
7. Connect ESP32-C3 Mini to PC via USB-C
8. Go to Tools > Board and select "ESP32C3 Dev Module"
9. Go to Tools > Port and select the COM port that appeared when you plugged in the ESP32

**Expected result:** Arduino IDE ready to upload code to ESP32-C3.

---

### Step 2: Wire the Circuit
**Who:** You (Supervisor)
**Action:**

**You will need to split the USB power from the power bank to get 5V for the HC-SR04.** The simplest way: use a second USB cable, cut the end, and expose the red (5V) and black (GND) wires. Alternatively, power the HC-SR04 from the ESP32's 5V pin if the board exposes one (check the ESP32-C3 Mini pinout — it may have a 5V pin from USB passthrough).

**Wiring diagram:**
```
BREADBOARD LAYOUT:

ESP32-C3 Mini          HC-SR04              Passive Buzzer
─────────────          ───────              ──────────────
5V (if available) ───→ VCC
GND ─────────────────→ GND ──────────────→ Negative (-)
GPIO 4 ──────────────→ TRIG
GPIO 5 ←── [1K res] ←─ ECHO
             │
         [2K res]
             │
            GND

GPIO 6 ──────────────────────────────────→ Positive (+)
```

**Voltage divider detail (ECHO pin protection):**
```
HC-SR04 ECHO ────┬──── 1K ohm resistor ────→ ESP32 GPIO 5
                 │
                 └──── 2K ohm resistor ────→ GND

This drops the 5V ECHO signal to ~3.3V for the ESP32.
The HC-SR04 itself still runs at full 5V on its VCC pin.
The TRIG pin receives 3.3V from the ESP32, which is sufficient (HC-SR04 recognizes anything above 2.5V as HIGH).
```

**Important:**
- All GND connections must be common (ESP32 GND, HC-SR04 GND, buzzer GND — all connected together on the breadboard)
- Double check wiring before powering on
- The HC-SR04 must receive 5V on VCC to function correctly

**Expected result:** Circuit wired on breadboard. Not powered yet.

---

### Step 3: Upload ESP32 Code
**Who:** Me (Code) + You (Upload)
**Action — Me:**
I will write `CursedAltar_ESP32.ino`.

**What the code does:**
- Connects to a WiFi network (you provide SSID and password — your phone hotspot)
- Prints assigned IP address to Serial Monitor (you note this down)
- Starts a TCP server on port 7777
- Reads ultrasonic sensor distance every 100ms
- Sends "DIST:<value>" to connected Unity client
- Listens for "BUZZER:ON" and "BUZZER:OFF" commands
- On "BUZZER:ON": plays an eerie tone on the passive buzzer using PWM
- On "BUZZER:OFF": stops the buzzer
- Handles client disconnection and reconnection

**Action — You:**
1. Open `CursedAltar_ESP32.ino` in Arduino IDE
2. Edit line with WiFi credentials:
   ```
   const char* ssid = "YourPhoneHotspot";
   const char* password = "YourPassword";
   ```
3. Click Upload
4. Open Serial Monitor (Tools > Serial Monitor, baud rate 115200)
5. You should see:
   ```
   Connecting to WiFi...
   Connected! IP: 192.168.x.x
   TCP Server started on port 7777
   ```
6. Note down the IP address

**Expected result:** ESP32 running, connected to WiFi, waiting for Unity to connect.

---

### Step 4: Test Hardware Standalone
**Who:** You (Supervisor)
**Action:**
1. With Serial Monitor open, wave your hand above the ultrasonic sensor
2. You should see distance values printing:
   ```
   DIST:45
   DIST:32
   DIST:15
   DIST:8
   ```
3. Type "BUZZER:ON" in the Serial Monitor send field and press Enter
4. The buzzer should make a sound
5. Type "BUZZER:OFF" — buzzer stops

**Expected result:** Sensor reads distance correctly. Buzzer responds to commands. Hardware verified working independently before connecting to Unity.

---

### Step 5: Connect Unity to ESP32
**Who:** You (Supervisor)
**Action:**
1. Open the Unity project from Phase 1
2. Select the "NetworkManager" GameObject
3. In the ESP32Connection component in the Inspector:
   - Set `simulationMode` to **false**
   - Set `esp32IP` to the IP address from Step 3 (e.g., "192.168.x.x")
   - Set `esp32Port` to 7777
4. Make sure your PC is on the same network as the ESP32:
   - Connect your PC to the same phone hotspot
5. Press Play in Unity Editor
6. Check the Console — you should see:
   ```
   Connected to ESP32 at 192.168.x.x:7777
   ```
7. Wave your hand over the ultrasonic sensor — the candle in VR should react

**Expected result:** Live bidirectional communication working in Unity Editor.

---

### Step 6: Test Full Loop in Editor
**Who:** You (Supervisor)
**Action:**
1. Unity in Play mode, ESP32 powered and connected
2. Test the full sequence:
   - Point at door → rattles (this has no ESP32 involvement, should work as before)
   - Move hand toward ultrasonic sensor → candle brightness changes in VR
   - Get very close (<15cm) → skull appears
   - Grab skull → place on table → real buzzer fires
3. If anything doesn't work, check Unity Console for error messages and let me know

**Expected result:** Full game loop with real hardware working in editor.

---

### Step 7: Build APK for Quest 3S
**Who:** You (Supervisor)
**Action:**
1. Make sure Quest 3S is connected to the **same phone hotspot** as the ESP32
2. In Unity, build the APK (File > Build Settings > Build and Run)
3. Put on the Quest 3S
4. Power the ESP32 from the USB power bank (disconnect from PC)
5. The ESP32 will connect to the phone hotspot automatically and start its TCP server
6. The Quest app will connect to the ESP32's IP
7. Test the full game loop on the headset

**Important networking note:**
- Phone hotspot is the central network
- ESP32 joins the phone hotspot (WiFi station mode)
- Quest 3S joins the same phone hotspot
- All three devices (phone, ESP32, Quest) are on the same local network
- The phone just acts as a router — it doesn't run anything

**Expected result:** Fully wireless. ESP32 on power bank, Quest standalone. No PC needed during demo.

---

### Step 8: Physical Setup for Demo Stall
**Who:** You (Supervisor)
**Action:**
1. Place a small table or stand in front of where the player will stand (~30cm away)
2. On the table:
   - Breadboard with ESP32 + ultrasonic sensor pointing UP + buzzer
   - Power bank connected to ESP32 via USB-C
   - Phone running hotspot (can be in your pocket)
3. Position the ultrasonic sensor so it is at the same relative position as the virtual candle/table in VR
4. The player stands at a marked spot (use tape on the floor)
5. Calibrate: put on the headset at that spot, verify the virtual table aligns roughly with the real table

```
DEMO STALL LAYOUT (top view):

    ┌──────────────────────────┐
    │                          │
    │   ┌──────────────────┐   │
    │   │  Table            │   │
    │   │  [Breadboard]     │   │
    │   │  [Power Bank]     │   │
    │   └──────────────────┘   │
    │                          │
    │       ┌──────┐           │
    │       │Player│           │
    │       │ here │           │
    │       └──────┘           │
    │     (tape on floor)      │
    │                          │
    └──────────────────────────┘
           ~ 1m x 1m
```

**Expected result:** Clean, compact demo setup.

---

### Step 9: Record Video
**Who:** You (Supervisor)
**Action:**
1. Record two views simultaneously (or edit together):
   - **VR view:** Use Quest's built-in screen recording (or SideQuest to stream to PC and capture)
   - **Physical view:** Phone camera (second phone or ask someone) showing the table setup, your hand approaching the sensor, and the buzzer going off
2. Show the full game loop:
   - Start the experience
   - Ray-cast the door → it rattles
   - Reach toward the table → candle reacts (show real hand approaching sensor in physical view)
   - Skull appears → grab it → place on table → buzzer fires (show buzzer going off in physical view)
   - Room calms, experience ends
3. Keep the video short — 2-3 minutes maximum
4. Optionally add text overlays explaining what's happening

**Expected result:** Clear demonstration video showing both VR and physical interaction.

---

### Step 10: Write README
**Who:** Me (Code) + You (Review)
**Action — Me:**
I will write a README.md covering:
- Project overview and concept
- Hardware list and wiring diagram
- Software requirements
- Setup instructions (step-by-step)
- How to run the project
- Demo video link

**Action — You:**
- Review and adjust any details
- Add your video link
- Submit

**Expected result:** Complete documentation ready for submission.

---

## FILES I DELIVER IN PHASE 2

| File | Purpose |
|---|---|
| `CursedAltar_ESP32.ino` | ESP32 Arduino code (WiFi, TCP, ultrasonic, buzzer) |
| `README.md` | Project documentation for submission |

Note: The Unity scripts are the same ones from Phase 1. No new Unity scripts are needed — only `simulationMode` is toggled to false.

---

## PHASE 2 CHECKLIST

- [ ] Arduino IDE installed with ESP32 board support
- [ ] Circuit wired on breadboard (ESP32 + HC-SR04 + buzzer + voltage divider)
- [ ] ESP32 code uploaded, connects to WiFi, prints IP
- [ ] Hardware tested standalone (distance readings in Serial Monitor, buzzer responds to commands)
- [ ] Unity project's simulationMode set to false, ESP32 IP entered
- [ ] Bidirectional communication working in Unity Editor
- [ ] APK built and deployed to Quest 3S
- [ ] Full wireless loop working (Quest + ESP32 on phone hotspot, ESP32 on power bank)
- [ ] Demo stall layout arranged, player position calibrated
- [ ] Video recorded showing VR view + physical setup
- [ ] README written and reviewed
- [ ] Files submitted

---

## COMPLETE FILE DELIVERY LIST (BOTH PHASES)

```
/Scripts (Unity — delivered in Phase 1)
  ├── ESP32Connection.cs
  ├── CandleController.cs
  ├── CursedSkull.cs
  └── DoorRayCast.cs

/ESP32 (Arduino — delivered in Phase 2)
  └── CursedAltar_ESP32.ino

/Docs (delivered in Phase 2)
  └── README.md
```
