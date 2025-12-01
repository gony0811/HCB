using HCB.Data.Entity.Type;
using HCB.Data.Entity;
using System.Linq;

namespace HCB.UI
{
    public static class MotionFactory
    {
        // =============================================================
        // MotionEntity → DMotion
        // =============================================================
        public static DAxis ToRuntime(MotionEntity e, IMotionDevice device)
        {
            var dm = new DAxis
            {
                Id = e.Id,
                Name = e.Name,
                MotorNo = e.MotorNo,
                Unit = e.Unit,

                LimitMinSpeed = e.MinimumSpeed,
                LimitMaxSpeed = e.MaximumSpeed,

                LimitMinPosition = e.MinimumLocation,
                LimitMaxPosition = e.MaximumLocation,

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
                Location = e.Location,
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
                StringValue = e.StringValue,
                IntValue = e.IntValue,
                DoubleValue = e.DoubleValue,
                BoolValue = e.BoolValue,
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
                Location = r.Location,
                MotionId = r.ParentMotion.Id
            };
        }

        public static MotionParameter ToEntity(DMotionParameter r)
        {
            return new MotionParameter
            {
                Id = r.Id,
                Name = r.Name,
                ValueType = r.ValueType,
                StringValue = r.StringValue,
                IntValue = r.IntValue,
                DoubleValue = r.DoubleValue,
                BoolValue = r.BoolValue,
                UnitType = r.Unit,
                MotionId = r.ParentMotion.Id
            };
        }
    }
}
