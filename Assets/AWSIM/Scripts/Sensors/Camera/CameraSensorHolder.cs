using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using UnityEngine.Profiling;
using System;
using UnityEngine.Rendering;

namespace AWSIM
{
    /// <summary>
    /// CameraSensorHolder.
    /// Controls the rendering sequence of multiple camera sensors.
    /// </summary>
    public class CameraSensorHolder : MonoBehaviour
    {
        [Header("Camera Sensors")]
        [SerializeField] private List<CameraSensor> cameraSensors = default;

        [Header("Parameters")]

        /// <summary>
        /// Data output hz.
        /// Sensor processing and callbacks are called in this hz.
        /// </summary>
        [Range(0, 30)][SerializeField] private uint publishHz = 10;

        /// <summary>
        /// Rendering sequence type.
        /// Set True for sensors render at different frames one after another.
        /// Set False for all sensors render at the same frame.
        /// </summary>
        [SerializeField] private bool renderInQueue = true;

        [SerializeField] private int processType = 0;

        [SerializeField] private bool useCommandBuffer = false;

        float timer = 0;
        bool timerStart = false;
        bool publishFlag = false;

        List<Coroutine> renderCoroutines;
        List<Task> renderTasks;

        int shouldPublishCount = 0;
        [NonSerialized] public int renderedCount = 0;
        [NonSerialized] public int renderRequestedCount = 0;
        [NonSerialized] public int setShaderCount = 0;
        [NonSerialized] public int shaderRequestedCount = 0;
        [NonSerialized] public int shadedCount = 0;
        [NonSerialized] public int publishedCount = 0;

        private void Awake() 
        {
            if(cameraSensors == null || cameraSensors.Count < 1)
            {
                Debug.LogError("Camera sensor list should have at least one camera to render.");
                return;
            }

            StartCoroutine(FixedUpdateRoutine());
        }

        private void Start()
        {   
            Debug.Log("Rendering Threading Mode: " + SystemInfo.renderingThreadingMode);
        }

        private void FixedUpdate()
        {   
            if (timerStart)
            {
                // Update timer.
                timer += Time.deltaTime;

                // Matching output to hz.
                var interval = 1.0f / (int)publishHz;
                if (timer >= interval)
                {   
                    publishFlag = true;
                    shouldPublishCount += cameraSensors.Count;
                    timer = 0f + (timer - interval);
                }
            }

            Debug.Log("FixedUpdate");
                
            Debug.Log("ShouldPublishCount : " + shouldPublishCount + "   " +
                      "RenderRequestedCount : " + renderRequestedCount + "   " +
                      "RenderedCount : " + renderedCount + "   " +
                      "SetShaderCount : " + setShaderCount + "   " +
                      "ShaderRequestedCount : " + shaderRequestedCount + "   " +
                      "ShadedCount : " + shadedCount + "   " +
                      "PublishedCount : " + publishedCount);
        }

        private void Update()
        {   
            Debug.Log("Update");

            Debug.Log("ShouldPublishCount : " + shouldPublishCount + "   " +
                      "RenderRequestedCount : " + renderRequestedCount + "   " +
                      "RenderedCount : " + renderedCount + "   " +
                      "SetShaderCount : " + setShaderCount + "   " +
                      "ShaderRequestedCount : " + shaderRequestedCount + "   " +
                      "ShadedCount : " + shadedCount + "   " +
                      "PublishedCount : " + publishedCount);

            //Thread.Sleep(200);
        }

        private void LateUpdate()
        {   
            Debug.Log("LateUpdate");

            Debug.Log("ShouldPublishCount : " + shouldPublishCount + "   " +
                      "RenderRequestedCount : " + renderRequestedCount + "   " +
                      "RenderedCount : " + renderedCount + "   " +
                      "SetShaderCount : " + setShaderCount + "   " +
                      "ShaderRequestedCount : " + shaderRequestedCount + "   " +
                      "ShadedCount : " + shadedCount + "   " +
                      "PublishedCount : " + publishedCount);
        }

        private void OnGUI()
        {   
            Debug.Log("OnGUI");

            Debug.Log("ShouldPublishCount : " + shouldPublishCount + "   " +
                      "RenderRequestedCount : " + renderRequestedCount + "   " +
                      "RenderedCount : " + renderedCount + "   " +
                      "SetShaderCount : " + setShaderCount + "   " +
                      "ShaderRequestedCount : " + shaderRequestedCount + "   " +
                      "ShadedCount : " + shadedCount + "   " +
                      "PublishedCount : " + publishedCount);

            //Thread.Sleep(100);
        }

        private IEnumerator FixedUpdateRoutine()
        {   
            yield return new WaitForSeconds(1);

            timerStart = true;

            while(true)
            {
                yield return new WaitForFixedUpdate();

                if(!publishFlag)
                {
                    continue;
                }
                publishFlag = false;

                Debug.Log("Publishing...");
                // sensors render at different frames one after another
                if(renderInQueue)
                {   
                    int CompletedCount = 0;
                    switch(processType)
                    {   
                        case 0:
                            for (int i = 0; i < cameraSensors.Count; i++)
                            {
                                yield return StartCoroutine(RenderCamera(cameraSensors[i], true));
                                CompletedCount++;
                                Debug.Log(CompletedCount + " Completed RenderCamera");
                            }
                            Debug.Log("All Completed RenderCamera");
                            break;
                        
                        case 1:
                            for (int i = 0; i < cameraSensors.Count; i++)
                            {
                                yield return StartCoroutine(RenderCamera_Optimized(cameraSensors[i], true));
                                CompletedCount++;
                                Debug.Log(CompletedCount + " Completed RenderCamera");
                            }
                            Debug.Log("All Completed RenderCamera");
                            break;

                        case 2:
                            for (int i = 0; i < cameraSensors.Count; i++)
                            {   
                                if(i == cameraSensors.Count-1)
                                {
                                    StartCoroutine(RenderCamera(cameraSensors[i], false));
                                }
                                else
                                {
                                    yield return StartCoroutine(RenderCamera(cameraSensors[i], false));
                                }
                                CompletedCount++;
                                Debug.Log(CompletedCount + " Completed RenderCamera");
                            }
                            Debug.Log("All Completed RenderCamera");
                            break;

                        case 3:
                            for (int i = 0; i < cameraSensors.Count; i++)
                            {   
                                if(i == cameraSensors.Count-1)
                                {
                                    StartCoroutine(RenderCamera_Optimized(cameraSensors[i], false));
                                }
                                else
                                {
                                    yield return StartCoroutine(RenderCamera_Optimized(cameraSensors[i], false));
                                }
                                CompletedCount++;
                                Debug.Log(CompletedCount + " Completed RenderCamera");
                            }
                            Debug.Log("All Completed RenderCamera");
                            break;

                        case 4:
                            for (int i = 0; i < cameraSensors.Count; i++)
                            {
                                yield return StartCoroutine(RenderCamera_test(cameraSensors[i], false));
                                CompletedCount++;
                                Debug.Log(CompletedCount + " Completed RenderCamera");
                            }
                            Debug.Log("All Completed RenderCamera");
                            break;
                    }
                }
                // sensors render at the same frame
                else
                {   
                    switch(processType)
                    {
                        case 0:
                            for (int i = 0; i < cameraSensors.Count; i++)
                            {
                                StartCoroutine(RenderCamera(cameraSensors[i], false));
                            }
                            break;

                        case 1:
                            for (int i = 0; i < cameraSensors.Count; i++)
                            {
                                StartCoroutine(RenderCamera_Optimized(cameraSensors[i], false));
                            }
                            break;

                        case 2:
                            renderTasks = new List<Task>();
                            for (int i = 0; i < cameraSensors.Count; i++)
                            {
                                renderTasks.Add(RenderCameraAsync(cameraSensors[i], false));
                            }
                            break;
                        
                        case 3:
                            renderCoroutines = new List<Coroutine>();
                            for (int i = 0; i < cameraSensors.Count; i++)
                            {
                                renderCoroutines.Add(StartCoroutine(RenderCamera(cameraSensors[i], false)));
                            }
                            int CompletedCount = 0;
                            foreach (var coroutine in renderCoroutines)
                            {
                                yield return coroutine;
                                CompletedCount++;
                                Debug.Log(CompletedCount + " Completed RenderCamera");
                            }
                            Debug.Log("All Completed RenderCamera");
                            break;

                        case 4:
                            renderTasks = new List<Task>();
                            for (int i = 0; i < cameraSensors.Count; i++)
                            {
                                renderTasks.Add(RenderCameraAsync(cameraSensors[i], false));
                            }
                            yield return new WaitUntil(() => Task.WhenAll(renderTasks).IsCompleted);
                            Debug.Log("All Completed RenderCamera");
                            break;
                        
                        case 5:
                            break;
                        
                        case 6:
                            renderTasks = new List<Task>();
                            for (int i = 0; i < cameraSensors.Count; i++)
                            {
                                renderTasks.Add(RenderCameraAsync(cameraSensors[i], true));                            
                            }
                            break;
                        
                        case 7:
                            // Parallel.Forを使用して並列にレンダリング
                            Parallel.For(0, cameraSensors.Count, i =>
                            {
                                var cameraSensor = cameraSensors[i];
                                if (cameraSensor.gameObject.activeInHierarchy)
                                {
                                    cameraSensor.DoRender();
                                    Interlocked.Increment(ref renderedCount); // スレッドセーフにカウントをインクリメント
                                }
                            });
                            break;
                    }
                }
                Debug.Log("Published.");
            }
        }

    /// <summary>
    /// Call camera sensor to do a render.
    /// </summary>
    /// <param name="cameraSensor">Camera sensor to render.</param>
    /// <param name="wait">Set True if wait to end of the frame after render.</param>
        private IEnumerator RenderCamera(CameraSensor cameraSensor, bool wait) 
        {
            if(cameraSensor.gameObject.activeInHierarchy)
            {
                cameraSensor.DoRender();
            }

            if(wait)
            {
                yield return new WaitForEndOfFrame();
            }
            else
            {
                yield return new WaitForFixedUpdate();
            }
            Debug.Log("Completed RenderCamera");
        }

        private IEnumerator RenderCamera_Optimized(CameraSensor cameraSensor, bool wait) 
        {
            if(cameraSensor.gameObject.activeInHierarchy)
            {
                cameraSensor.DoRender_Optimized(useCommandBuffer);
            }
            
            if(wait)
            {
                yield return new WaitForEndOfFrame();
            }
            else
            {
                yield return new WaitForFixedUpdate();
            }
            Debug.Log("Completed RenderCamera");
        }

        private IEnumerator RenderCamera_test(CameraSensor cameraSensor, bool wait) 
        {
            if(cameraSensor.gameObject.activeInHierarchy)
            {
                StartCoroutine(cameraSensor.DoRender_test());
            }
            
            if(wait)
            {
                yield return new WaitForEndOfFrame();
            }
            else
            {
                yield return new WaitForFixedUpdate();
            }
            Debug.Log("Completed RenderCamera");
        }

        private async Task RenderCameraAsync(CameraSensor cameraSensor, bool run)
        {   
            if(cameraSensor.gameObject.activeInHierarchy)
            {   
                if(!run) cameraSensor.DoRender();
                else await Task.Run(() => cameraSensor.DoRender());
            }

            if(!run) await Task.Yield();
            Debug.Log("Completed RenderCamera");
        }
    }
}
