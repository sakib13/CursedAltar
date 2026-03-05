using UnityEngine;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public class ESP32Connection : MonoBehaviour
{
    [Header("Connection (for Phase 2 hardware)")]
    public string esp32IP = "192.168.1.100";
    public int esp32Port = 7777;

    [Header("Mode")]
    public bool simulationMode = true;

    [Header("Simulation Settings")]
    public float simDistanceSpeed = 50f;
    public float simMaxDistance = 200f;

    private TcpClient client;
    private NetworkStream stream;
    private Thread receiveThread;
    private bool isConnected = false;
    private float currentDistance = 200f;
    private bool shouldReconnect = false;

    void Start()
    {
        if (!simulationMode)
        {
            Connect();
        }
    }

    void Update()
    {
        // Simulation mode — hold X button to decrease distance
        if (simulationMode)
        {
            if (OVRInput.Get(OVRInput.Button.Three))
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

        if (!simulationMode && shouldReconnect)
        {
            shouldReconnect = false;
            Connect();
        }
    }

    void Connect()
    {
        try
        {
            client = new TcpClient();
            client.BeginConnect(esp32IP, esp32Port, OnConnected, null);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("ESP32 connection failed: " + e.Message);
            shouldReconnect = true;
        }
    }

    void OnConnected(System.IAsyncResult result)
    {
        try
        {
            client.EndConnect(result);
            stream = client.GetStream();
            isConnected = true;
            Debug.Log("Connected to ESP32 at " + esp32IP + ":" + esp32Port);

            receiveThread = new Thread(ReceiveData);
            receiveThread.IsBackground = true;
            receiveThread.Start();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("ESP32 connect error: " + e.Message);
            shouldReconnect = true;
        }
    }

    void ReceiveData()
    {
        byte[] buffer = new byte[256];
        while (isConnected)
        {
            try
            {
                int length = stream.Read(buffer, 0, buffer.Length);
                if (length > 0)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, length).Trim();
                    if (message.StartsWith("DIST:"))
                    {
                        string value = message.Substring(5);
                        if (float.TryParse(value, out float dist))
                        {
                            currentDistance = dist;
                        }
                    }
                }
                else
                {
                    isConnected = false;
                    shouldReconnect = true;
                }
            }
            catch
            {
                isConnected = false;
                shouldReconnect = true;
            }
        }
    }

    public float GetDistance()
    {
        return currentDistance;
    }

    public void SendBuzzerOn()
    {
        if (simulationMode)
        {
            Debug.Log("[Simulation] BUZZER:ON");
            return;
        }
        SendToESP32("BUZZER:ON");
    }

    public void SendBuzzerOff()
    {
        if (simulationMode)
        {
            Debug.Log("[Simulation] BUZZER:OFF");
            return;
        }
        SendToESP32("BUZZER:OFF");
    }

    void SendToESP32(string message)
    {
        if (!isConnected || stream == null) return;
        try
        {
            byte[] data = Encoding.UTF8.GetBytes(message + "\n");
            stream.Write(data, 0, data.Length);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("ESP32 send error: " + e.Message);
            isConnected = false;
            shouldReconnect = true;
        }
    }

    void OnDestroy()
    {
        isConnected = false;
        if (receiveThread != null)
            receiveThread.Abort();
        if (stream != null)
            stream.Close();
        if (client != null)
            client.Close();
    }
}
