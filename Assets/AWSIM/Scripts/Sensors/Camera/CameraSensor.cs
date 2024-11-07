using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Profiling;
//using UnityEditor.PackageManager.Requests;
using UnityEngine.PlayerLoop;
using System.Threading;

namespace AWSIM
{
    /// <summary>
    /// Camera Sensor.
    /// Apply OpenCV distortion and encode to bgr8 format. Use ComputeShader.
    /// </summary>
    public class CameraSensor : MonoBehaviour
    {
        /// <summary>
        /// Camera Parameters
        /// fx, fy = focal length.
        /// cx, cy = camera principal point.
        /// k1, k2, k3, p1, p2 = intrinsic camera parameters.
        /// </summary>
        /// <href>https://docs.opencv.org/4.5.1/dc/dbb/tutorial_py_calibration.html</href>
        [System.Serializable]
        public class CameraParameters
        {
            /// <summary>
            /// Image height.
            /// </summary>
            [Range(256, 2048)] public int width = 1280;

            /// <summary>
            /// Image width.
            /// </summary>
            [Range(256, 2048)] public int height = 720;

            /// <summary>
            /// Focal lengths in pixels
            /// </summary>
            [HideInInspector] public float fx = 0;
            [HideInInspector] public float fy = 0;

            /// <summary>
            /// Principal point in pixels
            /// </summary>
            [HideInInspector] public float cx;
            [HideInInspector] public float cy;

            /// <summary>
            /// Camera distortion coefficients.
            /// For "plumb_bob" model, there are 5 parameters are: (k1, k2, p1, p2, k3).
            /// </summary>
            [Range(-1f, 1f)] public float k1;
            [Range(-1f, 1f)] public float k2;
            [Range(-0.5f, 0.5f)] public float p1;
            [Range(-0.5f, 0.5f)] public float p2;
            [Range(-1f, 1f)] public float k3;

            /// <summary>
            /// Get distortion parameters for "plumb bob" model.
            /// </summary>
            public double[] getDistortionParameters()
            {
                return new double[] { k1, k2, p1, p2, k3 };
            }

            /// <summary>
            /// Get intrinsic camera matrix.
            ///
            /// It projects 3D points in the camera coordinate system to 2D pixel
            /// using focal lengths and principal point.
            ///     [fx  0 cx]
            /// K = [ 0 fy cy]
            ///     [ 0  0  1]
            /// </summary>
            public double[] getCameraMatrix()
            {
                return new double[] {
                    fx, 0, cx,
                    0, fy, cy,
                    0, 0, 1};
            }

            /// <summary>
            /// Get projection matrix.
            ///
            /// For monocular camera Tx = Ty = 0, and P[1:3,1:3] = K
            ///     [fx'  0  cx' Tx]
            /// P = [ 0  fy' cy' Ty]
            ///     [ 0   0   1   0]
            /// </summary>
            public double[] getProjectionMatrix()
            {
                return new double[] {
                    fx, 0, cx, 0,
                    0, fy, cy, 0,
                    0, 0, 1, 0};
            }
        }

        /// <summary>
        /// This data is output from the CameraSensor.
        /// </summary>
        public class OutputData
        {
            /// <summary>
            /// Buffer with image data.
            /// </summary>
            public byte[] imageDataBuffer;

            /// <summary>
            /// Set of the camera parameters.
            /// </summary>
            public CameraParameters cameraParameters;
        }

        [System.Serializable]
        public class ImageOnGui
        {
            /// <summary>
            /// If camera image should be show on GUI
            /// </summary>
            public bool show = true;

            [Range(1, 50)] public uint scale = 1;

            [Range(0, 2048)] public uint xAxis = 0;
            [Range(0, 2048)] public uint yAxis = 0;
        }

        [SerializeField] ImageOnGui imageOnGui = new ImageOnGui();

        [SerializeField] CameraParameters cameraParameters;

        /// <summary>
        /// Delegate used in callbacks.
        /// </summary>
        /// <param name="outputData">Data output for each hz</param>
        public delegate void OnOutputDataDelegate(OutputData outputData);

        /// <summary>
        /// Called each time when data is ready.
        /// </summary>
        public OnOutputDataDelegate OnOutputData;

        /// <summary>
        /// Unity camera object.
        /// </summary>
        [SerializeField] Camera cameraObject;

        [SerializeField] bool enableLensDistortionCorrection = false;
        public bool EnableLensDistortionCorrection
        {
            get => enableLensDistortionCorrection;
            set => enableLensDistortionCorrection = value;
        }
        
        [Range(0.0f, 1.0f)] public float sharpeningStrength = 0.0f;

        RenderTexture targetRenderTexture;
        RenderTexture distortedRenderTexture;
        RenderTexture distortionCorrectionRenderTexture;
        RenderTexture sharpenRenderTexture;

        [SerializeField] ComputeShader distortionShader;
        [SerializeField] ComputeShader rosImageShader;
        [SerializeField] ComputeShader distortionCorrectionShader;
        [SerializeField] ComputeShader sharpenShader;

        int shaderKernelIdx = -1;
        int rosShaderKernelIdx = -1;
        int cameraDistortionCorrectionShaderKernelIdx = -1;
        int sharpenShaderKernelIdx = -1;
        ComputeBuffer computeBuffer;

        OutputData outputData = new OutputData();

        private enum FocalLengthName
        {
            Fx,
            Fy
        }

        private int distortionShaderGroupSizeX;
        private int distortionShaderGroupSizeY;
        private int rosImageShaderGroupSizeX;
        private int distortionCorrectionShaderGroupSizeX;
        private int distortionCorrectionShaderGroupSizeY;
        private int sharpenShaderGroupSizeX;
        private int sharpenShaderGroupSizeY;

        private int bytesPerPixel = 3;

        private CameraSensorHolder multiCameraHolder;

        private CommandBuffer cmd;

        void Start()
        {
            if (cameraObject == null)
            {
                throw new MissingComponentException("No active Camera component found in GameObject.");
            }

            if (distortionShader == null)
            {
                throw new MissingComponentException("No distortion shader specified.");
            }

            if (rosImageShader == null)
            {
                throw new MissingComponentException("No ros image shader specified.");
            }

            if ((cameraParameters.width * cameraParameters.height) % sizeof(uint) != 0)
            {
                throw new ArgumentException($"Image size {cameraParameters.width} x {cameraParameters.height} should be multiply of {sizeof(uint)}");
            }

            sharpenShaderKernelIdx = sharpenShader.FindKernel("SharpenTexture");
            shaderKernelIdx = distortionShader.FindKernel("DistortTexture");
            rosShaderKernelIdx = rosImageShader.FindKernel("RosImageShaderKernel");
            cameraDistortionCorrectionShaderKernelIdx = distortionCorrectionShader.FindKernel("CameraDistortionCorrection");

            // Set camera parameters
            cameraObject.usePhysicalProperties = true;
            UpdateCameraParameters();
            UpdateRenderTexture();
            //UpdateRenderTexture_test();
            ConfigureDistortionShaderBuffers();
            distortionShader.GetKernelThreadGroupSizes(shaderKernelIdx,
                out var distortionShaderThreadsPerGroupX, out var distortionShaderthreadsPerGroupY, out _);
            rosImageShader.GetKernelThreadGroupSizes(rosShaderKernelIdx,
                out var rosImageShaderThreadsPerGroupX, out _ , out _);
            distortionCorrectionShader.GetKernelThreadGroupSizes(cameraDistortionCorrectionShaderKernelIdx,
                out var distortionCorrectionShaderThreadsPerGroupX, out var distortionCorrectionShaderthreadsPerGroupY, out _);
            sharpenShader.GetKernelThreadGroupSizes(sharpenShaderKernelIdx,
                out var sharpenShaderThreadsPerGroupX, out var sharpenShaderthreadsPerGroupY, out _);

            distortionShaderGroupSizeX = ((distortedRenderTexture.width + (int)distortionShaderThreadsPerGroupX - 1) / (int)distortionShaderThreadsPerGroupX);
            distortionShaderGroupSizeY = ((distortedRenderTexture.height + (int)distortionShaderthreadsPerGroupY - 1) / (int)distortionShaderthreadsPerGroupY);
            rosImageShaderGroupSizeX = (((cameraParameters.width * cameraParameters.height) * sizeof(uint)) / ((int)rosImageShaderThreadsPerGroupX * sizeof(uint)));
            distortionCorrectionShaderGroupSizeX = ((distortedRenderTexture.width + (int)distortionCorrectionShaderThreadsPerGroupX - 1) / (int)distortionCorrectionShaderThreadsPerGroupX);
            distortionCorrectionShaderGroupSizeY = ((distortedRenderTexture.height + (int)distortionCorrectionShaderthreadsPerGroupY - 1) / (int)distortionCorrectionShaderthreadsPerGroupY);
            sharpenShaderGroupSizeX = ((sharpenRenderTexture.width + (int)sharpenShaderThreadsPerGroupX - 1) / (int)sharpenShaderThreadsPerGroupX);
            sharpenShaderGroupSizeY = ((sharpenRenderTexture.height + (int)sharpenShaderthreadsPerGroupY - 1) / (int)sharpenShaderthreadsPerGroupY);

            multiCameraHolder = GetComponentInParent<CameraSensorHolder>();

            // コマンドバッファの初期化
            cmd = new CommandBuffer();
            cmd.name = "Render and Execute Shaders";
        }

        public void DoRender()
        {
            // Render Unity Camera
            Profiler.BeginSample("Render Unity Camera");
            cameraObject.Render();
            multiCameraHolder.renderRequestedCount++;
            Profiler.EndSample();

            // Set data to shader
            Profiler.BeginSample("Execute Shader");
            UpdateShaderParameters();
            sharpenShader.SetTexture(sharpenShaderKernelIdx, "_InputTexture", targetRenderTexture);
            sharpenShader.SetTexture(sharpenShaderKernelIdx, "_ResultTexture", sharpenRenderTexture);
            sharpenShader.Dispatch(sharpenShaderKernelIdx, sharpenShaderGroupSizeX, sharpenShaderGroupSizeY, 1);
            distortionShader.SetTexture(shaderKernelIdx, "_InputTexture", sharpenRenderTexture);
            distortionShader.SetTexture(shaderKernelIdx, "_DistortedTexture", distortedRenderTexture);
            distortionShader.Dispatch(shaderKernelIdx, distortionShaderGroupSizeX, distortionShaderGroupSizeY, 1);
            if (enableLensDistortionCorrection) {
                distortionCorrectionShader.SetTexture(cameraDistortionCorrectionShaderKernelIdx, "_InputTexture", distortedRenderTexture);
                distortionCorrectionShader.SetTexture(cameraDistortionCorrectionShaderKernelIdx, "_DistortedTexture", distortionCorrectionRenderTexture);
                distortionCorrectionShader.Dispatch(cameraDistortionCorrectionShaderKernelIdx, distortionCorrectionShaderGroupSizeX, distortionCorrectionShaderGroupSizeY, 1);    
                rosImageShader.SetTexture(rosShaderKernelIdx, "_InputTexture", distortionCorrectionRenderTexture);
            } else {
                rosImageShader.SetTexture(rosShaderKernelIdx, "_InputTexture", distortedRenderTexture);
            }
            rosImageShader.SetBuffer(rosShaderKernelIdx, "_RosImageBuffer", computeBuffer);
            rosImageShader.Dispatch(rosShaderKernelIdx, rosImageShaderGroupSizeX, 1, 1);
            multiCameraHolder.setShaderCount++;

            // Get data from shader
            AsyncGPUReadback.Request(computeBuffer, OnGPUReadbackRequest);
            multiCameraHolder.shaderRequestedCount++;
            Profiler.EndSample();

            // Callback called once the AsyncGPUReadback request is fullfield.
            void OnGPUReadbackRequest(AsyncGPUReadbackRequest request)
            {
                if (request.hasError)
                {
                    Debug.LogWarning("AsyncGPUReadback error");
                    return;
                }
                request.GetData<byte>().CopyTo(outputData.imageDataBuffer);
                multiCameraHolder.shadedCount++;
                Debug.Log("Shaded");
            }

            // Update output data.
            outputData.cameraParameters = cameraParameters;

            // Call registered callback.
            Profiler.BeginSample("ROS2 Publish");
            OnOutputData.Invoke(outputData);
            multiCameraHolder.publishedCount++;
            Profiler.EndSample();
        }

        public void DoRender_Optimized(bool useCommandBuffer)
        {   
            if(useCommandBuffer)
            {
                // コマンドバッファのクリア
                cmd.Clear();

                // Unityカメラのレンダリング
                cmd.BeginSample("Render Unity Camera");
                cameraObject.Render();
                multiCameraHolder.renderRequestedCount++;
                cmd.EndSample("Render Unity Camera");

                // シャープシェーダの実行
                cmd.BeginSample("Execute Sharpen Shader");
                UpdateShaderParameters();
                cmd.SetComputeTextureParam(sharpenShader, sharpenShaderKernelIdx, "_InputTexture", targetRenderTexture);
                cmd.SetComputeTextureParam(sharpenShader, sharpenShaderKernelIdx, "_ResultTexture", sharpenRenderTexture);
                cmd.DispatchCompute(sharpenShader, sharpenShaderKernelIdx, sharpenShaderGroupSizeX, sharpenShaderGroupSizeY, 1);
                cmd.EndSample("Execute Sharpen Shader");

                // 歪みシェーダの実行
                cmd.BeginSample("Execute Distortion Shader");
                cmd.SetComputeTextureParam(distortionShader, shaderKernelIdx, "_InputTexture", sharpenRenderTexture);
                cmd.SetComputeTextureParam(distortionShader, shaderKernelIdx, "_DistortedTexture", distortedRenderTexture);
                cmd.DispatchCompute(distortionShader, shaderKernelIdx, distortionShaderGroupSizeX, distortionShaderGroupSizeY, 1);
                cmd.EndSample("Execute Distortion Shader");

                // ROSイメージシェーダの実行
                cmd.BeginSample("Execute ROS Image Shader");
                if (enableLensDistortionCorrection)
                {
                    cmd.SetComputeTextureParam(distortionCorrectionShader, cameraDistortionCorrectionShaderKernelIdx, "_InputTexture", distortedRenderTexture);
                    cmd.SetComputeTextureParam(distortionCorrectionShader, cameraDistortionCorrectionShaderKernelIdx, "_DistortedTexture", distortionCorrectionRenderTexture);
                    cmd.DispatchCompute(distortionCorrectionShader, cameraDistortionCorrectionShaderKernelIdx, distortionCorrectionShaderGroupSizeX, distortionCorrectionShaderGroupSizeY, 1);
                    cmd.SetComputeTextureParam(rosImageShader, rosShaderKernelIdx, "_InputTexture", distortionCorrectionRenderTexture);
                }
                else
                {
                    cmd.SetComputeTextureParam(rosImageShader, rosShaderKernelIdx, "_InputTexture", distortedRenderTexture);
                }
                cmd.SetComputeBufferParam(rosImageShader, rosShaderKernelIdx, "_RosImageBuffer", computeBuffer);
                cmd.DispatchCompute(rosImageShader, rosShaderKernelIdx, rosImageShaderGroupSizeX, 1, 1);
                multiCameraHolder.setShaderCount++;
                cmd.EndSample("Execute ROS Image Shader");

                // シェーダのデータ取得
                cmd.BeginSample("Get Data from Shader");
                var shaderRequest = AsyncGPUReadback.Request(computeBuffer, OnGPUReadbackRequest_Shader);
                cmd.EndSample("Get Data from Shader");

                // コマンドバッファの実行
                Graphics.ExecuteCommandBuffer(cmd);

                // コルーチンの開始
                StartCoroutine(UpdateRequest(shaderRequest));
                //Thread.Sleep(50);
            }
            else
            {
                // Render Unity Camera
                Profiler.BeginSample("Render Unity Camera");
                cameraObject.Render();
                multiCameraHolder.renderRequestedCount++;
                //var renderRequest = AsyncGPUReadback.Request(targetRenderTexture, 0, TextureFormat.ARGB32, OnGPUReadbackRequest_Render);
                Profiler.EndSample();
                //StartCoroutine(UpdateRequest(renderRequest));

                // Set data to shader
                Profiler.BeginSample("Execute Sharpen Shader");
                UpdateShaderParameters();
                sharpenShader.SetTexture(sharpenShaderKernelIdx, "_InputTexture", targetRenderTexture);
                sharpenShader.SetTexture(sharpenShaderKernelIdx, "_ResultTexture", sharpenRenderTexture);
                sharpenShader.Dispatch(sharpenShaderKernelIdx, sharpenShaderGroupSizeX, sharpenShaderGroupSizeY, 1);
                //var sharpenRequest = AsyncGPUReadback.Request(sharpenRenderTexture, 0, TextureFormat.ARGB32, OnGPUReadbackRequest_Sharpen);
                //GL.Flush();
                Profiler.EndSample();
                //StartCoroutine(UpdateRequest(sharpenRequest));
                Profiler.BeginSample("Execute Distortion Shader");
                distortionShader.SetTexture(shaderKernelIdx, "_InputTexture", sharpenRenderTexture);
                distortionShader.SetTexture(shaderKernelIdx, "_DistortedTexture", distortedRenderTexture);
                distortionShader.Dispatch(shaderKernelIdx, distortionShaderGroupSizeX, distortionShaderGroupSizeY, 1);
                //var distortionRequest = AsyncGPUReadback.Request(distortedRenderTexture, 0, TextureFormat.ARGB32, OnGPUReadbackRequest_Distortion);
                //GL.Flush();
                Profiler.EndSample();
                //StartCoroutine(UpdateRequest(distortionRequest));
                Profiler.BeginSample("Execute ROS Image Shader");
                if (enableLensDistortionCorrection) {
                    distortionCorrectionShader.SetTexture(cameraDistortionCorrectionShaderKernelIdx, "_InputTexture", distortedRenderTexture);
                    distortionCorrectionShader.SetTexture(cameraDistortionCorrectionShaderKernelIdx, "_DistortedTexture", distortionCorrectionRenderTexture);
                    distortionCorrectionShader.Dispatch(cameraDistortionCorrectionShaderKernelIdx, distortionCorrectionShaderGroupSizeX, distortionCorrectionShaderGroupSizeY, 1);  
                    rosImageShader.SetTexture(rosShaderKernelIdx, "_InputTexture", distortionCorrectionRenderTexture);
                } else {
                    rosImageShader.SetTexture(rosShaderKernelIdx, "_InputTexture", distortedRenderTexture);
                }
                rosImageShader.SetBuffer(rosShaderKernelIdx, "_RosImageBuffer", computeBuffer);
                rosImageShader.Dispatch(rosShaderKernelIdx, rosImageShaderGroupSizeX, 1, 1);
                multiCameraHolder.setShaderCount++;

                // Get data from shader
                var shaderRequest = AsyncGPUReadback.Request(computeBuffer, OnGPUReadbackRequest_Shader);
                GL.Flush();
                multiCameraHolder.shaderRequestedCount++;
                Profiler.EndSample();
                StartCoroutine(UpdateRequest(shaderRequest));
                //Thread.Sleep(35);
            }

            // Callback called once the AsyncGPUReadback request is fullfield.
            void OnGPUReadbackRequest_Render(AsyncGPUReadbackRequest request)
            {
                if (request.hasError)
                {
                    Debug.LogWarning("AsyncGPUReadback error");
                    return;
                }
                multiCameraHolder.renderedCount++;
                Debug.Log("Rendered");
            }

            void OnGPUReadbackRequest_Sharpen(AsyncGPUReadbackRequest request)
            {
                if (request.hasError)
                {
                    Debug.LogWarning("AsyncGPUReadback error");
                    return;
                }
                Debug.Log("Sharpened");
            }

            void OnGPUReadbackRequest_Distortion(AsyncGPUReadbackRequest request)
            {
                if (request.hasError)
                {
                    Debug.LogWarning("AsyncGPUReadback error");
                    return;
                }
                Debug.Log("Distorted");
            }

            void OnGPUReadbackRequest_Shader(AsyncGPUReadbackRequest request)
            {
                if (request.hasError)
                {
                    Debug.LogWarning("AsyncGPUReadback error");
                    return;
                }
                request.GetData<byte>().CopyTo(outputData.imageDataBuffer);
                multiCameraHolder.shadedCount++;
                Debug.Log("Shaded");

                // Call registered callback.
                Profiler.BeginSample("ROS2 Publish");
                OnOutputData.Invoke(outputData);
                multiCameraHolder.publishedCount++;
                Profiler.EndSample();
            }

            // Update output data.
            outputData.cameraParameters = cameraParameters;
        }

        public IEnumerator DoRender_test()
        {   
            // Render Unity Camera
            Profiler.BeginSample("Render Unity Camera");
            cameraObject.Render();
            multiCameraHolder.renderRequestedCount++;
            //var renderRequest = AsyncGPUReadback.Request(targetRenderTexture, 0, TextureFormat.ARGB32, OnGPUReadbackRequest_Render);
            Profiler.EndSample();
            //StartCoroutine(UpdateRequest(renderRequest));
            //yield return StartCoroutine(UpdateRequest(renderRequest));

            // Set data to shader
            Profiler.BeginSample("Execute Sharpen Shader");
            UpdateShaderParameters();
            sharpenShader.SetTexture(sharpenShaderKernelIdx, "_InputTexture", targetRenderTexture);
            sharpenShader.SetTexture(sharpenShaderKernelIdx, "_ResultTexture", sharpenRenderTexture);
            sharpenShader.Dispatch(sharpenShaderKernelIdx, sharpenShaderGroupSizeX, sharpenShaderGroupSizeY, 1);
            //var sharpenRequest = AsyncGPUReadback.Request(sharpenRenderTexture, 0, TextureFormat.ARGB32, OnGPUReadbackRequest_Sharpen);
            //GL.Flush();
            Profiler.EndSample();
            //yield return StartCoroutine(UpdateRequest(sharpenRequest));
            Profiler.BeginSample("Execute Distortion Shader");
            distortionShader.SetTexture(shaderKernelIdx, "_InputTexture", sharpenRenderTexture);
            distortionShader.SetTexture(shaderKernelIdx, "_DistortedTexture", distortedRenderTexture);
            distortionShader.Dispatch(shaderKernelIdx, distortionShaderGroupSizeX, distortionShaderGroupSizeY, 1);
            //var distortionRequest = AsyncGPUReadback.Request(distortedRenderTexture, 0, TextureFormat.ARGB32, OnGPUReadbackRequest_Distortion);
            //GL.Flush();
            Profiler.EndSample();
            //yield return StartCoroutine(UpdateRequest(distortionRequest));
            Profiler.BeginSample("Execute ROS Image Shader");
            if (enableLensDistortionCorrection) {
                distortionCorrectionShader.SetTexture(cameraDistortionCorrectionShaderKernelIdx, "_InputTexture", distortedRenderTexture);
                distortionCorrectionShader.SetTexture(cameraDistortionCorrectionShaderKernelIdx, "_DistortedTexture", distortionCorrectionRenderTexture);
                distortionCorrectionShader.Dispatch(cameraDistortionCorrectionShaderKernelIdx, distortionCorrectionShaderGroupSizeX, distortionCorrectionShaderGroupSizeY, 1);  
                rosImageShader.SetTexture(rosShaderKernelIdx, "_InputTexture", distortionCorrectionRenderTexture);
            } else {
                rosImageShader.SetTexture(rosShaderKernelIdx, "_InputTexture", distortedRenderTexture);
            }
            rosImageShader.SetBuffer(rosShaderKernelIdx, "_RosImageBuffer", computeBuffer);
            rosImageShader.Dispatch(rosShaderKernelIdx, rosImageShaderGroupSizeX, 1, 1);
            multiCameraHolder.setShaderCount++;

            // Get data from shader
            var shaderRequest = AsyncGPUReadback.Request(computeBuffer, OnGPUReadbackRequest_Shader);
            //GL.Flush();
            multiCameraHolder.shaderRequestedCount++;
            Profiler.EndSample();
            //StartCoroutine(UpdateRequest(shaderRequest));
            //yield return StartCoroutine(UpdateRequest(shaderRequest));

            // Callback called once the AsyncGPUReadback request is fullfield.
            void OnGPUReadbackRequest_Render(AsyncGPUReadbackRequest request)
            {
                if (request.hasError)
                {
                    Debug.LogWarning("AsyncGPUReadback error");
                    return;
                }
                multiCameraHolder.renderedCount++;
                Debug.Log("Rendered");
            }

            void OnGPUReadbackRequest_Sharpen(AsyncGPUReadbackRequest request)
            {
                if (request.hasError)
                {
                    Debug.LogWarning("AsyncGPUReadback error");
                    return;
                }
                Debug.Log("Sharpened");
            }

            void OnGPUReadbackRequest_Distortion(AsyncGPUReadbackRequest request)
            {
                if (request.hasError)
                {
                    Debug.LogWarning("AsyncGPUReadback error");
                    return;
                }
                Debug.Log("Distorted");
            }

            void OnGPUReadbackRequest_Shader(AsyncGPUReadbackRequest request)
            {
                if (request.hasError)
                {
                    Debug.LogWarning("AsyncGPUReadback error");
                    return;
                }
                request.GetData<byte>().CopyTo(outputData.imageDataBuffer);
                multiCameraHolder.shadedCount++;
                Debug.Log("Shaded");
            }

            // Update output data.
            outputData.cameraParameters = cameraParameters;

            // Call registered callback.
            Profiler.BeginSample("ROS2 Publish");
            OnOutputData.Invoke(outputData);
            multiCameraHolder.publishedCount++;
            Profiler.EndSample();

            yield return null;
        }

        private IEnumerator UpdateRequest(AsyncGPUReadbackRequest request)
        {
            while(!request.done)
            {
                request.Update();
                yield return new WaitForFixedUpdate();
            }
        }

        private void OnDestroy()
        {
            computeBuffer.Release();
            cmd.Release();
        }

        private void ConfigureDistortionShaderBuffers()
        {
            // RosImageShader translates Texture2D to ROS image by encoding two pixels color (bgr8 -> 3 bytes) into one uint32 (4 bytes).
            var rosImageBufferSize = (cameraParameters.width * cameraParameters.height * bytesPerPixel) / sizeof(uint);
            if (computeBuffer == null || computeBuffer.count != rosImageBufferSize)
            {
                computeBuffer = new ComputeBuffer(rosImageBufferSize, sizeof(uint));
                outputData.imageDataBuffer = new byte[cameraParameters.width * cameraParameters.height * bytesPerPixel];
            }
        }

        void OnGUI()
        {
            if (imageOnGui.show)
            {
                if (enableLensDistortionCorrection) {
                    DrawTextureOnGUI(distortionCorrectionRenderTexture);
                } else {
                    DrawTextureOnGUI(distortedRenderTexture);
                    //DrawTextureOnGUI_test(targetRenderTexture);
                }
            }
        }

        void DrawTextureOnGUI(RenderTexture texture)
        {
            GUI.DrawTexture(new Rect(imageOnGui.xAxis, imageOnGui.yAxis,
                texture.width / imageOnGui.scale, texture.height / imageOnGui.scale), texture);
        }

        void DrawTextureOnGUI_test(RenderTexture texture)
        {
            GUI.DrawTexture(new Rect(imageOnGui.xAxis + 360, imageOnGui.yAxis,
                texture.width / imageOnGui.scale, texture.height / imageOnGui.scale), texture);
        }

        private bool FloatEqual(float value1, float value2, float epsilon = 0.001f)
        {
            return Math.Abs(value1 - value2) < epsilon;
        }

        private void VerifyFocalLengthInPixels(ref float focalLengthInPixels, float imageSize, float sensorSize, FocalLengthName focalLengthInPixelsName)
        {
            var computedFocalLengthInPixels = imageSize / sensorSize * cameraObject.focalLength;
            if (focalLengthInPixels == 0.0)
            {
                focalLengthInPixels = computedFocalLengthInPixels;
            }
            else if (!FloatEqual(focalLengthInPixels, computedFocalLengthInPixels))
            {
                Debug.LogWarning("The <" + focalLengthInPixelsName + "> [" + focalLengthInPixels +
                "] you have provided for camera is inconsistent with specified <imageSize> [" + imageSize +
                "] and sensorSize [" + sensorSize + "]. Please double check to see that " + focalLengthInPixelsName + " = imageSize " +
                " / sensorSize * focalLength. The expected " + focalLengthInPixelsName + " value is [" + computedFocalLengthInPixels +
                "], please update your camera model description accordingly. Set " + focalLengthInPixelsName + " to 0 to calculate it automatically");
            }
        }

        private void UpdateRenderTexture()
        {
            targetRenderTexture = new RenderTexture(
                cameraParameters.width, cameraParameters.height, 32, RenderTextureFormat.BGRA32);

            distortedRenderTexture = new RenderTexture(
                cameraParameters.width, cameraParameters.height, 24, RenderTextureFormat.BGRA32, RenderTextureReadWrite.sRGB)
            {
                dimension = TextureDimension.Tex2D,
                antiAliasing = 1,
                useMipMap = false,
                useDynamicScale = false,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                enableRandomWrite = true
            };

            distortionCorrectionRenderTexture = new RenderTexture(
                cameraParameters.width, cameraParameters.height, 24, RenderTextureFormat.BGRA32, RenderTextureReadWrite.sRGB)
            {
                dimension = TextureDimension.Tex2D,
                antiAliasing = 1,
                useMipMap = false,
                useDynamicScale = false,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                enableRandomWrite = true
            };

            sharpenRenderTexture = new RenderTexture(
                cameraParameters.width, cameraParameters.height, 24, RenderTextureFormat.BGRA32, RenderTextureReadWrite.sRGB)
            {
                dimension = TextureDimension.Tex2D,
                antiAliasing = 1,
                useMipMap = false,
                useDynamicScale = false,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                enableRandomWrite = true
            };

            distortedRenderTexture.Create();
            distortionCorrectionRenderTexture.Create();
            sharpenRenderTexture.Create();

            cameraObject.targetTexture = targetRenderTexture;
        }

        private void UpdateRenderTexture_test()
        {
            targetRenderTexture = new RenderTexture(
                cameraParameters.width, cameraParameters.height, 32, RenderTextureFormat.ARGB32);

            distortedRenderTexture = new RenderTexture(
                cameraParameters.width, cameraParameters.height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                dimension = TextureDimension.Tex2D,
                antiAliasing = 1,
                useMipMap = false,
                useDynamicScale = false,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                enableRandomWrite = true
            };

            distortionCorrectionRenderTexture = new RenderTexture(
                cameraParameters.width, cameraParameters.height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                dimension = TextureDimension.Tex2D,
                antiAliasing = 1,
                useMipMap = false,
                useDynamicScale = false,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                enableRandomWrite = true
            };

            sharpenRenderTexture = new RenderTexture(
                cameraParameters.width, cameraParameters.height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                dimension = TextureDimension.Tex2D,
                antiAliasing = 1,
                useMipMap = false,
                useDynamicScale = false,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                enableRandomWrite = true
            };

            distortedRenderTexture.Create();
            distortionCorrectionRenderTexture.Create();
            sharpenRenderTexture.Create();

            cameraObject.targetTexture = targetRenderTexture;
        }

        private void UpdateShaderParameters()
        {
            distortionShader.SetInt("_width", cameraParameters.width);
            distortionShader.SetInt("_height", cameraParameters.height);
            distortionShader.SetFloat("_fx", cameraParameters.fx);
            distortionShader.SetFloat("_fy", cameraParameters.fy);
            distortionShader.SetFloat("_cx", cameraParameters.cx);
            distortionShader.SetFloat("_cy", cameraParameters.cy);
            distortionShader.SetFloat("_k1", -cameraParameters.k1); // TODO: Find out why 'minus' is needed for proper distortion
            distortionShader.SetFloat("_k2", -cameraParameters.k2); // TODO: Find out why 'minus' is needed for proper distortion
            distortionShader.SetFloat("_p1", cameraParameters.p1);
            distortionShader.SetFloat("_p2", -cameraParameters.p2); // TODO: Find out why 'minus' is needed for proper distortion
            distortionShader.SetFloat("_k3", -cameraParameters.k3); // TODO: Find out why 'minus' is needed for proper distortion

            distortionCorrectionShader.SetInt("_width", cameraParameters.width);
            distortionCorrectionShader.SetInt("_height", cameraParameters.height);
            distortionCorrectionShader.SetFloat("_fx", cameraParameters.fx);
            distortionCorrectionShader.SetFloat("_fy", cameraParameters.fy);
            distortionCorrectionShader.SetFloat("_cx", cameraParameters.cx);
            distortionCorrectionShader.SetFloat("_cy", cameraParameters.cy);
            distortionCorrectionShader.SetFloat("_k1", -cameraParameters.k1); // TODO: Find out why 'minus' is needed for proper distortion
            distortionCorrectionShader.SetFloat("_k2", -cameraParameters.k2); // TODO: Find out why 'minus' is needed for proper distortion
            distortionCorrectionShader.SetFloat("_p1", cameraParameters.p1);
            distortionCorrectionShader.SetFloat("_p2", -cameraParameters.p2); // TODO: Find out why 'minus' is needed for proper distortion
            distortionCorrectionShader.SetFloat("_k3", -cameraParameters.k3); // TODO: Find out why 'minus' is needed for proper distortion

            sharpenShader.SetFloat("_sharpeningStrength", sharpeningStrength);

            rosImageShader.SetInt("_width", cameraParameters.width);
            rosImageShader.SetInt("_height", cameraParameters.height);
        }

        private void UpdateCameraParameters()
        {
            VerifyFocalLengthInPixels(ref cameraParameters.fx, cameraParameters.width, cameraObject.sensorSize.x, FocalLengthName.Fx);
            VerifyFocalLengthInPixels(ref cameraParameters.fy, cameraParameters.height, cameraObject.sensorSize.y, FocalLengthName.Fy);
            cameraParameters.cx = ((cameraParameters.width + 1) / 2.0f);
            cameraParameters.cy = ((cameraParameters.height + 1) / 2.0f);
        }
    }
}