using HCB.Data.Entity.Type;
using HCB.Data.Entity;
using System.Linq;
using Serilog;
using System.Runtime.CompilerServices;
using Serilog;

namespace HCB.UI
{
    public static class MotionFactory
    {
        // =============================================================
        // MotionEntity → DMotion
        // =============================================================
        public static DAxis ToRuntime(ILogger logger, MotionEntity e, IMotionDevice device)
        {
            var dm = new DAxis(logger)
            {
                Id = e.Id,
                Name = e.Name,
                MotorNo = e.MotorNo,
                Unit = e.Unit,

                LimitMinSpeed = e.MinimumSpeed,
                LimitMaxSpeed = e.MaximumSpeed,

                LimitMinPosition = e.MinimumLocation,
                LimitMaxPosition = e.MaximumLocation,

                EncoderCountPerUnit = e.EncoderCountsPerUnit,
                HommingProgramNumber = e.HommingProgramNumber,

                Device = device,
            };

            // PositionList 변환
            foreach (var pos in e.PositionList)
            {
                dm.PositionList.Add(ToRuntime(pos, dm));
            }

            // ParameterList 변환
            foreach (var param in e.ParameterList)
            {
                dm.ParameterList.Add(ToRuntime(param, dm));
            }

            return dm;
        }

        // =============================================================
        // MotionPositionEntity → DMotionPosition
        // =============================================================
        public static DMotionPosition ToRuntime(MotionPosition e, IAxis parent)
        {
            return new DMotionPosition
            {
                Id = e.Id,
                Name = e.Name,
                Speed = e.Speed,
                Position = e.Position,
                ParentMotion = parent
            };
        }

        // =============================================================
        // MotionParameterEntity → DMotionParameter
        // =============================================================
        public static DMotionParameter ToRuntime(MotionParameter e, DAxis parent)
        {
            return new DMotionParameter
            {
                Id = e.Id,
                Name = e.Name,
                ValueType = e.ValueType,
                Value = e.Value(),
                Unit = e.UnitType,
                ParentMotion = parent
            };
        }

        // =============================================================
        // DMotion → MotionEntity
        // =============================================================
        public static MotionEntity ToEntity(DAxis r)
        {
            var e = new MotionEntity
            {
                Id = r.Id,
                Name = r.Name,
                MotorNo = r.MotorNo,
                Unit = r.Unit,

                IsEnabled = r.IsEnabled, // 런타임 double → DB bool

                MinimumSpeed = r.LimitMinSpeed,
                MaximumSpeed = r.LimitMaxSpeed,

                MinimumLocation = r.LimitMinPosition,
                MaximumLocation = r.LimitMaxPosition,

                EncoderCountsPerUnit = r.EncoderCountPerUnit,
                HommingProgramNumber = r.HommingProgramNumber,

                ParentDeviceId = r.Device.Id
            };

            // DMotionPosition → MotionPosition
            foreach (var pos in r.PositionList)
            {
                e.PositionList.Add(ToEntity(pos));
            }

            // DMotionParameter → MotionParameter
            foreach (var param in r.ParameterList)
            {
                e.ParameterList.Add(ToEntity(param));
            }

            return e;
        }

        public static MotionPosition ToEntity(DMotionPosition r)
        {
            return new MotionPosition
            {
                Id = r.Id,
                Name = r.Name,
                Speed = r.Speed,
                Position = r.Position,
                MotionId = r.ParentMotion.Id
            };
        }

        public static MotionParameter ToEntity(DMotionParameter r)
        {
            var result = new MotionParameter
            {
                Id = r.Id,
                Name = r.Name,
                ValueType = r.ValueType,
                UnitType = r.Unit,
                MotionId = r.ParentMotion.Id
            };

            switch (result.ValueType)
            {
                case ValueType.Boolean:
                    result.BoolValue = bool.TryParse(r.Value?.ToString(), out var b) ? b : null;
                    break;

                case ValueType.Integer:
                    result.IntValue = int.TryParse(r.Value?.ToString(), out var i) ? i : null;
                    break;

                case ValueType.String:
                    result.StringValue = r.Value?.ToString();
                    break;

                case ValueType.Double:
                case ValueType.Float:
                    result.DoubleValue = double.TryParse(r.Value?.ToString(), out var d) ? d : null;
                    break;
            }


            return result;
        }
    }
}
