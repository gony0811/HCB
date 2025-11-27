using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity.Type;
using HCB.Data.Entity;
using HCB.IoC;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using HCB.Data.Repository;

namespace HCB.UI
{
    [Service(Lifetime.Singleton)]
    public class DeviceManager : ObservableObject
    {
        private readonly DeviceRepository deviceRepository;
        public DeviceManager(DeviceRepository deviceRepository)
        {
            this.deviceRepository = deviceRepository;
            _ = LoadFromDatabaseAsync();
        }

        // 내부 Dictionary (검색/접근 빠름)
        private readonly Dictionary<string, IDevice> _deviceMap = new Dictionary<string, IDevice>();

        // UI에 바인딩되는 ObservableCollection
        public ObservableCollection<IDevice> Devices { get; } = new ObservableCollection<IDevice>();

        public async Task LoadFromDatabaseAsync()
        {
            Clear();
            var deviceEntities = await deviceRepository.ListAsync(
                            include: q => q
                                .Include(d => d.MotionDeviceDetail)
                                    .ThenInclude(md => md.MotionList)
                                        .ThenInclude(m => m.ParameterList)
                                .Include(d => d.MotionDeviceDetail)
                                    .ThenInclude(md => md.MotionList)
                                        .ThenInclude(m => m.PositionList)
            );

            foreach (var entity in deviceEntities)
            {
                var dev = CreateRuntimeDevice(entity);
                if (dev != null)
                    Devices.Add(dev);
            }

            var dd = Devices;
        }

        #region 디바이스 등록 ( 타입별 ) 
        public async Task RegisterDevice(IMotionDevice device)
        {
            if (device == null || string.IsNullOrEmpty(device.Name))
                throw new Exception("필수 이름값이 없습니다.");

            if (_deviceMap.ContainsKey(device.Name))
                throw new Exception("같은 이름이 존재합니다.");

            try
            {
                Device entity = new Device
                {
                    Name = device.Name,
                    DeviceType = device.DeviceType,
                    FileName = device.FileName,
                    InstanceName = device.InstanceName,
                    IsEnabled = device.IsEnabled,
                    Description = device.Description
                };
                Device result = await deviceRepository.AddAsync(entity);
                device.Id = result.Id;


                MotionDeviceDetail detail = new MotionDeviceDetail
                {
                    DeviceId = result.Id,
                    Ip = device.Ip,
                    Port = device.Port,
                    MotionDeviceType = device.MotionDeviceType
                };

                MotionDeviceDetail detailResult = await deviceRepository.AddMotionDeviceDetail(detail);
              
                _deviceMap.Add(device.Name, device);
                Devices.Add(device);
                MessageBox.Show("저장되었습니다");
            }
            catch (Exception ex)
            {
                MessageBox.Show("저장에 실패했습니다");
            }
        }
        #endregion

        #region 디바이스 검색 

        public T GetDevice<T>(string name) where T : class, IDevice
        {
            if (_deviceMap.TryGetValue(name, out var device))
                return device as T;
            return null;
        }
        #endregion

        public bool RemoveDevice(string name)
        {
            if (!_deviceMap.TryGetValue(name, out var device))
                return false;

            _deviceMap.Remove(name);
            Devices.Remove(device);
            return true;
        }

        public void Clear()
        {
            _deviceMap.Clear();
            Devices.Clear();
        }
        private IDevice CreateRuntimeDevice(Device entity)
        {
            var device = DeviceFactory.Create(entity);

            // MotionController의 경우 추가 상세 매핑
            if (device is IMotionDevice motion && entity.MotionDeviceDetail != null)
            {
                var d = entity.MotionDeviceDetail;

                motion.Ip = d.Ip;
                motion.Port = d.Port;
                motion.MotionDeviceType = d.MotionDeviceType;

                // motion.MotionList 생성 코드 그대로 사용
                foreach (var m in d.MotionList)
                {
                    motion.MotionList.Add(ConvertToDMotion(m, motion));
                }
            }

            return device;
        }

        private DMotion ConvertToDMotion(MotionEntity m, IMotionDevice runtime)
        {
            var dm = new DMotion
            {
                Id = m.Id,
                Name = m.Name,
                MotorNo = m.MotorNo,
                Unit = m.Unit,
                LimitMinSpeed = m.MinimumSpeed,
                LimitMaxSpeed = m.MaximumSpeed,
                LimitMinPosition = m.MinimumLocation,
                LimitMaxPosition = m.MaximumLocation,
                Device = runtime
            };

            foreach (var p in m.ParameterList)
            {
                dm.ParameterList.Add(new DMotionParameter
                {
                    Id = p.Id,
                    Name = p.Name,
                    ValueType = p.ValueType,
                    StringValue = p.StringValue,
                    IntValue = p.IntValue,
                    DoubleValue = p.DoubleValue,
                    BoolValue = p.BoolValue,
                    Unit = p.UnitType,
                    ParentMotion = dm
                });
            }

            foreach (var pos in m.PositionList)
            {
                dm.PositionList.Add(new DMotionPosition
                {
                    Id = pos.Id,
                    Name = pos.Name,
                    Speed = pos.Speed,
                    Location = pos.Location,
                    ParentMotion = dm
                });
            }

            return dm;
        }
    }
}
