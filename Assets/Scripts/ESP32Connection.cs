using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public class ESP32Connection : MonoBehaviour
{
    [Header("ESP32 Wi-Fi Settings")]
    public string esp32IP = "192.168.1.100"; // Change to ESP32's IP (shown in Serial Monitor)

    [Header("Mode")]
    public bool simulationMode = true;

    [Header("Simulation Settings")]
    public float simDistanceSpeed = 50f;
    public float simMaxDistance = 200f;

    [Header("Polling")]
    public float pollInterval = 0.15f; // How often to poll distance (seconds)

    private float currentDistance = 200f;
    private bool isPolling = false;
    private bool buzzerActive = false;

    void Update()
    {
        // Simulation mode — only runs after StartPolling() is called
        // Hold BOTH grip buttons (left+right) simultaneously to decrease distance
        if (simulationMode && isPolling)
        {
            bool bothGrips = OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.LTouch)
                          && OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch);
            if (bothGrips)
            {
                currentDistance -= simDistanceSpeed * Time.deltaTime;
                if (currentDistance < 5f) currentDistance = 5f;
            }
            else
            {
                currentDistance += simDistanceSpeed * Time.deltaTime;
                if (currentDistance > simMaxDistance) currentDistance = simMaxDistance;
            }
        }
    }

    // Get the latest ultrasonic distance in cm
    public float GetDistance()
    {
        return currentDistance;
    }

    // Start polling distance from ESP32
    public void StartPolling()
    {
        if (simulationMode)
        {
            isPolling = true; // enables simulation in Update()
            Debug.Log("[Simulation] Distance polling started (hold BOTH grips to approach)");
            return;
        }

        if (!isPolling)
        {
            isPolling = true;
            StartCoroutine(PollDistanceLoop());
        }
    }

    // Stop polling
    public void StopPolling()
    {
        isPolling = false;
    }

    // Send buzzer play command
    public void SendBuzzerOn()
    {
        if (simulationMode)
        {
            Debug.Log("[Simulation] BUZZER:ON (cryptic ringtone for 5s)");
            return;
        }

        buzzerActive = true;
        StartCoroutine(SendHTTP("/buzzer", "Buzzer ON"));
    }

    // Send buzzer stop command
    public void SendBuzzerOff()
    {
        if (simulationMode)
        {
            Debug.Log("[Simulation] BUZZER:OFF");
            return;
        }

        buzzerActive = false;
        StartCoroutine(SendHTTP("/buzzer/stop", "Buzzer OFF"));
    }

    // Poll distance in a loop
    private IEnumerator PollDistanceLoop()
    {
        Debug.Log("[ESP32] Distance polling started (interval=" + pollInterval + "s)");

        while (isPolling)
        {
            string url = "http://" + esp32IP + "/distance";
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 2;
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    string response = req.downloadHandler.text.Trim();
                    if (float.TryParse(response, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float dist))
                    {
                        currentDistance = dist;
                    }
                }
                else
                {
                    Debug.LogWarning("[ESP32] Poll failed: " + req.error);
                }
            }

            yield return new WaitForSeconds(pollInterval);
        }

        Debug.Log("[ESP32] Distance polling stopped");
    }

    // Generic HTTP GET request
    private IEnumerator SendHTTP(string endpoint, string logLabel)
    {
        string url = "http://" + esp32IP + endpoint;
        Debug.Log("[ESP32] Sending: " + url);

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.timeout = 3;
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
                Debug.Log("[ESP32] " + logLabel + " OK");
            else
                Debug.LogWarning("[ESP32] " + logLabel + " failed: " + req.error);
        }
    }
}
