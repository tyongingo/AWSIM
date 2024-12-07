using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AWSIM;
using RGLUnityPlugin;
using ROS2;


public class QosAndHzSelector : MonoBehaviour
{   
    [Header("Lidar Sensor")]
    [SerializeField] public GameObject lidarSensor = default;

    [Header("Camera Sensor Holder")]
    [SerializeField] public List<GameObject> cameraSensors = default;

    [Header("IMU Sensor")]
    [SerializeField] public GameObject imuSensor = default;

    [Header("GNSS Sensor")]
    [SerializeField] public GameObject gnssSensor = default;

    [Header("Pose Sensor")]
    [SerializeField] public GameObject poseSensor = default;

    [Header("Odometry Sensor")]
    [SerializeField] public GameObject odometrySensor = default;

    void Awake()
    {   
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-qos":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out int qos))
                    {   
                        switch (qos)
                        {
                            case 0:
                                lidarSensor.GetComponent<RglLidarPublisher>().qos.reliabilityPolicy = RGLQosPolicyReliability.QOS_POLICY_RELIABILITY_BEST_EFFORT;
                                lidarSensor.GetComponent<RglLidarPublisher>().qos.durabilityPolicy = RGLQosPolicyDurability.QOS_POLICY_DURABILITY_VOLATILE;
                                lidarSensor.GetComponent<RglLidarPublisher>().qos.historyPolicy = RGLQosPolicyHistory.QOS_POLICY_HISTORY_KEEP_LAST;
                                lidarSensor.GetComponent<RglLidarPublisher>().qos.historyDepth = 5;

                                foreach (var cameraSensor in cameraSensors)
                                {   
                                    cameraSensor.GetComponent<CameraRos2Publisher>().qosSettings.ReliabilityPolicy = ReliabilityPolicy.QOS_POLICY_RELIABILITY_BEST_EFFORT;
                                    cameraSensor.GetComponent<CameraRos2Publisher>().qosSettings.DurabilityPolicy = DurabilityPolicy.QOS_POLICY_DURABILITY_VOLATILE;
                                    cameraSensor.GetComponent<CameraRos2Publisher>().qosSettings.HistoryPolicy = HistoryPolicy.QOS_POLICY_HISTORY_KEEP_LAST;
                                    cameraSensor.GetComponent<CameraRos2Publisher>().qosSettings.Depth = 1;
                                }

                                imuSensor.GetComponent<ImuRos2Publisher>().qosSettings.ReliabilityPolicy = ReliabilityPolicy.QOS_POLICY_RELIABILITY_BEST_EFFORT;
                                imuSensor.GetComponent<ImuRos2Publisher>().qosSettings.DurabilityPolicy = DurabilityPolicy.QOS_POLICY_DURABILITY_VOLATILE;
                                imuSensor.GetComponent<ImuRos2Publisher>().qosSettings.HistoryPolicy = HistoryPolicy.QOS_POLICY_HISTORY_KEEP_LAST;
                                imuSensor.GetComponent<ImuRos2Publisher>().qosSettings.Depth = 1;

                                gnssSensor.GetComponent<GnssRos2Publisher>().qosSettings.ReliabilityPolicy = ReliabilityPolicy.QOS_POLICY_RELIABILITY_BEST_EFFORT;
                                gnssSensor.GetComponent<GnssRos2Publisher>().qosSettings.DurabilityPolicy = DurabilityPolicy.QOS_POLICY_DURABILITY_VOLATILE;
                                gnssSensor.GetComponent<GnssRos2Publisher>().qosSettings.HistoryPolicy = HistoryPolicy.QOS_POLICY_HISTORY_KEEP_LAST;
                                gnssSensor.GetComponent<GnssRos2Publisher>().qosSettings.Depth = 1;

                                poseSensor.GetComponent<PoseRos2Publisher>().QosSettings.ReliabilityPolicy = ReliabilityPolicy.QOS_POLICY_RELIABILITY_BEST_EFFORT;
                                poseSensor.GetComponent<PoseRos2Publisher>().QosSettings.DurabilityPolicy = DurabilityPolicy.QOS_POLICY_DURABILITY_VOLATILE;
                                poseSensor.GetComponent<PoseRos2Publisher>().QosSettings.HistoryPolicy = HistoryPolicy.QOS_POLICY_HISTORY_KEEP_LAST;
                                poseSensor.GetComponent<PoseRos2Publisher>().QosSettings.Depth = 1;

                                odometrySensor.GetComponent<OdometryRos2Publisher>().QosSettings.ReliabilityPolicy = ReliabilityPolicy.QOS_POLICY_RELIABILITY_BEST_EFFORT;
                                odometrySensor.GetComponent<OdometryRos2Publisher>().QosSettings.DurabilityPolicy = DurabilityPolicy.QOS_POLICY_DURABILITY_VOLATILE;
                                odometrySensor.GetComponent<OdometryRos2Publisher>().QosSettings.HistoryPolicy = HistoryPolicy.QOS_POLICY_HISTORY_KEEP_LAST;
                                odometrySensor.GetComponent<OdometryRos2Publisher>().QosSettings.Depth = 1;

                                break;

                            case 1:
                                

                                break;

                            case 2:
                                

                                break;
                        }
                    }
                    break;

                case "-hz":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out int hz))
                    {

                    }
                    break;

                case "-depth":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out int depth))
                    {
                        foreach (var cameraSensor in cameraSensors)
                        {
                            cameraSensor.GetComponent<CameraRos2Publisher>().qosSettings.Depth = depth;
                        }
                    }
                    break;

                case "-reli":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out int reli))
                    {
                        
                    }
                    break;

                case "-dura":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out int dura))
                    {
                        
                    }
                    break;
            }
        }
    }
}
