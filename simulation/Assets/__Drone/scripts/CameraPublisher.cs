using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;
using RosMessageTypes.Std;
using RosMessageTypes.BuiltinInterfaces;

[RequireComponent(typeof(Camera))]
public class CameraPublisher : MonoBehaviour
{
    private ROSConnection ros;
    private Camera droneCamera;

    [Header("ROS Topic")]
    public string imageTopic = "drone/image_raw";

    [Header("Publish Settings")]
    public int width = 640;
    public int height = 480;
    public int fps = 30;

    private float timer = 0f;
    private float publishInterval;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        droneCamera = GetComponent<Camera>();
        publishInterval = 1f / fps;
        ros.RegisterPublisher<ImageMsg>(imageTopic);
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= publishInterval)
        {
            timer = 0f;
            PublishCameraImage();
        }
    }

    void PublishCameraImage()
    {
        // Create a render texture and render the camera view into it
        RenderTexture rt = new RenderTexture(width, height, 24);
        droneCamera.targetTexture = rt;
        Texture2D screenShot = new Texture2D(width, height, TextureFormat.RGB24, false);
        droneCamera.Render();
        RenderTexture.active = rt;
        screenShot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        screenShot.Apply();

        // Convert to byte array (ROS expects RGB8)
        byte[] imageData = screenShot.GetRawTextureData();

        // Cleanup
        droneCamera.targetTexture = null;
        RenderTexture.active = null;
        Destroy(rt);
        Destroy(screenShot);

        // Build Image message
        ImageMsg msg = new ImageMsg
        {
            header = new HeaderMsg
            {
                frame_id = "drone_camera",
                stamp = new TimeMsg
                {
                    sec = (int)Time.time,
                    nanosec = (uint)((Time.time - (int)Time.time) * 1e9)
                }
            },
            height = (uint)height,
            width = (uint)width,
            encoding = "rgb8",
            step = (uint)(width * 3), // 3 bytes per pixel (RGB)
            data = imageData
        };

        ros.Publish(imageTopic, msg);
    }
}