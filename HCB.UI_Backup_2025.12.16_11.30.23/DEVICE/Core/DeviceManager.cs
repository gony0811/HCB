using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity;
using HCB.Data.Entity.Type;
using HCB.Data.Repository;
using HCB.IoC;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace HCB.UI
{
    [Service(Lifetime.Singleton)]
    public class DeviceManager : ObservableObject
    {
        private readonly ILogger _logger;
        private readonly DeviceRepository _deviceRepository;
        private readonly IDeviceFactory _deviceFactory;

        public DeviceManager(ILogger logger, DeviceRepository deviceRepository, IDeviceFactory deviceFactory)
        {
            _logger = logger.ForContext<DeviceManager>();
            _deviceRepository = deviceRepository;
            _deviceFactory = deviceFactory;

            _ = LoadFromDatabaseAsync();
        }
        public ObservableCollection<IDevice> Devices { get; } = new ObservableCollection<IDevice>();

        public async Task LoadFromDatabaseAsync()
        {
            Clear();

            var deviceEntities = await _deviceRepository.ListAsync(
                include: q => q
                    .Include(d => d.MotionDeviceDetail)
                        .ThenInclude(md => md.MotionList)
                            .ThenInclude(m => m.ParameterList)

                    .Include(d => d.MotionDeviceDetail)
                        .ThenInclude(md => md.MotionList)
                            .ThenInclude(m => m.PositionList)

                    .Include(d => d.IoDeviceDetail)
                        .ThenInclude(id => id.IoDataList)
            );

            foreach (var entity in deviceEntities)
            {
                var dev = CreateRuntimeDevice(entity);
                if (dev != null)
                    Devices.Add(dev);
            }
        }

        #region 디바이스 등록

        public async Task RegisterDevice(IMotionDevice device)
        {
            if (device == null || string.IsNullOrEmpty(device.Name))
                throw new Exception("필수 이름값이 없습니다.");

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

                Device result = await _deviceRepository.AddAsync(entity);
                device.Id = result.Id;

                MotionDeviceDetail detail = new MotionDeviceDetail
                {
                    DeviceId = result.Id,
                    Ip = device.Ip,
                    Port = device.Port,
                    MotionDeviceType = device.MotionDeviceType
                };

                await _deviceRepository.AddMotionDeviceDetail(detail);

                Devices.Add(device);
                MessageBox.Show("저장되었습니다");
            }
            catch
            {
                MessageBox.Show("저장에 실패했습니다");
            }
        }

        public T GetDevice<T>(string name) where T : class, IDevice
        {
            return Devices.FirstOrDefault(d => d.Name == name) as T;
        }

        public async Task RegisterDevice(IIoDevice device)
        {
            if (device == null || string.IsNullOrEmpty(device.Name))
                throw new Exception("필수 이름값이 없습니다.");

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

                Device result = await _deviceRepository.AddAsync(entity);
                device.Id = result.Id;

                IoDeviceDetail detail = new IoDeviceDetail
                {
                    DeviceId = result.Id,
                    Ip = device.Ip,
                    Port = device.Port,
                    IoDeviceType = device.IoDeviceType
                };

                await _deviceRepository.AddIoDeviceDetail(detail);

                Devices.Add(device);
                MessageBox.Show("저장되었습니다");
            }
            catch
            {
                MessageBox.Show("저장에 실패했습니다");
            }
        }

        #endregion

        #region 디바이스 업데이트

        public async Task UpdateDevice(IMotionDevice device)
        {
            if (device == null || device.Id <= 0)
                throw new Exception("잘못된 디바이스입니다.");

            if (device.IsEnabled)
                throw new Exception("사용중인 디바이스는 수정할 수 없습니다.");

            try
            {
                var entity = await _deviceRepository.FindAsync(keyValues: device.Id)
                             ?? throw new Exception("DB에서 디바이스를 찾을 수 없습니다.");

                entity.Name = device.Name;
                entity.FileName = device.FileName;
                entity.InstanceName = device.InstanceName;
                entity.Description = device.Description;

                await _deviceRepository.Update(entity);

                var detail = await _deviceRepository.FindMotionDeviceDetail(device.Id)
                             ?? throw new Exception("Detail 정보를 찾을 수 없습니다.");

                detail.Ip = device.Ip;
                detail.Port = device.Port;
                detail.MotionDeviceType = device.MotionDeviceType;

                await _deviceRepository.UpdateMotionDeviceDetail(detail);

                // runtime 업데이트
                var runtime = Devices.FirstOrDefault(d => d.Id == device.Id) as IMotionDevice;
                if (runtime != null)
                {
                    runtime.Name = device.Name;
                    runtime.FileName = device.FileName;
                    runtime.InstanceName = device.InstanceName;
                    runtime.Description = device.Description;

                    runtime.Ip = device.Ip;
                    runtime.Port = device.Port;
                    runtime.MotionDeviceType = device.MotionDeviceType;
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public async Task UpdateDevice(IIoDevice device)
        {
            if (device == null || device.Id <= 0)
                throw new Exception("잘못된 디바이스입니다.");

            if (device.IsEnabled)
                throw new Exception("사용중인 디바이스는 수정할 수 없습니다.");

            try
            {
                var entity = await _deviceRepository.FindAsync(keyValues: device.Id)
                             ?? throw new Exception("DB에서 디바이스를 찾을 수 없습니다.");

                entity.Name = device.Name;
                entity.FileName = device.FileName;
                entity.InstanceName = device.InstanceName;
                entity.Description = device.Description;

                await _deviceRepository.Update(entity);

                var detail = await _deviceRepository.FindIoDeviceDetail(device.Id)
                             ?? throw new Exception("Detail 정보를 찾을 수 없습니다.");

                detail.Ip = device.Ip;
                detail.Port = device.Port;
                detail.IoDeviceType = device.IoDeviceType;

                await _deviceRepository.UpdateIoDeviceDetail(detail);

                // runtime 업데이트
                var runtime = Devices.FirstOrDefault(d => d.Id == device.Id) as IIoDevice;
                if (runtime != null)
                {
                    runtime.Name = device.Name;
                    runtime.FileName = device.FileName;
                    runtime.InstanceName = device.InstanceName;
                    runtime.Description = device.Description;

                    runtime.Ip = device.Ip;
                    runtime.Port = device.Port;
                    runtime.IoDeviceType = device.IoDeviceType;
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        #endregion

        #region 삭제 / Clear

        public async Task<bool> RemoveDevice(int id)
        {
            var device = Devices.FirstOrDefault(d => d.Id== id);
            if (device == null) return false;
            try
            {
                await _deviceRepository.Remove(device.Id);
            }catch(Exception ex)
            {
                throw new Exception(ex.Message);
            }
            
            Devices.Remove(device);
            return true;
        }

        public void Clear()
        {
            Devices.Clear();
        }

        #endregion

        #region 런타임 생성

        private IDevice CreateRuntimeDevice(Device entity)
        {
            var device = _deviceFactory.Create(entity);

            if (device is IMotionDevice motion && entity.MotionDeviceDetail != null)
            {
                var d = entity.MotionDeviceDetail;
                motion.Ip = d.Ip;
                motion.Port = d.Port;
                motion.MotionDeviceType = d.MotionDeviceType;

                foreach (var m in d.MotionList)
                    motion.MotionList.Add(ConvertToDMotion(m, motion));
            }

            if (device is IIoDevice io && entity.IoDeviceDetail != null)
            {
                var d = entity.IoDeviceDetail;
                io.Ip = d.Ip;
                io.Port = d.Port;
                io.IoDeviceType = d.IoDeviceType;

                foreach (var ioData in d.IoDataList)
                {
                    switch (ioData.IoDataType)
                    {
                        case IoType.DigitalInput:
                            io.IoDataList.Add(new DigitalInput
                            {
                                Id = ioData.Id,
                                Name = ioData.Name,
                                Address = ioData.Address,
                                Index = ioData.Index,
                                IoType = ioData.IoDataType,
                            });
                            break;
                        case IoType.DigitalOutput:
                            io.IoDataList.Add(new DigitalOutput
                            {
                                Id = ioData.Id,
                                Name = ioData.Name,
                                Address = ioData.Address,
                                Index = ioData.Index,
                                IoType = ioData.IoDataType,
                            });
                            break;
                    }
                }
            }

            return device;
        }

        private DAxis ConvertToDMotion(MotionEntity m, IMotionDevice runtime)
        {
            var dm = new DAxis
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
                    Value = p.Value(),
                    Unit = p.UnitType,
                    ParentMotion = dm
                });
            }

            foreach (var p in m.PositionList)
            {
                dm.PositionList.Add(new DMotionPosition
                {
                    Id = p.Id,
                    Name = p.Name,
                    Position = p.Position,
                    Speed = p.Speed,
                    ParentMotion = dm
                });
            }

            return dm;
        }

        #endregion
    }
}
