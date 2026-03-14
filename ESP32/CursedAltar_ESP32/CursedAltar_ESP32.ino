/*
 * ============================================================
 * CursedAltar — ESP32-S2 Thing Plus
 * ============================================================
 *
 * WHAT THIS DOES:
 *   - Connects to Wi-Fi and runs an HTTP server on port 80
 *   - Reads distance from ultrasonic sensor (HC-SR04)
 *   - Plays a cryptic staggered phone ringtone on 4 passive buzzers
 *   - Unity communicates with this over Wi-Fi (HTTP GET requests)
 *
 * ============================================================
 * SETUP INSTRUCTIONS (Arduino IDE):
 * ============================================================
 *
 * 1. Open Arduino IDE
 * 2. Go to File > Preferences
 * 3. In "Additional Board Manager URLs", add:
 *    https://raw.githubusercontent.com/espressif/arduino-esp32/gh-pages/package_esp32_index.json
 * 4. Go to Tools > Board > Boards Manager
 * 5. Search "esp32" and install "esp32 by Espressif Systems"
 * 6. Go to Tools > Board > ESP32 Arduino > "ESP32S2 Dev Module"
 * 7. Connect ESP32-S2 via USB-C
 * 8. Select the correct COM port under Tools > Port
 * 9. Click Upload
 * 10. Open Serial Monitor (115200 baud) to see the IP address
 *
 * ============================================================
 * WIRING DIAGRAM:
 * ============================================================
 *
 *   ESP32-S2 Thing Plus         Component
 *   ---------------------       ---------
 *   3V3   ──────────────────>   HC-SR04 VCC
 *   GND   ──────────────────>   HC-SR04 GND
 *   Pin 4   ────────────────>   HC-SR04 TRIG
 *   Pin 6   <───[1K]───┬───>   HC-SR04 ECHO
 *                       │
 *                     [1.2K]
 *                       │
 *                      GND
 *
 *   (The resistor voltage divider on ECHO is REQUIRED because
 *    HC-SR04 outputs 5V but ESP32-S2 GPIO is 3.3V only!)
 *
 *   Pin 8   ────────────────>   Buzzer 1 (+)
 *   Pin 10  ────────────────>   Buzzer 2 (+)
 *   GND    ─────────────────>   Both Buzzer (-) pins (shared GND)
 *
 * ============================================================
 */

#include <WiFi.h>
#include <WebServer.h>

// =====================
// Wi-Fi credentials — CHANGE THESE before demo
// =====================
const char* WIFI_SSID     = "ASUS_10";
const char* WIFI_PASSWORD = "header_7567";

// =====================
// Pin definitions
// =====================
#define TRIG_PIN    4
#define ECHO_PIN    6

#define BUZZER1_PIN 8
#define BUZZER2_PIN 10
#define NUM_BUZZERS 2

// =====================
// HTTP Server
// =====================
WebServer server(80);

// =====================
// Ultrasonic
// =====================
float currentDistance = 400.0; // cm (default far away)
unsigned long lastDistRead = 0;
const unsigned long DIST_INTERVAL = 100; // Read every 100ms

// =====================
// Buzzer ringtone state
// =====================
bool buzzerPlaying = false;
unsigned long buzzerStartTime = 0;
const unsigned long BUZZER_DURATION = 5000; // 5 seconds

// Stagger offsets for each buzzer (milliseconds)
// Each buzzer starts the pattern slightly later for eerie echo effect
const unsigned long STAGGER_OFFSET[2] = {0, 200};

// Slight frequency detuning per buzzer for dissonance
// Buzzer 2 is slightly detuned for an eerie beating effect
const float DETUNE[2] = {1.0, 1.03};

// Cryptic phone ring pattern
// Each ring cycle: two short bursts then silence
// Total cycle = ~2000ms (ring-ring-pause)
// Frequencies create an eerie old rotary phone sound
const int RING_FREQ_HIGH = 1400;   // Old rotary phone high tone
const int RING_FREQ_LOW  = 1800;   // Old rotary phone low tone
const int RING_BURST_MS  = 150;    // Each burst length
const int RING_GAP_MS    = 100;    // Gap between two bursts
const int RING_PAUSE_MS  = 600;    // Silence between ring pairs
// One full cycle = BURST + GAP + BURST + PAUSE = 150+100+150+600 = 1000ms

// =====================
// SETUP
// =====================
void setup() {
  Serial.begin(115200);
  delay(1000);
  Serial.println("\n=== CursedAltar ESP32-S2 ===");

  // --- Ultrasonic pins ---
  pinMode(TRIG_PIN, OUTPUT);
  pinMode(ECHO_PIN, INPUT);
  digitalWrite(TRIG_PIN, LOW);

  // --- Buzzer LEDC setup ---
  // New ESP32 Arduino API: ledcAttach(pin, freq, resolution)
  ledcAttach(BUZZER1_PIN, 1000, 8);
  ledcAttach(BUZZER2_PIN, 1000, 8);

  // Make sure buzzers are silent
  stopAllBuzzers();

  // --- Connect to Wi-Fi ---
  Serial.print("Connecting to Wi-Fi: ");
  Serial.println(WIFI_SSID);
  WiFi.begin(WIFI_SSID, WIFI_PASSWORD);

  int attempts = 0;
  while (WiFi.status() != WL_CONNECTED && attempts < 30) {
    delay(500);
    Serial.print(".");
    attempts++;
  }

  if (WiFi.status() == WL_CONNECTED) {
    Serial.println("\nWi-Fi connected!");
    Serial.print("IP Address: ");
    Serial.println(WiFi.localIP());
    Serial.println("Enter this IP in Unity's ESP32Connection component.");
  } else {
    Serial.println("\nWi-Fi FAILED! Check SSID and password.");
    Serial.println("The ESP32 will keep trying in the background.");
  }

  // --- HTTP endpoints ---
  server.on("/distance", handleDistance);
  server.on("/buzzer", handleBuzzerOn);
  server.on("/buzzer/stop", handleBuzzerStop);
  server.on("/", handleRoot);

  server.begin();
  Serial.println("HTTP server started on port 80");
}

// =====================
// LOOP
// =====================
void loop() {
  // Handle incoming HTTP requests
  server.handleClient();

  // Read ultrasonic distance periodically
  if (millis() - lastDistRead >= DIST_INTERVAL) {
    lastDistRead = millis();
    currentDistance = readDistance();
  }

  // Update buzzer ringtone (non-blocking)
  if (buzzerPlaying) {
    updateRingtone();
  }
}

// =====================
// ULTRASONIC DISTANCE
// =====================
float readDistance() {
  // Send trigger pulse
  digitalWrite(TRIG_PIN, LOW);
  delayMicroseconds(2);
  digitalWrite(TRIG_PIN, HIGH);
  delayMicroseconds(10);
  digitalWrite(TRIG_PIN, LOW);

  // Read echo pulse duration (timeout after 30ms = ~5m max)
  long duration = pulseIn(ECHO_PIN, HIGH, 30000);

  if (duration == 0) {
    // No echo received — object too far or sensor error
    return 400.0;
  }

  // Convert to cm: speed of sound = 343m/s = 0.0343 cm/us
  // Distance = (duration * 0.0343) / 2 (round trip)
  float dist = (duration * 0.0343) / 2.0;
  return dist;
}

// =====================
// BUZZER RINGTONE
// =====================

// Called every loop iteration when buzzer is active
void updateRingtone() {
  unsigned long elapsed = millis() - buzzerStartTime;

  // Auto-stop after duration
  if (elapsed >= BUZZER_DURATION) {
    stopAllBuzzers();
    buzzerPlaying = false;
    Serial.println("Buzzer ringtone finished (5s)");
    return;
  }

  // Buzzer pins array for easy looping
  const int buzzerPins[NUM_BUZZERS] = {BUZZER1_PIN, BUZZER2_PIN};

  // Update each buzzer with its staggered offset
  for (int i = 0; i < NUM_BUZZERS; i++) {
    int pin = buzzerPins[i];

    // This buzzer's time (accounting for stagger)
    long buzzerTime = (long)elapsed - (long)STAGGER_OFFSET[i];

    if (buzzerTime < 0) {
      // Not started yet (stagger delay)
      ledcWriteTone(pin, 0);
      continue;
    }

    // Position within the ring cycle
    unsigned long cycleTime = (unsigned long)buzzerTime %
                              (RING_BURST_MS + RING_GAP_MS + RING_BURST_MS + RING_PAUSE_MS);

    // Determine what phase of the ring we're in
    float freq = 0;

    if (cycleTime < RING_BURST_MS) {
      // First ring burst — high tone
      freq = RING_FREQ_HIGH * DETUNE[i];
    }
    else if (cycleTime < RING_BURST_MS + RING_GAP_MS) {
      // Gap between bursts — silence
      freq = 0;
    }
    else if (cycleTime < RING_BURST_MS + RING_GAP_MS + RING_BURST_MS) {
      // Second ring burst — low tone (creates the "brrring-brrring" effect)
      freq = RING_FREQ_LOW * DETUNE[i];
    }
    else {
      // Pause between ring pairs — silence
      freq = 0;
    }

    if (freq > 0) {
      ledcWriteTone(pin, (int)freq);
    } else {
      ledcWriteTone(pin, 0);
    }
  }
}

void stopAllBuzzers() {
  ledcWriteTone(BUZZER1_PIN, 0);
  ledcWriteTone(BUZZER2_PIN, 0);
}

// =====================
// HTTP HANDLERS
// =====================

void handleRoot() {
  String html = "CursedAltar ESP32-S2\n";
  html += "Endpoints:\n";
  html += "  GET /distance    - Ultrasonic distance in cm\n";
  html += "  GET /buzzer      - Play cryptic ringtone (5s)\n";
  html += "  GET /buzzer/stop - Stop buzzers\n";
  html += "\nDistance: " + String(currentDistance, 1) + " cm\n";
  html += "Buzzer: " + String(buzzerPlaying ? "PLAYING" : "OFF") + "\n";
  server.send(200, "text/plain", html);
}

void handleDistance() {
  // Return distance as plain text number
  server.send(200, "text/plain", String(currentDistance, 1));
}

void handleBuzzerOn() {
  // Always do a clean reset: stop, re-attach pins, then start fresh
  stopAllBuzzers();
  delay(50);
  ledcDetach(BUZZER1_PIN);
  ledcDetach(BUZZER2_PIN);
  delay(50);
  ledcAttach(BUZZER1_PIN, 1000, 8);
  ledcAttach(BUZZER2_PIN, 1000, 8);
  stopAllBuzzers();

  buzzerPlaying = true;
  buzzerStartTime = millis();
  Serial.println("Buzzer ringtone started");
  server.send(200, "text/plain", "OK");
}

void handleBuzzerStop() {
  stopAllBuzzers();
  buzzerPlaying = false;
  Serial.println("Buzzer stopped");
  server.send(200, "text/plain", "OK");
}
