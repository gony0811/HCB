

using HCB.Data.Entity.Type;
using HCB.Data.Entity;
using System;
using System.Collections.Generic;

namespace HCB.UI
{
    public static class DeviceFactory
    {
        private static readonly Dictionary<DeviceType, Type> _map = new Dictionary<DeviceType, Type>
        {
            { DeviceType.MotionController, typeof(PowerPmacDevice) },
            //{ DeviceType.Camera, typeof(CanonCameraDevice) },
            //{ DeviceType.IODevice, typeof(BeckhoffIODevice) },
            //{ DeviceType.Laser, typeof(LaserDevice) }
        };

        public static IDevice Create(Device entity)
        {
            if (!_map.TryGetValue(entity.DeviceType, out var implType))
                throw new Exception($"{entity.DeviceType} 타입에 대한 구현체가 등록되지 않았습니다");

            var device = (IDevice)Activator.CreateInstance(implType);

            // 공통 필드 매핑
            device.Id = entity.Id;
            device.Name = entity.Name;
            device.DeviceType = entity.DeviceType;
            device.FileName = entity.FileName;
            device.InstanceName = entity.InstanceName;
            device.Description = entity.Description;
            device.IsEnabled = entity.IsEnabled;

            return device;
        }
    }

}
