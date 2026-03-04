using HCB.Data.Entity.Type;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Telerik.Windows.Documents.Fixed.Model.Data;

namespace HCB.UI
{
    public static partial class MotionExtensions
    {


        public static async Task Servo(this ISequenceHelper helper, int motorNo, bool ready, CancellationToken ct)
        {
            var axis = helper.DeviceManager.GetDevice<PowerPmacDevice>("PMAC").FindMotionByMotorIndex(motorNo);
            if (axis == null)
            {
                helper.Log(LogLevel.Critical, $"Axis with No. {motorNo} not found.");
            }

            if (helper.IsSimulation)
            {
                helper.Log(LogLevel.Information, $"[Simulation] Axis {axis.Name} Servo {(ready ? "On" : "Off")}");
                return;
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

        public static async Task StopAllAsync(this ISequenceHelper helper, CancellationToken ct)
        {
            var pmac = helper.DeviceManager.GetDevice<PowerPmacDevice>(PowerPmacDeviceName);
            var axes = pmac.MotionList;

            if (helper.IsSimulation)
            {
                helper.Log(LogLevel.Information, "[Simulation] Stopping all axes.");
                return;
            }

            // 1. 모든 축에 대해 동시에 정지 명령을 보냅니다.
            var stopTasks = axes.Select(axis => axis.MoveStop());
            await Task.WhenAll(stopTasks);

            await helper.DelayAsync(100, ct); // Small delay to ensure the stop commands are processed

            // 2. 모든 축이 멈출 때까지 동시에 기다립니다.
            var waitTasks = axes.Select(axis => helper.WaitUntilAsync(
                () => !axis.IsBusy,
                3000,
                ct,
                $"Axis {axis.Name} Stop Timeout"
            ));
            await Task.WhenAll(waitTasks);
        }

        public static async Task StopAsync(this ISequenceHelper helper, int axisId, CancellationToken ct)
        {
            var axis = helper.DeviceManager.GetDevice<PowerPmacDevice>(PowerPmacDeviceName).FindMotionByMotorIndex(axisId);
            if (axis == null)
            {
                helper.Log(LogLevel.Critical, $"Axis with ID {axisId} not found.");
            }

            if (helper.IsSimulation)
            {
                helper.Log(LogLevel.Information, $"[Simulation] Stopping axis {axis.Name}.");
                return;
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

        public static async Task HomeAsync(this ISequenceHelper helper, int motorNo, CancellationToken ct)
        {
            var axis = helper.DeviceManager.GetDevice<PowerPmacDevice>(PowerPmacDeviceName).FindMotionByMotorIndex(motorNo);
            if (axis == null)
            {
                helper.Log(LogLevel.Critical, $"Axis with ID {motorNo} not found.");
            }

            if (helper.IsSimulation)
            {
                helper.Log(LogLevel.Information, $"[Simulation] Homing axis {axis.Name}.");
                return;
            }

            await axis.Home();
            await helper.DelayAsync(100, ct); // Small delay to ensure the home command is processed
            await helper.WaitUntilAsync(
                () => axis.IsHomeDone,
                60000,
                ct,
                $"Axis {axis.Name} Homing Timeout"
            );
        }

        public static async Task<bool> HomeAsync(this ISequenceHelper helper, List<IAxis> axes, CancellationToken ct)
        {
            if (axes == null || axes.Count == 0) throw new ArgumentException("axes is null/empty");

            try
            {
                await Task.WhenAll(axes.Select(a => a.Home())).ConfigureAwait(false);
                await helper.DelayAsync(100, ct).ConfigureAwait(false);

                await Task.WhenAll(axes.Select(a =>
                    helper.WaitUntilAsync(() => a.IsHomeDone, 60000, ct, $"Axis {a.Name} Homing Timeout")
                )).ConfigureAwait(false);

                return true;
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                return false;
            }
        }

        public static async Task MoveAsync(this ISequenceHelper helper, int motorNo, string positionName, CancellationToken ct)
        {
            var axis = helper.DeviceManager.GetDevice<PowerPmacDevice>(PowerPmacDeviceName).FindMotionByMotorIndex(motorNo);
            var position = axis.PositionList.FirstOrDefault(p => p.Name == positionName);

            if (helper.IsSimulation)
            {
                helper.Log(LogLevel.Information, $"[Simulation] {axis.Name} -> {positionName}");
                return;
            }

            // 1. 이동 명령
            await axis.Move(MoveType.Absolute, jerk: 100, position.Speed, position.Position);

            // 2. [중요] 축이 실제로 움직이기 시작할 때까지 대기 (InPosition이 false가 되거나 Busy가 true가 될 때까지)
            // PMAC 통신 속도에 따라 명령 후 상태 반영까지 수 ms~수십 ms가 걸릴 수 있습니다.
            int retry = 0;
            while (axis.InPosition && retry < 10) // 최대 100ms 정도 대기 (상태 변화 확인)
            {
                await Task.Delay(10, ct);
                retry++;
            }

            // 3. 이제 실제로 "도착"할 때까지 대기
            bool result = await helper.WaitUntilAsync(() => axis.InPosition, 60000, ct,
                $"Axis {axis.Name} Move Timeout"
            );
        }

        public static async Task MoveAsync(this ISequenceHelper helper, string motorName, string positionName, CancellationToken ct)
        {
            var axis = helper.DeviceManager.GetDevice<PowerPmacDevice>(PowerPmacDeviceName).FindMotionByName(motorName);
            var position = axis.PositionList.FirstOrDefault(p => p.Name == positionName);
            if (axis == null || position == null)
            {
                helper.Log(LogLevel.Critical, $"Axis with Name {motorName} or Position {positionName} not found.");
                return;
            }
            if (helper.IsSimulation)
            {
                helper.Log(LogLevel.Information, $"[Simulation] Axis {axis.Name} Move to {positionName} at Speed {position.Speed}, Position {position.Position}");
                return;
            }
            await axis.Move(MoveType.Absolute, jerk: 100, position.Speed, position.Position);

            await helper.DelayAsync(100, ct); // Small delay to ensure the move command is processed

            bool result = await helper.WaitUntilAsync( () => axis.InPosition, 60000, ct, $"Axis {axis.Name} Move to {positionName} Timeout" );
            if (!result) throw new Exception("Timeout");
        }

        public static async Task AbsoluteMoveAsync(this ISequenceHelper helper, int motorNo, double velocity, double position, CancellationToken ct)
        {
            var axis = helper.DeviceManager.GetDevice<PowerPmacDevice>(PowerPmacDeviceName).FindMotionByMotorIndex(motorNo);

            if (axis == null)
            {
                helper.Log(LogLevel.Critical, $"Axis with No. {motorNo} not found.");
                return;
            }

            if (helper.IsSimulation)
            {
                helper.Log(LogLevel.Information, $"[Simulation] Axis {axis.Name} Absolute Move to Position {position} at Speed {velocity}");
                return;
            }

            await axis.Move(MoveType.Absolute, jerk: 100, velocity, position);

            await helper.DelayAsync(100, ct); // Small delay to ensure the move command is processed

            await helper.WaitUntilAsync(
                () => axis.InPosition,
                60000,
                ct,
                $"Axis {axis.Name} Move to {position} Timeout"
            );
        }

        public static async Task RelativeMoveAsync(this ISequenceHelper helper, int motorNo, double velocity, double distance, CancellationToken ct)
        {
            var axis = helper.DeviceManager.GetDevice<PowerPmacDevice>(PowerPmacDeviceName).FindMotionByMotorIndex(motorNo);

            if (axis == null)
            {
                helper.Log(LogLevel.Critical, $"Axis with No {motorNo} not found.");
                return;
            }

            if (helper.IsSimulation)
            {
                helper.Log(LogLevel.Information, $"[Simulation] Axis {axis.Name} Relative Move by Distance {distance} at Speed {velocity}");
                return;
            }

            await axis.Move(MoveType.Relative, jerk: 100, velocity, distance);

            await helper.DelayAsync(100, ct); // Small delay to ensure the move command is processed

            await helper.WaitUntilAsync(
                () => axis.InPosition,
                60000,
                ct,
                $"Axis {axis.Name} Relative Move as {distance} Timeout"
            );
        }

        public static async Task<bool> RelativeMoveAsync(this ISequenceHelper helper, string motorName, double velocity, double distance, CancellationToken ct)
        {
            var axis = helper.DeviceManager.GetDevice<PowerPmacDevice>(PowerPmacDeviceName).FindMotionByName(motorName);

            if (axis == null)
            {
                helper.Log(LogLevel.Critical, $"Axis with No {motorName} not found.");
                return false;
            }

            if (helper.IsSimulation)
            {
                helper.Log(LogLevel.Information, $"[Simulation] Axis {axis.Name} Relative Move by Distance {distance} at Speed {velocity}");
                return false;
            }
            velocity = velocity <= 0 ? axis.LimitMaxSpeed + axis.LimitMinSpeed / 2.0 : velocity;

            double targetPosition = axis.CurrentPosition + distance;

            distance = targetPosition > axis.LimitMaxPosition
                       ? axis.LimitMaxPosition - axis.CurrentPosition // 남은 거리만큼만 이동
                       : distance;                                     // 원래 입력된 거리 유지

            await axis.Move(MoveType.Relative, jerk: 100, velocity, distance);

            await helper.DelayAsync(100, ct); // Small delay to ensure the move command is processed

            return await helper.WaitUntilAsync(
                () => axis.InPosition,
                60000,
                ct,
                $"Axis {axis.Name} Relative Move as {distance} Timeout"
            );
        }


    }
}
