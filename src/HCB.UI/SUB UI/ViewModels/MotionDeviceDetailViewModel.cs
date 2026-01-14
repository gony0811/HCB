
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.Data.Entity;
using HCB.Data.Entity.Type;
using HCB.Data.Interface;
using HCB.Data.Repository;
using HCB.IoC;
using Serilog;
using SharpDX.Direct3D9;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;
using ValueType = HCB.Data.Entity.Type.ValueType;

namespace HCB.UI
{
    public interface IDeviceDetailViewModel { }

    public partial class MotionDeviceDetailViewModel : ObservableObject, IDeviceDetailViewModel
    {
        private readonly ILogger logger;
        private readonly DialogService dialogService;
        private readonly MotionRepository motionRepository;
        private readonly MotionParameterRepository parameterRepository;
        private readonly MotionPositionRepository positionRepository;

        [ObservableProperty] private IMotionDevice device;

        [ObservableProperty] private IAxis selectedMotion;
        [ObservableProperty] private DMotionParameter selectedParam;

        public MotionDeviceDetailViewModel(
            ILogger logger,
            IMotionDevice device, 
            DialogService dialogService,
            MotionRepository motionRepository,
            MotionParameterRepository parameterRepository,
            MotionPositionRepository positionRepository)
        {
            this.logger = logger.ForContext<MotionDeviceDetailViewModel>();
            this.dialogService = dialogService;
            this.motionRepository = motionRepository;
            this.parameterRepository = parameterRepository;
            this.positionRepository = positionRepository;

            Device = device;
            SelectedMotion = Device.MotionList.FirstOrDefault();
            SelectedParam = SelectedMotion != null ? SelectedMotion.ParameterList.FirstOrDefault() : null;
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
                    Device.MotionList.Add(MotionFactory.ToRuntime(this.logger, resultEntity, Device));
                    
                }catch (Exception e)
                {
                    dialogService.ShowMessage("모션 생성에 실패했습니다.", e.Message);
                }
            }else
            {
                dialogService.ShowMessage("취소", "모션 생성을 취소했습니다");
            }
            
        }


        [RelayCommand]
        public async Task MotionUpdate()
        {
            if (SelectedMotion == null || SelectedMotion.Id == 0)
            {
                dialogService.ShowMessage("모션 선택", "모션을 먼저 선택하세요");
            }

            var dto = Device.MotionList.FirstOrDefault(x => x.Id == SelectedMotion.Id);
            if (dto == null)
            {
                dialogService.ShowMessage("에러", "잘못된 모션 정보입니다");
            }
            var vm = new MotionCreateVM
            {
                Name = SelectedMotion.Name,
                MotorNo = SelectedMotion.MotorNo,
                MaximumLocation = SelectedMotion.LimitMaxPosition,
                MinimumLocation = SelectedMotion.LimitMinPosition,
                MaximumSpeed = SelectedMotion.LimitMaxSpeed,
                MinimumSpeed = SelectedMotion.LimitMinSpeed,
                Unit = SelectedMotion.Unit
            };

            bool? result = await dialogService.ShowEditDialog(vm);
            if(result == true)
            {
                var entity = vm.ToEntity();
                entity.Id = SelectedMotion.Id;
                entity.ParentDeviceId = Device.Id;
                try
                {
                    await motionRepository.Update(entity);
                    dto.Name = entity.Name;
                    dto.MotorNo = entity.MotorNo;
                    dto.LimitMaxPosition = entity.MaximumLocation;
                    dto.LimitMinPosition = entity.MinimumLocation;
                    dto.LimitMaxSpeed = entity.MaximumSpeed;
                    dto.LimitMinSpeed = entity.MinimumSpeed;
                    dto.Unit = entity.Unit;

                    dialogService.ShowMessage("업데이트 완료", "모션이 저장 되었습니다");
                }catch(Exception ex)
                {
                    dialogService.ShowMessage("에러", "모션 업데이트 중 에러 발생");
                }
            }
            
        }

        [RelayCommand] 
        public async Task MotionParameterCreate()
        {
            if (SelectedMotion == null ||  SelectedMotion.Id == 0)
            {
                dialogService.ShowMessage("모션 선택", "모션을 먼저 선택하세요");
            }

            var vm = new MotionParameterCreateVM();

            bool? result = await dialogService.ShowEditDialog(vm);
            
            if (result == true)
            {
                var entity = vm.ToEntity();
                entity.MotionId = SelectedMotion.Id;
                try
                {
                    var resultEntity = await parameterRepository.AddAsync(entity);

                    var param = new DMotionParameter
                    {
                        Id = resultEntity.Id,
                        Name = resultEntity.Name,
                        ValueType = resultEntity.ValueType,
                        Value = resultEntity.Value(),
                        Unit = resultEntity.UnitType
                    };

                    SelectedMotion.ParameterList.Add(param);

                }
                catch (Exception e)
                {
                    dialogService.ShowMessage("파라미터 생성 실패", e.Message);
                }
            }
            else
            {
                dialogService.ShowMessage("취소", "파라미터 생성 취소");
            }
        }

        [RelayCommand]
        public async Task MotionParameterUpdate()
        {
            if (SelectedMotion == null || SelectedMotion.Id == 0)
            {
                dialogService.ShowMessage("에러", "모션을 먼저 선택하세요");
                return;
            }

            if (SelectedParam == null || SelectedParam.Id == 0)
            {
                dialogService.ShowMessage("선택 오류", "수정할 파라미터를 선택하세요");
                return;
            }
            

            // 수정용 ViewModel 생성
            var vm = new MotionParameterCreateVM
            {
                Name = SelectedParam.Name,
                ValueType = SelectedParam.ValueType,
                Unit = SelectedParam.Unit,
                Value = SelectedParam.Value    // object 값 그대로
            };

            bool? result = await dialogService.ShowEditDialog(vm);
            if (result != true)
                return;

            try
            {
                // DB 업데이트용 엔티티 생성
                var entity = vm.ToEntity();
                entity.Id = SelectedParam.Id;
                entity.MotionId = SelectedMotion.Id;

                await parameterRepository.Update(entity);

                // 런타임 값 업데이트
                SelectedParam.Name = entity.Name;
                SelectedParam.ValueType = entity.ValueType;

                SelectedParam.Unit = entity.UnitType;

                // Value 업데이트
                SelectedParam.Value = entity.Value();

                dialogService.ShowMessage("성공", "파라미터가 수정되었습니다.");
            }
            catch (Exception ex)
            {
                dialogService.ShowMessage("에러", ex.Message);
            }
        }

        [RelayCommand]
        public async Task MotionParameterDelete()
        {
            if (SelectedMotion == null || SelectedMotion.Id == 0)
            {
                dialogService.ShowMessage("에러", "모션을 먼저 선택하세요");
                return;
            }
            if (SelectedParam == null || SelectedParam.Id == 0)
            {
                dialogService.ShowMessage("선택 오류", "삭제할 파라미터를 선택하세요");
                return;
            }

            if (MessageBox.Show($"파라미터 '{SelectedParam.Name}' 을(를) 삭제하시겠습니까?",
                                "삭제 확인",
                                MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return;

            try
            {
                // DB 삭제
                await parameterRepository.Remove(SelectedParam.Id);

                // 런타임 리스트에서 제거
                SelectedMotion.ParameterList.Remove(SelectedParam);

                dialogService.ShowMessage("삭제 완료", "파라미터가 삭제되었습니다.");
            }
            catch (Exception ex)
            {
                dialogService.ShowMessage("삭제 실패", ex.Message);
            }
        }
    }

}
