using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SensorKitSelector : MonoBehaviour
{   
    [Header("Lidar Sensors")]
    [SerializeField] public List<GameObject> lidarSensors = default;

    [Header("Camera Sensor Holders")]
    [SerializeField] public List<GameObject> cameraSensorHolders = default;

    void Awake()
    {   
        Debug.Log("Rendering Threading Mode: " + SystemInfo.renderingThreadingMode);
        
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-lidar":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out int lidar))
                    {   
                        switch (lidar)
                        {
                            case 0:
                                lidarSensors[0].SetActive(false);
                                lidarSensors[1].SetActive(false);
                                lidarSensors[2].SetActive(false);
                                break;
                            case 1:
                                lidarSensors[0].SetActive(true);
                                lidarSensors[1].SetActive(false);
                                lidarSensors[2].SetActive(false);
                                break;
                            case 2:
                                lidarSensors[0].SetActive(true);
                                lidarSensors[1].SetActive(true);
                                lidarSensors[2].SetActive(true);
                                break;
                        }
                    }
                    break;

                case "-holder":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out int holder))
                    {
                        switch (holder)
                        {
                            case 0:
                                cameraSensorHolders[0].SetActive(true);
                                cameraSensorHolders[1].SetActive(false);
                                cameraSensorHolders[2].SetActive(false);
                                break;
                            case 1:
                                cameraSensorHolders[0].SetActive(false);
                                cameraSensorHolders[1].SetActive(true);
                                cameraSensorHolders[2].SetActive(false);
                                break;
                            case 2:
                                cameraSensorHolders[0].SetActive(false);
                                cameraSensorHolders[1].SetActive(false);
                                cameraSensorHolders[2].SetActive(true);
                                break;
                            case 3:
                                cameraSensorHolders[0].SetActive(true);
                                cameraSensorHolders[1].SetActive(true);
                                cameraSensorHolders[2].SetActive(false);
                                break;
                            case 4:
                                cameraSensorHolders[0].SetActive(true);
                                cameraSensorHolders[1].SetActive(false);
                                cameraSensorHolders[2].SetActive(true);
                                break;
                        }
                    }
                    break;
            }
        }
    }
}
