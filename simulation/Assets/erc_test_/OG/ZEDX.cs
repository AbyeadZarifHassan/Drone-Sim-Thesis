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

public class ZEDX : MonoBehaviour
{
    public Camera leftCamera;

    public bool enableImageCapture = true;
    public bool enableDepthCapture = false;
    public bool enablePointCloud = false;

    public int width = 1280;
    public int height = 720;
    public float vFov = 70f;
    public float depthMin = 0.3f;
    public float depthMax = 50f;
    public int fps = 15;

    public string rgbLeftTopic        = "/zedx/left/image_raw";
    public string depthLeftTopic      = "/zedx/left/depth_raw";
    public string cameraInfoLeftTopic = "/zedx/left/camera_info";
    public string pointCloudTopic     = "/zedx/points";
    public string opticalFrameId             = "camera_link_optical";
    
    // internals
    ROSConnection ros;
    RenderTexture  lColorRT, lDepthRT;
    NativeArray<byte>  lColorBuf;
    NativeArray<float> lDepthBuf;
    Material depthMat;
    CameraInfoMsg camInfoL;
    CancellationTokenSource cts;
    float lastTime = 0f, interval;
    bool isCleaning = false;

    // performance buffers
    byte[] rgbBuffer;
    byte[] depthBuffer;
    byte[] pcBuffer;
    float fxConst;

    void Start()
    {
        interval = 1f / fps;
        SetupROS();
        SetupCameras();
        SetupBuffers();
        SetupIntrinsics();
        cts = new CancellationTokenSource();
        _ = CaptureLoopAsync();
    }

    void SetupROS()
    {
        ros = ROSConnection.GetOrCreateInstance();
        
        if (enableImageCapture)
        {
            ros.RegisterPublisher<ImageMsg>(rgbLeftTopic);
            ros.RegisterPublisher<CameraInfoMsg>(cameraInfoLeftTopic);
        }
        
        if (enableDepthCapture)
        {
            ros.RegisterPublisher<ImageMsg>(depthLeftTopic);
        }
        
        if (enablePointCloud)
        {
            ros.RegisterPublisher<PointCloud2Msg>(pointCloudTopic);
        }
    }

    void SetupCameras()
    {
        leftCamera.transform.localPosition = Vector3.zero;
        leftCamera.fieldOfView   = vFov;
        leftCamera.nearClipPlane = depthMin;
        leftCamera.farClipPlane  = depthMax;
        
        if (enableDepthCapture || enablePointCloud)
            leftCamera.depthTextureMode = DepthTextureMode.Depth;
        
        // Prevent automatic rendering but keep camera component active for manual rendering
        leftCamera.enabled = false;

        if (enableImageCapture)
        {
            lColorRT = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            lColorRT.Create();
        }

        if (enableDepthCapture || enablePointCloud)
        {
            lDepthRT = new RenderTexture(width, height, 0, RenderTextureFormat.RFloat) { enableRandomWrite = true };
            lDepthRT.Create();

            depthMat = new Material(Shader.Find("Custom/DepthTextureShader"));
            depthMat.SetFloat("_DepthMin", depthMin);
            depthMat.SetFloat("_DepthMax", depthMax);
        }
    }

    void SetupBuffers()
    {
        if (enableImageCapture)
        {
            lColorBuf = new NativeArray<byte>(width * height * 4, Allocator.Persistent);
            rgbBuffer = new byte[width * height * 3];
        }

        if (enableDepthCapture || enablePointCloud)
        {
            lDepthBuf = new NativeArray<float>(width * height, Allocator.Persistent);
        }

        if (enableDepthCapture)
        {
            depthBuffer = new byte[width * height * 2];
        }

        if (enablePointCloud)
        {
            pcBuffer = new byte[width * height * 16];
        }
    }

    void SetupIntrinsics()
    {
        float fovY   = vFov * Mathf.Deg2Rad;
        float fy     = height / (2f * Mathf.Tan(fovY / 2f));
        float fx     = fy;
        float cx     = (width  - 1) / 2f;
        float cy     = (height - 1) / 2f;

        if (enableImageCapture)
        {
            camInfoL = new CameraInfoMsg
            {
                header = new HeaderMsg { frame_id = opticalFrameId },
                height = (uint)height,
                width  = (uint)width,
                distortion_model = "plumb_bob",
                k = new double[]{ fx,0,cx, 0,fy,cy, 0,0,1 },
                d = new double[5],
                r = new double[]{1,0,0, 0,1,0, 0,0,1},
                p = new double[]{ fx,0,cx, 0,0,fy,cy, 0,0,0,1,0 },
                binning_x = 0,
                binning_y = 0,
                roi = new RegionOfInterestMsg()
            };
        }

        fxConst = fx;
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

                await RenderAndRead(leftCamera, token);
                PublishFrame();
            }
        }
        catch (OperationCanceledException)
        {
            // expected on stop
        }
        catch (Exception e)
        {
            Debug.LogWarning($"ZEDX capture loop error: {e}");
        }
        finally
        {
            CleanupResources();
        }
    }

    async Task RenderAndRead(Camera cam, CancellationToken token)
    {
        // Temporarily enable the camera for rendering
        bool wasEnabled = cam.enabled;
        cam.enabled = true;
        
        // Handle image capture
        if (enableImageCapture)
        {
            cam.targetTexture = lColorRT;
            cam.Render();
            var creq = AsyncGPUReadback.RequestIntoNativeArray(ref lColorBuf, lColorRT, 0, TextureFormat.RGBA32);
            while (!creq.done) await Awaitable.NextFrameAsync(token);
        }

        // Handle depth capture (only if needed)
        if (enableDepthCapture || enablePointCloud)
        {
            // Now render depth by setting up a temporary depth buffer
            RenderTexture tempDepthRT = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.Depth);
            
            // Render again to get fresh depth data
            cam.targetTexture = tempDepthRT;
            cam.Render();
            
            // Now blit the depth using our shader, passing the specific depth texture
            depthMat.SetTexture("_MainTex", tempDepthRT);
            Graphics.Blit(tempDepthRT, lDepthRT, depthMat);
            
            // Clean up
            RenderTexture.ReleaseTemporary(tempDepthRT);
            
            var dreq = AsyncGPUReadback.RequestIntoNativeArray(ref lDepthBuf, lDepthRT, 0, TextureFormat.RFloat);
            while (!dreq.done) await Awaitable.NextFrameAsync(token);
        }
        
        cam.targetTexture = null;
        cam.enabled = wasEnabled;
    }

    void PublishFrame()
    {
        var stamp = new TimeMsg
        {
            sec    = (int)Time.time,
            nanosec= (uint)((Time.time - Mathf.Floor(Time.time)) * 1e9f)
        };

        // Publish image data
        if (enableImageCapture)
        {
            camInfoL.header.stamp = stamp;
            var rgbL = BuildImageMsg(lColorBuf, width, height, stamp, opticalFrameId, "rgb8", 3);
            
            ros.Publish(rgbLeftTopic, rgbL);
            ros.Publish(cameraInfoLeftTopic, camInfoL);
        }

        // Publish depth data
        if (enableDepthCapture)
        {
            var depL = BuildImageMsg(lDepthBuf, width, height, stamp, opticalFrameId, "16UC1", 2, true);
            ros.Publish(depthLeftTopic, depL);
        }

        // Publish point cloud
        if (enablePointCloud)
        {
            var pc2 = BuildPointCloud2(lDepthBuf, lColorBuf, width, height, stamp);
            ros.Publish(pointCloudTopic, pc2);
        }
    }

    ImageMsg BuildImageMsg<T>(
        NativeArray<T> buf,
        int w, int h,
        TimeMsg stamp,
        string fid,
        string encoding,
        int chan,
        bool depth = false
    ) where T : struct
    {
        byte[] data = depth ? depthBuffer : rgbBuffer;
        uint step   = (uint)(w * chan);

        unsafe
        {
            if (!depth)
            {
                byte* ptr = (byte*)buf.GetUnsafeReadOnlyPtr();
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int si = ((h - 1 - y) * w + x) * 4;  // FLIP Y for ROS
                        int di = (y * w + x) * 3;
                        data[di+0] = ptr[si+0];  // R
                        data[di+1] = ptr[si+1];  // G
                        data[di+2] = ptr[si+2];  // B
                    }
            }
            else
            {
                float* ptrf = (float*)buf.GetUnsafeReadOnlyPtr();
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float m = ptrf[(h - 1 - y) * w + x];  // FLIP Y for ROS
                        ushort mm = (ushort)Mathf.Clamp(m * 1000f, 1, 65535);
                        int di = (y * w + x) * 2;
                        data[di+0] = (byte)(mm & 0xFF);
                        data[di+1] = (byte)(mm >> 8);
                    }
            }
        }

        return new ImageMsg
        {
            header      = new HeaderMsg { frame_id = fid, stamp = stamp },
            height      = (uint)h,
            width       = (uint)w,
            encoding    = encoding,
            is_bigendian= 0,
            step        = step,
            data        = data
        };
    }

    PointCloud2Msg BuildPointCloud2(
        NativeArray<float> dbuf,
        NativeArray<byte>  cbuf,
        int w, int h,
        TimeMsg stamp)
    {
        int np = w * h;
        int pointStep = 16;

        unsafe
        {
            float* dp  = (float*)dbuf.GetUnsafeReadOnlyPtr();
            byte*  cpb = cbuf.IsCreated ? (byte*)cbuf.GetUnsafeReadOnlyPtr() : null;
            fixed (byte* outp = pcBuffer)
            {
                byte* op = outp;
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int src = y * w + x;  // NO Y-flip for point cloud - use original Unity coordinates
                        float z = dp[src];
                        
                        // Keep Unity camera coordinates - TF will handle the conversion
                        float unityX = (x - (w - 1) * 0.5f) * z / fxConst;
                        float unityY = ((h - 1) * 0.5f - y) * z / fxConst;
                        float unityZ = z;

                        uint rgb = 0xFFFFFF; // Default white if no color data
                        if (cpb != null && enableImageCapture)
                        {
                            uint r = cpb[src*4 +0], g = cpb[src*4+1], b = cpb[src*4+2];
                            rgb = (r<<16)|(g<<8)|b;
                        }

                        *(float*)(op +  0) = unityX;  // Keep Unity coordinates
                        *(float*)(op +  4) = unityY;
                        *(float*)(op +  8) = unityZ;
                        *(float*)(op + 12) = *(float*)&rgb;
                        op += 16;
                    }
                }
            }
        }

        var fields = new PointFieldMsg[]{
            new PointFieldMsg("x",  0, PointFieldMsg.FLOAT32, 1),
            new PointFieldMsg("y",  4, PointFieldMsg.FLOAT32, 1),
            new PointFieldMsg("z",  8, PointFieldMsg.FLOAT32, 1),
            new PointFieldMsg("rgb",12, PointFieldMsg.FLOAT32, 1)
        };

        return new PointCloud2Msg
        {
            header      = new HeaderMsg { frame_id = opticalFrameId, stamp = stamp },
            height      = 1,
            width       = (uint)np,
            fields      = fields,
            is_bigendian= false,
            point_step  = (uint)pointStep,
            row_step    = (uint)(pointStep * np),
            data        = pcBuffer,
            is_dense    = true
        };
    }

    void CleanupResources()
    {
        if (isCleaning) return;
        isCleaning = true;

        cts?.Cancel();
        cts?.Dispose();
        cts = null;

        try { if (lColorBuf.IsCreated) lColorBuf.Dispose(); } catch { }
        try { if (lDepthBuf.IsCreated) lDepthBuf.Dispose(); } catch { }

        if (lColorRT  != null) { lColorRT.Release();  DestroyImmediate(lColorRT);  lColorRT = null; }
        if (lDepthRT  != null) { lDepthRT.Release();  DestroyImmediate(lDepthRT);  lDepthRT = null; }
        if (depthMat  != null) { DestroyImmediate(depthMat); depthMat = null; }
    }

    void OnDisable()        => CleanupResources();
    void OnDestroy()        => CleanupResources();
    void OnApplicationQuit() => CleanupResources();
}