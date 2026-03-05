using System.Collections.Generic;
using uPLibrary.Networking.M2Mqtt.Messages;
using M2MqttUnity;
using UnityEngine;

namespace ExtralityLab
{
    public class MqttClientExampleReceiveRGB : M2MqttUnityClient
    {
        [Header("Topics Config")]
        public string subscribedTopic = "myUnityApp/analogRGB";
        public bool autoSubscribe = false;

        [Header("Actuators Config")]
        public Light virtualLightRGB;
        public Color currentColor;

        private List<string> eventMessages = new List<string>();

        protected override void Start()
        {
            base.Start();

            // Add here your custom Start() below:

        }

        protected override void Update()
        {
            base.Update();

            if (eventMessages.Count > 0)
            {
                foreach (string msg in eventMessages)
                {
                    ProcessMessage(msg);
                }
                eventMessages.Clear();
            }
        }

        protected override void OnConnecting()
        {
            base.OnConnecting();
            Debug.Log($"MQTT: subscription {subscribedTopic} connecting to broker on " + brokerAddress + ":" + brokerPort.ToString() + "...\n");
        }

        protected override void OnConnected()
        {
            // base.OnConnected(); // Uncommenting this will autosubscribe to topics
            Debug.Log($"MQTT: subscription {subscribedTopic} connected!");
            if (autoSubscribe)
                SubscribeTopics();
        }

        protected override void SubscribeTopics()
        {
            client.Subscribe(new string[] { subscribedTopic }, new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE });
        }

        protected override void UnsubscribeTopics()
        {
            client.Unsubscribe(new string[] { subscribedTopic });
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        protected override void DecodeMessage(string topic, byte[] message)
        {
            string msg = System.Text.Encoding.UTF8.GetString(message);
            // Debug.Log("Received: " + msg);
            eventMessages.Add(msg);
        }

        ////// CALLBACKS from Buttons

        public void SubscribeToMqttTopic()
        {
            SubscribeTopics();
        }

        public void UnsubscribeFromTopic()
        {
            UnsubscribeTopics();
        }

        private void ProcessMessage(string msg)
        {
            Debug.Log($"MQTT Subscription {subscribedTopic} received: " + msg);

            string[] rgb = msg.Split(",");
            // Slider values come between [0,100], map to [0,1]
            float red = float.Parse(rgb[0]) / 100.0f;
            float green = float.Parse(rgb[1]) / 100.0f;
            float blue = float.Parse(rgb[2]) / 100.0f;
            // Create a new color from the RGB
            currentColor = new Color(red, green, blue);

            // Apply color to the Analog RGB virtual lamp
            virtualLightRGB.color = currentColor;
        }
        
    }
}
