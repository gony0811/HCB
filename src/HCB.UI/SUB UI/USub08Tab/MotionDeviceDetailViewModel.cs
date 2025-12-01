
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.Data.Entity.Type;
using HCB.Data.Entity;
using HCB.Data.Interface;
using HCB.Data.Repository;
using HCB.IoC;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace HCB.UI
{
    public interface IDeviceDetailViewModel { }

    public partial class MotionDeviceDetailViewModel : ObservableObject, IDeviceDetailViewModel
    {
        private readonly MotionRepository motionRepository;
        private readonly MotionParameterRepository parameterRepository;
        private readonly MotionPositionRepository positionRepository;

        [ObservableProperty] private IMotionDevice device;

        [ObservableProperty] private IAxis selectedMotion;

        public MotionDeviceDetailViewModel(IMotionDevice device, 
            MotionRepository motionRepository,
            MotionParameterRepository parameterRepository,
            MotionPositionRepository positionRepository)
        {
            this.motionRepository = motionRepository;
            this.parameterRepository = parameterRepository;
            this.positionRepository = positionRepository;

            Device = device;
            SelectedMotion = Device.MotionList.FirstOrDefault();
        }

        [RelayCommand]
        public async Task MotionCreate()
        {
            if (Device == null || Device.Id == 0)
            {
                MessageBox.Show("디바이스를 먼저 저장하세요.");
            }

            var vm = new MotionCreateVM();
            var modal = new CreateModal
            {
				Header = "Device Create",
                DataContext = vm,
                Width = 400,
                Height = 800,
            };

            bool? result = modal.ShowDialog();

            if (result == true)
            {
                var entity = vm.ToEntity();
                entity.ParentDeviceId = Device.Id;
                try
                {
                    var resultEntity = await motionRepository.AddAsync(entity);
                    Device.MotionList.Add(MotionFactory.ToRuntime(resultEntity, Device));
                    
                }catch (Exception e)
                {
                    throw new Exception("모션 생성에 실패했습니다.", e);
                }
            }else
            {
                MessageBox.Show("모션 생성이 취소되었습니다.");
            }
            
        }

        [RelayCommand] 
        public async Task MotionParameterCreate()
        {
            if (SelectedMotion == null ||  SelectedMotion.Id == 0)
            {
                MessageBox.Show("모션을 먼저 선택하세요");
            }

            var vm = new MotionParameterCreateVM();
            var modal = new CreateModal
            {
				Header = "Device Create",
                DataContext = vm,
                Width = 400,
                Height = 800,
            };

            bool? result = modal.ShowDialog();

            if (result == true)
            {
                var entity = vm.ToEntity();
                entity.MotionId = SelectedMotion.Id;
                try
                {
                    var resultEntity = await parameterRepository.AddAsync(entity);
                    var param = new DMotionParameter
                    {
                        Id = resultEntity.Id
                    };

                    //SelectedMotion.ParameterList.Add();



                }
                catch (Exception e)
                {
                    throw new Exception("모션 생성에 실패했습니다.", e);
                }
            }
            else
            {
                MessageBox.Show("모션 생성이 취소되었습니다.");
            }
        }
    }

}
