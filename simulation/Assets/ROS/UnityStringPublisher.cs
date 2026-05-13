using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Std;   // <- prebuilt

public class UnityStringPublisher : MonoBehaviour
{
    ROSConnection ros;
    public string topicName = "/unity_chatter";

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<StringMsg>(topicName);
        Debug.Log("Publisher registered");
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ros.Publish(topicName, new StringMsg("Hello from Unity"));
            Debug.Log("Message sent");
        }
    }
}
