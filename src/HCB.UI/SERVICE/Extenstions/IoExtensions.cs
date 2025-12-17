using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HCB.UI
{
    public static class IoExtensions
    {
        public const string IoDeviceName = "PmacIO";

        #region Digital Input Names
        public const string DI_EMO_1_SWITCH = "DI_EMO_1_SWITCH";
        public const string DI_EMO_2_SWITCH = "DI_EMO_2_SWITCH";
        public const string DI_START_SWITCH = "DI_START_SWITCH";
        public const string DI_STOP_SWITCH = "DI_STOP_SWITCH";
        public const string DI_RESET_SWITCH = "DI_RESET_SWITCH";
        public const string DI_LIGHT_CURTAIN = "DI_LIGHT_CURTAIN";
        public const string DI_FRONT_LEFT_DOOR = "DI_FRONT_LEFT_DOOR";
        public const string DI_FRONT_RIGHT_DOOR = "DI_FRONT_RIGHT_DOOR";
        public const string DI_SIDE_LEFT_DOOR = "DI_SIDE_LEFT_DOOR";
        public const string DI_SIDE_RIGHT_DOOR = "DI_SIDE_RIGHT_DOOR";
        public const string DI_FAN_1_ALARM = "DI_FAN_1_ALARM";
        public const string DI_FAN_2_ALARM = "DI_FAN_2_ALARM";
        public const string DI_FAN_3_ALARM = "DI_FAN_3_ALARM";
        public const string DI_FAN_4_ALARM = "DI_FAN_4_ALARM";
        public const string DI_CP04_TRIP = "DI_CP04_TRIP";                  // EFU
        public const string DI_CP05_TRIP = "DI_CP05_TRIP";                  // Piezo & LV32-DLDC
        public const string DI_CP06_TRIP = "DI_CP06_TRIP";                  // LED Controller
        public const string DI_CP07_TRIP = "DI_CP07_TRIP";                  // Driver Power SMPS
        public const string DI_CP08_TRIP = "DI_CP08_TRIP";                  // Safety SMPS
        public const string DI_CP10_TRIP = "DI_CP10_TRIP";                  // Driver Controller
        public const string DI_CP11_TRIP = "DI_CP11_TRIP";                  // Safety Module
        public const string DI_CP12_TRIP = "DI_CP12_TRIP";                  // Safety DC
        public const string DI_DRIVER_BUS_DC = "DI_DRIVER_BUS_DC";          // Driver Bus DC
        public const string DI_CP22_TRIP = "DI_CP22_TRIP";                  // Motion Controller
        public const string DI_CP23_TRIP = "DI_CP23_TRIP";                  // Load Cell Indicator
        public const string DI_CP24_TRIP = "DI_CP24_TRIP";                  // Ethernet Hub
        public const string DI_HEADER_VAC_EJECTOR = "DI_HEADER_VAC_EJECTOR";// Header Picker Vacuum Ejector
        public const string DI_DTABLE_VAC_PRESSURE_SWITCH = "DI_DTABLE_VAC_PRESSURE_SWITCH";// D-Table Vacuum Pressure Switch
        public const string DI_WTABLE_VAC_PRESSURE_SWITCH = "DI_WTABLE_VAC_PRESSURE_SWITCH";// W-Table Vacuum Pressure Switch
        public const string DI_WTABLE_LIFT_PIN_UP = "DI_WTABLE_LIFT_PIN_UP_SENSOR";          // W-Table Lift Pin Up
        public const string DI_WTABLE_LIFT_PIN_DOWN = "DI_WTABLE_LIFT_PIN_DOWN_SENSOR";      // W-Table Lift Pin Down

        public const string DI_MAIN_CDA_PRESSURE_SWITCH_ALARM = "DI_MAIN_CDA_PRESSURE_SWITCH_ALARM";// Main CDA Pressure Switch Alarm
        public const string DI_MAIN_VAC_PRESSURE_SWITCH_1_ALARM = "DI_MAIN_VAC_PRESSURE_SWITCH_1_ALARM";// Main Vacuum Pressure Switch 1 Alarm
        public const string DI_MAIN_VAC_PRESSURE_SWITCH_2_ALARM = "DI_MAIN_VAC_PRESSURE_SWITCH_2_ALARM";// Main Vacuum Pressure Switch 2 Alarm
        public const string DI_MAIN_N2_PRESSURE_SWITCH_ALARM = "DI_MAIN_N2_PRESSURE_SWITCH_ALARM";// Main N2 Pressure Switch Alarm
        public const string DI_SAFETY_MODULE = "DI_SAFETY_MODULE";          // Safety Module Status
        #endregion

        #region Digital Ouput Names
        public const string DO_START_SWITCH_LAMP = "DO_START_SWITCH_LAMP";                                // Start S/W Lamp ON
        public const string DO_STOP_SWITCH_LAMP = "DO_STOP_SWITCH_LAMP";                                  // Stop S/W Lamp ON
        public const string DO_TOWER_LAMP_GREEN = "DO_TOWER_LAMP_GREEN";                                  // Tower Lamp Green ON
        public const string DO_TOWER_LAMP_YELLOW = "DO_TOWER_LAMP_YELLOW";                                // Tower Lamp Yellow ON
        public const string DO_TOWER_LAMP_RED = "DO_TOWER_LAMP_RED";                                      // Tower Lamp Red ON
        public const string DO_TOWER_LAMP_BUZZER = "DO_TOWER_LAMP_BUZZER";                                // Tower Lamp Buzzer ON
        public const string DO_HEADER_EJECTOR_VAC_ON = "DO_HEADER_EJECTOR_VAC_ON";                        // Header Picker Vacuum ON
        public const string DO_HEADER_EJECTOR_VAC_RELEASE_ON = "DO_HEADER_EJECTOR_VAC_RELEASE_ON";        // Header Picker Vacuum Release ON
        public const string DO_ZIMM_SOL_ON = "DO_ZIMM_SOL_ON";                                            // Zimmer Solenoid ON
        public const string DO_DTABLE_VAC_1_ON = "DO_DTABLE_VAC_1_ON";                                    // D-Table Vacuum 1 ON
        public const string DO_DTABLE_VAC_1_RELEASE = "DO_DTABLE_VAC_1_RELEASE";                          // D-Table Vacuum 1 Release ON
        public const string DO_DTABLE_VAC_2_ON = "DO_DTABLE_VAC_2_ON";                                    // D-Table Vacuum 2 ON
        public const string DO_DTABLE_VAC_2_RELEASE = "DO_DTABLE_VAC_2_RELEASE";                          // D-Table Vacuum 2 Release ON
        public const string DO_DTABLE_VAC_3_ON = "DO_DTABLE_VAC_3_ON";                                    // D-Table Vacuum 3 ON
        public const string DO_DTABLE_VAC_3_RELEASE = "DO_DTABLE_VAC_3_RELEASE";                          // D-Table Vacuum 3 Release ON
        public const string DO_DTABLE_VAC_4_ON = "DO_DTABLE_VAC_4_ON";                                    // D-Table Vacuum 4 ON
        public const string DO_DTABLE_VAC_4_RELEASE = "DO_DTABLE_VAC_4_RELEASE";                          // D-Table Vacuum 4 Release ON
        public const string DO_DTABLE_VAC_5_ON = "DO_DTABLE_VAC_5_ON";                                    // D-Table Vacuum 5 ON
        public const string DO_DTABLE_VAC_5_RELEASE = "DO_DTABLE_VAC_5_RELEASE";                          // D-Table Vacuum 5 Release ON
        public const string DO_DTABLE_VAC_6_ON = "DO_DTABLE_VAC_6_ON";                                    // D-Table Vacuum 6 ON
        public const string DO_DTABLE_VAC_6_RELEASE = "DO_DTABLE_VAC_6_RELEASE";                          // D-Table Vacuum 6 Release ON
        public const string DO_DTABLE_VAC_7_ON = "DO_DTABLE_VAC_7_ON";                                    // D-Table Vacuum 7 ON
        public const string DO_DTABLE_VAC_7_RELEASE = "DO_DTABLE_VAC_7_RELEASE";                          // D-Table Vacuum 7 Release ON
        public const string DO_DTABLE_VAC_8_ON = "DO_DTABLE_VAC_8_ON";                                    // D-Table Vacuum 8 ON
        public const string DO_DTABLE_VAC_8_RELEASE = "DO_DTABLE_VAC_8_RELEASE";                          // D-Table Vacuum 8 Release ON
        public const string DO_DTABLE_VAC_9_ON = "DO_DTABLE_VAC_9_ON";                                    // D-Table Vacuum 9 ON
        public const string DO_DTABLE_VAC_9_RELEASE = "DO_DTABLE_VAC_9_RELEASE";                          // D-Table Vacuum 9 Release ON
        public const string DO_WTABLE_LIFT_PIN_UP = "DO_WTABLE_LIFT_PIN_UP";                              // W-Table Lift Pin Up
        public const string DO_WTABLE_LIFT_PIN_DOWN = "DO_WTABLE_LIFT_PIN_DOWN";                          // W-Table Lift Pin Down
        public const string DO_WTABLE_VAC_1_ON = "DO_WTABLE_VAC_1_ON";                                    // W-Table Vacuum 1 ON
        public const string DO_WTABLE_VAC_1_RELEASE = "DO_WTABLE_VAC_1_RELEASE";                          // W-Table Vacuum 1 Release ON
        public const string DO_WTABLE_VAC_2_ON = "DO_WTABLE_VAC_2_ON";                                    // W-Table Vacuum 2 ON
        public const string DO_WTABLE_VAC_2_RELEASE = "DO_WTABLE_VAC_2_RELEASE";                          // W-Table Vacuum 2 Release ON
        public const string DO_WTABLE_VAC_3_ON = "DO_WTABLE_VAC_3_ON";                                    // W-Table Vacuum 3 ON
        public const string DO_WTABLE_VAC_3_RELEASE = "DO_WTABLE_VAC_3_RELEASE";                          // W-Table Vacuum 3 Release ON
        public const string DO_WTABLE_VAC_4_ON = "DO_WTABLE_VAC_4_ON";                                    // W-Table Vacuum 4 ON
        public const string DO_WTABLE_VAC_4_RELEASE = "DO_WTABLE_VAC_4_RELEASE";                          // W-Table Vacuum 4 Release ON
        public const string DO_WTABLE_VAC_5_ON = "DO_WTABLE_VAC_5_ON";                                    // W-Table Vacuum 5 ON
        public const string DO_WTABLE_VAC_5_RELEASE = "DO_WTABLE_VAC_5_RELEASE";                          // W-Table Vacuum 5 Release ON
        public const string DO_WTABLE_N2_BLOW = "DO_WTABLE_N2_BLOW";                                      // W-Table N2 Blow ON
        public const string DO_INDICATOR_ZERO = "DO_INDICATOR_ZERO";                                      // Load Cell Indicator Zeroing
        #endregion


        public static void SetDigital(this ISequenceHelper helper, string name, bool bOnOff)
        {
            var device = helper.DeviceManager.GetDevice<PmacIoDevice>(IoDeviceName);
            if (device == null)
            {
                helper.Log(LogLevel.Critical, $"Io Device {IoDeviceName} not found.");
            }
            device.SetDigital(name, bOnOff);
        }

        public static bool GetDigital(this ISequenceHelper helper, string name)
        {
            var device = helper.DeviceManager.GetDevice<PmacIoDevice>(IoDeviceName);
            if (device == null)
            {
                helper.Log(LogLevel.Critical, $"Io Device {IoDeviceName} not found.");
            }
            return device.GetDigital(name);
        }

        public static void SetTowerLamp(this ISequenceHelper helper, bool green, bool yellow, bool red, bool buzzer)
        {
            var device = helper.DeviceManager.GetDevice<PmacIoDevice>(IoDeviceName);
            if (device == null)
            {
                helper.Log(LogLevel.Critical, $"Io Device {IoDeviceName} not found.");
            }
            device.SetDigital(DO_TOWER_LAMP_GREEN, green);
            device.SetDigital(DO_TOWER_LAMP_YELLOW, yellow);
            device.SetDigital(DO_TOWER_LAMP_RED, red);
            device.SetDigital(DO_TOWER_LAMP_BUZZER, buzzer);
        }

        /// <summary>
        /// Head Picker Vacuum On/Off
        /// Vacuum On: DO_HEADER_EJECTOR_VAC_ON = true, DO_HEADER_EJECTOR_VAC_RELEASE_ON = false
        /// Vaccum Off: DO_HEADER_EJECTOR_VAC_ON = false, DO_HEADER_EJECTOR_VAC_RELEASE_ON = true
        /// </summary>
        /// <param name="helper"></param>
        /// <param name="onOff"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public static async Task HeadPickerVacuum(this ISequenceHelper helper, eOnOff onOff, CancellationToken ct)
        {
            var bOnOff = onOff == eOnOff.On ? true : false;
            var device = helper.DeviceManager.GetDevice<PmacIoDevice>(IoDeviceName);

            if (device == null)
            {
                helper.Log(LogLevel.Critical, $"Io Device {IoDeviceName} not found.");
            }

            device.SetDigital(DO_HEADER_EJECTOR_VAC_ON, bOnOff);
            device.SetDigital(DO_HEADER_EJECTOR_VAC_RELEASE_ON, !bOnOff);

            while (ct.IsCancellationRequested == false)
            {
                await helper.DelayAsync(100, ct); // Small delay to ensure the servo on command is processed
                await helper.WaitUntilAsync(
                    () => device.GetDigital(DI_HEADER_VAC_EJECTOR) == bOnOff,
                    3000,
                    ct,
                    $"{DO_HEADER_EJECTOR_VAC_ON} = {onOff} Timeout"
                );
            }
        }

        public static async Task DTableVacuumAll(this ISequenceHelper helper, eOnOff onOff, CancellationToken ct)
        {
            for (int channel = 1; channel <= 9; channel++)
            {
                await helper.DTableVacuum(channel, onOff, ct);
            }
        }

        public static async Task DTableVacuum(this ISequenceHelper helper, int channel, eOnOff onOff, CancellationToken ct)
        {
            var bOnOff = onOff == eOnOff.On ? true : false;
            var device = helper.DeviceManager.GetDevice<PmacIoDevice>(IoDeviceName);
            if (device == null)
            {
                helper.Log(LogLevel.Critical, $"Io Device {IoDeviceName} not found.");
            }
            string doOn = $"DO_DTABLE_VAC_{channel}_ON";
            string doRelease = $"DO_DTABLE_VAC_{channel}_RELEASE";
            string diPressureSwitch = $"DI_DTABLE_VAC_PRESSURE_SWITCH";
            device.SetDigital(doOn, bOnOff);
            device.SetDigital(doRelease, !bOnOff);
            while (ct.IsCancellationRequested == false)
            {
                await helper.DelayAsync(100, ct); // Small delay to ensure the servo on command is processed
                await helper.WaitUntilAsync(
                    () => device.GetDigital(diPressureSwitch) == bOnOff,
                    3000,
                    ct,
                    $"{doOn} = {onOff} Timeout"
                );
            }
        }

        public static async Task WTableVacuumAll(this ISequenceHelper helper, eOnOff onOff, CancellationToken ct)
        {
            for (int channel = 1; channel <= 5; channel++)
            {
                await helper.WTableVacuum(channel, onOff, ct);
            }
        }

        public static async Task WTableVacuum(this ISequenceHelper helper, int channel, eOnOff onOff, CancellationToken ct)
        {
            var bOnOff = onOff == eOnOff.On ? true : false;
            var device = helper.DeviceManager.GetDevice<PmacIoDevice>(IoDeviceName);
            if (device == null)
            {
                helper.Log(LogLevel.Critical, $"Io Device {IoDeviceName} not found.");
            }
            string doOn = $"DO_WTABLE_VAC_{channel}_ON";
            string doRelease = $"DO_WTABLE_VAC_{channel}_RELEASE";
            string doN2Blow = $"DO_WTABLE_N2_BLOW";
            string diPressureSwitch = $"DI_WTABLE_VAC_PRESSURE_SWITCH";
           
            device.SetDigital(doOn, bOnOff);
            device.SetDigital(doRelease, !bOnOff);
            device.SetDigital(doN2Blow, !bOnOff); // N2 Blow is the opposite of Vacuum On/Off
            while (ct.IsCancellationRequested == false)
            {
                await helper.DelayAsync(100, ct); // Small delay to ensure the servo on command is processed
                await helper.WaitUntilAsync(
                    () => device.GetDigital(diPressureSwitch) == bOnOff,
                    3000,
                    ct,
                    $"{doOn} = {onOff} Timeout"
                );
            }

            device.SetDigital(doN2Blow, false); // Ensure N2 Blow is turned off after operation
        }

        public static async Task WTableLiftPin(this ISequenceHelper helper, eUpDown upDown, CancellationToken ct)
        {
            var device = helper.DeviceManager.GetDevice<PmacIoDevice>(IoDeviceName);
            if (device == null)
            {
                helper.Log(LogLevel.Critical, $"Io Device {IoDeviceName} not found.");
            }
            device.SetDigital(DO_WTABLE_LIFT_PIN_UP, upDown == eUpDown.Up? true : false);
            device.SetDigital(DO_WTABLE_LIFT_PIN_DOWN, upDown == eUpDown.Down? true : false);

            while(ct.IsCancellationRequested == false)
            {
                await helper.DelayAsync(100, ct); // Small delay to ensure the servo on command is processed
                await helper.WaitUntilAsync(
                    () => device.GetDigital(DI_WTABLE_LIFT_PIN_UP) == (upDown == eUpDown.Up) && device.GetDigital(DI_WTABLE_LIFT_PIN_DOWN) == (upDown == eUpDown.Down),
                    3000,
                    ct,
                    $"W-TABLE LIFT PIN UP/DOWN = {upDown} Timeout"
                );
            }
        }

        public static void StartLampOnOff(this ISequenceHelper helper, eOnOff onOff, CancellationToken ct)
        {
            var device = helper.DeviceManager.GetDevice<PmacIoDevice>(IoDeviceName);

            if (device == null)
            {
                helper.Log(LogLevel.Critical, $"Io Device {IoDeviceName} not found.");
            }

            device.SetDigital(DO_START_SWITCH_LAMP, onOff == eOnOff.On? true : false);

            //if (ready)
            //{
            //    await axis.ServoReady(ready);
            //    await helper.DelayAsync(100, ct); // Small delay to ensure the servo on command is processed
            //    await helper.WaitUntilAsync(
            //        () => axis.IsEnabled,
            //        3000,
            //        ct,
            //        $"Axis {axis.Name} Servo On Timeout"
            //    );
            //}
            //else
            //{
            //    await axis.ServoReady(ready);
            //    await helper.DelayAsync(100, ct); // Small delay to ensure the servo off command is processed
            //    await helper.WaitUntilAsync(
            //        () => !axis.IsEnabled,
            //        3000,
            //        ct,
            //        $"Axis {axis.Name} Servo Off Timeout"
            //    );
            //}
        }
    }
}
