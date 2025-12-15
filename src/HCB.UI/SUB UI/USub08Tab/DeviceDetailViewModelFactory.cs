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
        private readonly DialogService dialogService;
        private readonly MotionRepository motionRepository;
        private readonly MotionParameterRepository parameterRepository;
        private readonly MotionPositionRepository positionRepository;
        private readonly IoDataRepository ioDataRepository;

		public DeviceDetailViewModelFactory(
            DialogService dialogService,
            MotionRepository motionRepository,
            MotionParameterRepository parameterRepository,
            MotionPositionRepository positionRepository,
            IoDataRepository ioDataRepository)
        {
            this.dialogService = dialogService;
            this.motionRepository = motionRepository;
            this.parameterRepository = parameterRepository;
            this.positionRepository = positionRepository;
            this.ioDataRepository = ioDataRepository;
        }

        public IDeviceDetailViewModel Create(IDevice device)
        {
            if (device is IMotionDevice m)
                return new MotionDeviceDetailViewModel(m,dialogService, motionRepository, parameterRepository, positionRepository);
            else if (device is IIoDevice i)
                return new IoDeviceDetatilViewModel(i, dialogService ,ioDataRepository);
            else
                return null;
        }
    }

}
