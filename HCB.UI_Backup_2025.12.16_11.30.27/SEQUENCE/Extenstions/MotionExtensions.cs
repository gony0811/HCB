using HCB.Data.Entity.Type;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace HCB.UI
{
    public static class MotionExtensions
    {
        public const string PowerPmacDeviceName = "PMAC";

        public static async Task Servo(this ISequenceHelper helper, int axisId, bool ready, CancellationToken ct)
        {
            var axis = helper.DeviceManager.GetDevice<PowerPmacDevice>(PowerPmacDeviceName).FindMotionByMotorIndex(axisId);
            if (axis == null)
            {
                helper.Log(LogLevel.Critical, $"Axis with ID {axisId} not found.");
            }
            if (ready)
            {
                await axis.ServoReady(ready);
                await helper.DelayAsync(100, ct); // Small delay to ensure the servo on command is processed
                await helper.WaitUntilAsync(
                    () => axis.IsEnabled,
                    3000,
                    ct,
                    $"Axis {axis.Name} Servo On Timeout"
                );
            }
            else
            {
                await axis.ServoReady(ready);
                await helper.DelayAsync(100, ct); // Small delay to ensure the servo off command is processed
                await helper.WaitUntilAsync(
                    () => !axis.IsEnabled,
                    3000,
                    ct,
                    $"Axis {axis.Name} Servo Off Timeout"
                );
            }
        }

        public static async Task StopAsync(this ISequenceHelper helper, int axisId, CancellationToken ct)
        {
            var axis = helper.DeviceManager.GetDevice<PowerPmacDevice>(PowerPmacDeviceName).FindMotionByMotorIndex(axisId);
            if (axis == null)
            {
                helper.Log(LogLevel.Critical, $"Axis with ID {axisId} not found.");
            }
            await axis.MoveStop();
            await helper.DelayAsync(100, ct); // Small delay to ensure the stop command is processed
            await helper.WaitUntilAsync(
                () => !axis.IsBusy,
                3000,
                ct,
                $"Axis {axis.Name} Stop Timeout"
            );
        }

        public static async Task HomeAsync(this ISequenceHelper helper, int axisId, CancellationToken ct)
        {
            var axis = helper.DeviceManager.GetDevice<PowerPmacDevice>(PowerPmacDeviceName).FindMotionByMotorIndex(axisId);
            if (axis == null)
            {
                helper.Log(LogLevel.Critical, $"Axis with ID {axisId} not found.");
            }
            await axis.Home();
            await helper.DelayAsync(100, ct); // Small delay to ensure the home command is processed
            await helper.WaitUntilAsync(
                () => axis.IsHomeDone,
                20000,
                ct,
                $"Axis {axis.Name} Homing Timeout"
            );
        }

        public static async Task MoveAsync(this ISequenceHelper helper, int axisId, string positionName, CancellationToken ct)
        {
            var axis = helper.DeviceManager.GetDevice<PowerPmacDevice>(PowerPmacDeviceName).FindMotionByMotorIndex(axisId);
            var position = axis.PositionList.FirstOrDefault(p => p.Name == positionName);

            if (axis == null || position == null)
            {
                helper.Log(LogLevel.Critical, $"Axis with ID {axisId} or Position {positionName} not found.");
            }

            await axis.Move(MoveType.Absolute, jerk: 100, position.Speed, position.Position);

            await helper.DelayAsync(100, ct); // Small delay to ensure the move command is processed

            await helper.WaitUntilAsync(
                () => axis.InPosition,
                10000,
                ct,
                $"Axis {axis.Name} Move to {positionName} Timeout"
            );
        }

        public static async Task AbsoluteMoveAsync(this ISequenceHelper helper, int axisId, double velocity, double position, CancellationToken ct)
        {
            var axis = helper.DeviceManager.GetDevice<PowerPmacDevice>(PowerPmacDeviceName).FindMotionByMotorIndex(axisId);

            if (axis == null)
            {
                helper.Log(LogLevel.Critical, $"Axis with ID {axisId} not found.");
            }

            await axis.Move(MoveType.Absolute, jerk: 100, velocity, position);

            await helper.DelayAsync(100, ct); // Small delay to ensure the move command is processed

            await helper.WaitUntilAsync(
                () => axis.InPosition,
                10000,
                ct,
                $"Axis {axis.Name} Move to {position} Timeout"
            );
        }

        public static async Task RelativeMoveAsync(this ISequenceHelper helper, int axisId, double velocity, double distance, CancellationToken ct)
        {
            var axis = helper.DeviceManager.GetDevice<PowerPmacDevice>(PowerPmacDeviceName).FindMotionByMotorIndex(axisId);

            if (axis == null)
            {
                helper.Log(LogLevel.Critical, $"Axis with ID {axisId} not found.");
            }

            await axis.Move(MoveType.Relative, jerk: 100, velocity, distance);

            await helper.DelayAsync(100, ct); // Small delay to ensure the move command is processed

            await helper.WaitUntilAsync(
                () => axis.InPosition,
                10000,
                ct,
                $"Axis {axis.Name} Relative Move as {distance} Timeout"
            );
        }
    }
}
