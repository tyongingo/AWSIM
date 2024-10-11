using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AWSIM
{
    /// <summary>
    /// Displays speed and gear UI.
    /// </summary>
    public class VehicleInfomationUI : MonoBehaviour
    {
        [SerializeField] public Vehicle vehicle;
        [SerializeField] Text speedText;
        [SerializeField] Text gearText;
        [SerializeField] Text accelerationText; // 加速度表示用のTextフィールド
        [SerializeField] Text steerAngleText; // ステアアングル表示用のTextフィールド

        void Update()
        {
            if (!vehicle) {
                speedText.text = "";
                gearText.text = "";
                accelerationText.text = ""; // 加速度のテキストをクリア
                steerAngleText.text = ""; // ステアアングルのテキストをクリア
                return;
            }

            speedText.text = "" + Mathf.Floor(vehicle.Speed * 3.6f);
            gearText.text = "" + GetShiftString(vehicle.AutomaticShift);
            accelerationText.text = "" + Mathf.Floor(vehicle.LocalAcceleration.z * 100) / 100; // 前方方向への加速度を表示
            steerAngleText.text = "" + Mathf.Floor(vehicle.SteerAngle * 100) / 100; // ステアアングルを表示

            static string GetShiftString(Vehicle.Shift shift)
            {
                string shiftString = "";
                if (shift == Vehicle.Shift.DRIVE)
                    shiftString = "D";
                else if (shift == Vehicle.Shift.NEUTRAL)
                    shiftString = "N";
                else if (shift == Vehicle.Shift.PARKING)
                    shiftString = "P";
                else
                    shiftString = "R";

                return shiftString;
            }
        }
    }
}