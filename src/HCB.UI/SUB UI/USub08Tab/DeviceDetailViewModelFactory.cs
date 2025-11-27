using HCB.IoC;
using HCB.Data.Repository;

namespace HCB.UI
{

    public interface IDeviceDetailViewModelFactory
    {
        IDeviceDetailViewModel Create(IDevice device);
    }
    [Service(Lifetime.Singleton)] // Autofac 자동 등록
    public class DeviceDetailViewModelFactory : IDeviceDetailViewModelFactory
    {
        private readonly MotionRepository motionRepository;
        private readonly MotionParameterRepository parameterRepository;
        private readonly MotionPositionRepository positionRepository;

        public DeviceDetailViewModelFactory(
            MotionRepository motionRepository,
            MotionParameterRepository parameterRepository,
            MotionPositionRepository positionRepository)
        {
            this.motionRepository = motionRepository;
            this.parameterRepository = parameterRepository;
            this.positionRepository = positionRepository;
        }

        public IDeviceDetailViewModel Create(IDevice device)
        {
            if (device is IMotionDevice m)
                return new MotionDeviceDetailViewModel(m, motionRepository, parameterRepository, positionRepository);

            return null;
        }
    }

}
