using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.MessageGeneration;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using RosMessageTypes.Sensor;
using RosMessageTypes.Std;
using RosMessageTypes.Geometry;
using RosMessageTypes.BuiltinInterfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

public class RGBCamera : MonoBehaviour
{
    public Camera rgbCamera;

    public int width = 1280;
    public int height = 720;
    public float vFov = 70f;
    public float depthMin = 0.3f;
    public float depthMax = 50f;
    public int fps = 15;

    public string rgbTopic = "/rgb_camera/image_raw";
    public string cameraInfoTopic = "/rgb_camera/camera_info";
    public string opticalFrameId = "rgb_camera_optical";

    private bool usePersistentBuffer = true;

    ROSConnection ros;
    RenderTexture colorRT;
    NativeArray<byte> colorBuf;
    byte[] rgbBuffer;
    CameraInfoMsg camInfo;
    CancellationTokenSource cts;
    float lastTime = 0f, interval;
    bool isCleaning = false;
    bool isInitialized = false;

    AsyncGPUReadbackRequest readbackReq;
    bool readbackInFlight = false;

    void Start()
    {
        if (isInitialized) return;
        isInitialized = true;

        interval = Mathf.Max(0.001f, 1f / Mathf.Max(1, fps));
        SetupROS();
        SetupCamera();
        SetupBuffers();
        SetupIntrinsics();

        cts = new CancellationTokenSource();
        _ = CaptureLoopAsync();
    }

    void SetupROS()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<ImageMsg>(rgbTopic);
        ros.RegisterPublisher<CameraInfoMsg>(cameraInfoTopic);
    }

    void SetupCamera()
    {
        if (rgbCamera == null)
        {
            rgbCamera = GetComponent<Camera>();
            if (rgbCamera == null)
            {
                Debug.LogError("RGBCamera: No camera component found!");
                return;
            }
        }

        rgbCamera.transform.localPosition = Vector3.zero;
        rgbCamera.fieldOfView = vFov;
        rgbCamera.nearClipPlane = depthMin;
        rgbCamera.farClipPlane = depthMax;

        rgbCamera.enabled = false;

        colorRT = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
        {
            antiAliasing = 1,
            enableRandomWrite = false
        };
        colorRT.Create();

        rgbBuffer = new byte[width * height * 3];
    }

    void SetupBuffers()
    {
        if (!usePersistentBuffer)
            Debug.LogWarning("RGBCamera: usePersistentBuffer=false is not supported for AsyncGPUReadback. Forcing Persistent allocation.");

        int needed = width * height * 4;
        if (colorBuf.IsCreated && colorBuf.Length != needed)
        {
            colorBuf.Dispose();
        }
        if (!colorBuf.IsCreated)
        {
            colorBuf = new NativeArray<byte>(needed, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        }
    }

    void EnsurePersistentBufferSize()
    {
        int needed = width * height * 4;
        if (!colorBuf.IsCreated || colorBuf.Length != needed)
        {
            if (colorBuf.IsCreated) colorBuf.Dispose();
            colorBuf = new NativeArray<byte>(needed, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        }

        if (rgbBuffer == null || rgbBuffer.Length != width * height * 3)
            rgbBuffer = new byte[width * height * 3];
    }

    void SetupIntrinsics()
    {
        float fovY = vFov * Mathf.Deg2Rad;
        float fy = height / (2f * Mathf.Tan(fovY * 0.5f));
        float fx = fy;
        float cx = (width - 1) * 0.5f;
        float cy = (height - 1) * 0.5f;

        camInfo = new CameraInfoMsg
        {
            header = new HeaderMsg { frame_id = opticalFrameId },
            height = (uint)height,
            width = (uint)width,
            distortion_model = "plumb_bob",
            k = new double[] { fx, 0, cx, 0, fy, cy, 0, 0, 1 },
            d = new double[5],
            r = new double[] { 1, 0, 0, 0, 1, 0, 0, 0, 1 },
            p = new double[] { fx, 0, cx, 0, 0, fy, cy, 0, 0, 0, 1, 0 },
            binning_x = 0,
            binning_y = 0,
            roi = new RegionOfInterestMsg()
        };
    }

    async Task CaptureLoopAsync()
    {
        var token = cts.Token;
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Awaitable.EndOfFrameAsync(token);

                if (Time.time - lastTime < interval) continue;
                lastTime = Time.time;

                await RenderAndRead(token);
                PublishFrame();
            }
        }
        catch (OperationCanceledException) {}
        catch (Exception e)
        {
            Debug.LogWarning($"RGBCamera capture loop error: {e}");
        }
        finally
        {
            CleanupResources();
        }
    }

    async Task RenderAndRead(CancellationToken token)
    {
        if (rgbCamera == null || colorRT == null) return;

        EnsurePersistentBufferSize();

        rgbCamera.targetTexture = colorRT;
        rgbCamera.Render();
        rgbCamera.targetTexture = null;

        if (readbackInFlight && readbackReq.done == false)
        {
            while (!readbackReq.done)
                await Awaitable.NextFrameAsync(token);
        }
        readbackInFlight = false;

        readbackReq = AsyncGPUReadback.RequestIntoNativeArray(ref colorBuf, colorRT, 0, TextureFormat.RGBA32);

        readbackInFlight = true;
        while (!readbackReq.done)
            await Awaitable.NextFrameAsync(token);

        if (readbackReq.hasError)
        {
            Debug.LogWarning("RGBCamera: GPU readback error.");
            return;
        }

        unsafe
        {
            byte* ptr = (byte*)colorBuf.GetUnsafeReadOnlyPtr();
            int w = width, h = height;

            for (int y = 0; y < h; y++)
            {
                int srcY = (h - 1 - y); // flip
                int srcRow = srcY * w * 4;
                int dstRow = y * w * 3;

                for (int x = 0; x < w; x++)
                {
                    int si = srcRow + x * 4;
                    int di = dstRow + x * 3;
                    rgbBuffer[di + 0] = ptr[si + 0]; // R
                    rgbBuffer[di + 1] = ptr[si + 1]; // G
                    rgbBuffer[di + 2] = ptr[si + 2]; // B
                }
            }
        }

        readbackInFlight = false;
    }

    void PublishFrame()
    {
        var now = Time.time;
        var stamp = new TimeMsg
        {
            sec = (int)Mathf.Floor(now),
            nanosec = (uint)((now - Mathf.Floor(now)) * 1e9f)
        };

        camInfo.header.stamp = stamp;

        var img = new ImageMsg
        {
            header = new HeaderMsg { frame_id = opticalFrameId, stamp = stamp },
            height = (uint)height,
            width = (uint)width,
            encoding = "rgb8",
            is_bigendian = 0,
            step = (uint)(width * 3),
            data = rgbBuffer
        };

        ros.Publish(rgbTopic, img);
        ros.Publish(cameraInfoTopic, camInfo);
    }

    void EnsureReadbackFinished()
    {
        if (readbackInFlight && readbackReq.done == false)
        {
            readbackReq.WaitForCompletion();
        }
        readbackInFlight = false;
    }

    void CleanupResources()
    {
        if (isCleaning) return;
        isCleaning = true;

        if (cts != null)
        {
            try { cts.Cancel(); } catch { }
            try { cts.Dispose(); } catch { }
            cts = null;
        }

        EnsureReadbackFinished();
        if (colorBuf.IsCreated)
        {
            try { colorBuf.Dispose(); } catch { }
            colorBuf = default;
        }

        if (colorRT != null)
        {
            try
            {
                colorRT.Release();
                DestroyImmediate(colorRT);
            }
            catch { }
            colorRT = null;
        }
    }

    void OnDisable() => CleanupResources();
    void OnDestroy() => CleanupResources();
    void OnApplicationQuit() => CleanupResources();
}
