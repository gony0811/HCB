using HCB.Data.Entity.Type;
using HCB.Data.Entity;
using System;
using System.Collections.Generic;
using HCB.IoC;
using Serilog;

namespace HCB.UI
{

    [Service(Lifetime.Singleton)]
    public class DeviceFactory : IDeviceFactory
    {
        private static readonly Dictionary<DeviceType, Type> _map = new Dictionary<DeviceType, Type>
        {
            { DeviceType.MotionController, typeof(PowerPmacDevice) },
            { DeviceType.IODevice, typeof(PmacIoDevice) },
            //{ DeviceType.Camera, typeof(CanonCameraDevice) },
            //{ DeviceType.IODevice, typeof(BeckhoffIODevice) },
            //{ DeviceType.Laser, typeof(LaserDevice) }
        };

        private readonly ILogger logger;

        public DeviceFactory(ILogger logger)
        {
            this.logger = logger;
        }

        public IDevice Create(Device entity)
        {
            if (!_map.TryGetValue(entity.DeviceType, out var implType))
                throw new Exception($"{entity.DeviceType} 타입에 대한 구현체가 등록되지 않았습니다");

            IDevice device;

            // ILogger를 매개변수로 받는 생성자가 있는지 확인합니다.
            var loggerConstructor = implType.GetConstructor(new[] { typeof(ILogger) });

            if (loggerConstructor != null)
            {
                // ILogger를 받는 생성자가 있으면 로거를 주입하여 인스턴스를 생성합니다.
                device = (IDevice)Activator.CreateInstance(implType, logger);
            }
            else if (implType.GetConstructor(Type.EmptyTypes) != null)
            {
                // 기본 생성자가 있으면 그것을 사용하여 인스턴스를 생성합니다.
                device = (IDevice)Activator.CreateInstance(implType);
            }
            else
            {
                // 적절한 생성자를 찾지 못한 경우 예외를 발생시킵니다.
                throw new InvalidOperationException($"{implType.Name}에 ILogger를 받는 생성자 또는 기본 생성자가 필요합니다.");
            }

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
