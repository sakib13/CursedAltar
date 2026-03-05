using System.Collections.Generic;
using uPLibrary.Networking.M2Mqtt.Messages;
using M2MqttUnity;
using UnityEngine;

namespace ExtralityLab
{
    public class MqttClientExampleSendRGB : M2MqttUnityClient
    {
        [Header("Topics Config")]
        public string publishTopicName = "myUnityApp/analogRGB";

        public int valueRed = 0;
        public int valueGreen = 0;
        public int valueBlue = 0;

        // Edited on demand based on value Red, Green, Blue.
        public string message = "";

        protected override void Start()
        {
            base.Start();

            // Add here your custom Start() below:

        }

        protected override void Update()
        {
            base.Update();

            // Add here your custom Update() below:

        }

        protected override void OnConnecting()
        {
            base.OnConnecting();
            Debug.Log($"MQTT: {publishTopicName} connecting to broker on " + brokerAddress + ":" + brokerPort.ToString() + "...\n");
        }

        protected override void OnConnected()
        {
            base.OnConnected();
            Debug.Log($"MQTT: {publishTopicName} connected!");
            PublishTopicValue();
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        ////// CALLBACKS from Buttons

        public void SetValueRed(float value)
        {
            valueRed = (int)value;
            PublishTopicValue();
        }

        public void SetValueGreen(float value)
        {
            valueGreen = (int)value;
            PublishTopicValue();
        }

        public void SetValueBlue(float value)
        {
            valueBlue = (int)value;
            PublishTopicValue();
        }

        public void PublishTopicValue()
        {
            message = $"{valueRed}, {valueGreen}, {valueBlue}";

            client.Publish(publishTopicName,
                            System.Text.Encoding.UTF8.GetBytes(message),
                            MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE,
                            false);
        }
        
    }
}
