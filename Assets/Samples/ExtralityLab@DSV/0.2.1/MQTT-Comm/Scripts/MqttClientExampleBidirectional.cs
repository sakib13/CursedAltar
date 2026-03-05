using System.Collections.Generic;
using M2MqttUnity;
using uPLibrary.Networking.M2Mqtt.Messages;
using UnityEngine;


namespace ExtralityLab
{
    public class MqttClientExampleBidirectional : M2MqttUnityClient
    {
        [Header("Topics Config")]
        public string publishTopicName = "myUnityApp/digital";

        public string subscribedTopic = "myUnityApp/message";

        private List<string> eventMessages = new List<string>();

        protected override void Start()
        {
            // Keep this message below
            base.Start();
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
            Debug.Log($"MQTT: {publishTopicName} connecting to broker on " + brokerAddress + ":" + brokerPort.ToString() + "...\n");
        }

        protected override void OnConnected()
        {
            base.OnConnected();
            Debug.Log($"MQTT: {publishTopicName} connected!");
            PublishInitialTopic();
        }

        protected override void SubscribeTopics()
        {
            client.Subscribe(new string[] { subscribedTopic }, new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE });
        }

        protected override void UnsubscribeTopics()
        {
            client.Unsubscribe(new string[] { subscribedTopic });
        }


        protected override void DecodeMessage(string topic, byte[] message)
        {
            string msg = System.Text.Encoding.UTF8.GetString(message);
            Debug.Log("Received: " + msg);
            StoreMessage(msg);

            if (subscribedTopic == topic)
            {
                // TODO: Decide here on what to do when a message is received
                Debug.Log($"Topic matches: {topic}! Do something with message: {message} ");
            }
        }

        private void StoreMessage(string eventMsg)
        {
            eventMessages.Add(eventMsg);
        }

        private void ProcessMessage(string msg)
        {
            Debug.Log("Received: " + msg);
        }

        private void OnDestroy()
        {
            Disconnect();
        }
        

        ////// CALLBACKS from Buttons
        public void PublishInitialTopic()
        {
            client.Publish(publishTopicName,
                            System.Text.Encoding.UTF8.GetBytes("Connected from Unity..."),
                            MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE,
                            false);

            Debug.Log($"Topic {publishTopicName} published");
        }

        public void PublishTopicValue(string msg)
        {
            client.Publish(publishTopicName,
                            System.Text.Encoding.UTF8.GetBytes(msg),
                            MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE,
                            false);
        }

        public void SubscribeToTopic()
        {
            SubscribeTopics();
        }

        public void UnsubscribeFromTopic()
        {
            UnsubscribeTopics();
        } 
        
    }
}
