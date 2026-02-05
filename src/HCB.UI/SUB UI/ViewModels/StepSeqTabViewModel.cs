using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.IoC;
using HCB.UI.DEVICE.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace HCB.UI
{
    [ViewModel(Lifetime.Singleton)]
    public partial class StepSeqTabViewModel : ObservableObject
    {
        private CancellationTokenSource _cts;
        private readonly SequenceService SequenceService;
        private readonly DeviceManager _deviceManager;
        private IOManager ioManager;
        public SequenceServiceVM SequenceServiceVM { get; }
        private SequenceHelper _sequenceHelper;
        [ObservableProperty]
        private bool isInitializing;

        [ObservableProperty]
        private ObservableCollection<SensorIoItemViewModel> dTableList = new ObservableCollection<SensorIoItemViewModel>();

        private List<string> dTableNameList = new List<string>()
        {
            "DIE 1","DIE 2", "DIE 3", "DIE 4", "DIE 5", "DIE 6", "DIE 7", "DIE 8", "DIE 9",
        };

        private List<string> dIoNameList = new List<string>()
        {
            IoExtensions.DO_DTABLE_VAC_1_ON, IoExtensions.DO_DTABLE_VAC_2_ON, IoExtensions.DO_DTABLE_VAC_3_ON, IoExtensions.DO_DTABLE_VAC_4_ON, IoExtensions.DO_DTABLE_VAC_5_ON, IoExtensions.DO_DTABLE_VAC_6_ON, IoExtensions.DO_DTABLE_VAC_7_ON, IoExtensions.DO_DTABLE_VAC_8_ON, IoExtensions.DO_DTABLE_VAC_9_ON,
        };

        public StepSeqTabViewModel(SequenceServiceVM sequenceServiceVM, SequenceService sequenceService, SequenceHelper sequenceHelper, DeviceManager deviceManager, IOManager ioManager)
        {
            this.SequenceServiceVM = sequenceServiceVM;
            this.SequenceService = sequenceService;
            this._sequenceHelper = sequenceHelper;
            this._deviceManager = deviceManager;
            this.ioManager = ioManager;
            var ioDevice = this._deviceManager.GetDevice<PmacIoDevice>(IoExtensions.IoDeviceName);

            if (ioDevice != null)
            {
                for (var i = 0; i < dTableNameList.Count; i++)
                {
                    var result = ioManager.CreateIoVM(dTableNameList[i], dIoNameList[i], dTableNameList[i]);
                    if (result != null) DTableList.Add(result);

                }
            }
        }

        [RelayCommand(CanExecute = nameof(CanStartInitialize))]
        public async Task Initialize()
        {
            try
            {
                IsInitializing = true;

                // 1. 기존 취소 토큰 정리 및 새 토큰 생성
                _cts?.Cancel();
                _cts = new CancellationTokenSource();

                // 2. 서비스 호출 (생성한 토큰 전달)
                await SequenceService.MachineStartAsync(_cts.Token);

                // 성공 시 알림 등 추가 로직
            }
            catch (OperationCanceledException)
            {
                // 사용자가 Stop을 눌렀을 때의 처리
            }
            catch (Exception ex)
            {
                // 에러 로깅
            }
            finally
            {
                IsInitializing = false;
                InitializeCommand.NotifyCanExecuteChanged();
            }
        }
        // 중지 명령
        [RelayCommand]
        public void Stop()
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel(); // 토큰에 취소 신호 전달
            }
        }

        [RelayCommand(CanExecute = nameof(CanStartInitialize))]
        public async Task DieLoad()
        {
            try
            {
                IsInitializing = true;
                _cts?.Cancel();
                _cts = new CancellationTokenSource();

                await SequenceService.DTableLoading(_cts.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
            }
            finally
            {
                IsInitializing = false;
                InitializeCommand.NotifyCanExecuteChanged();
            }
        }

        [RelayCommand(CanExecute = nameof(CanStartInitialize))]
        public async Task DieComplete()
        {
            try
            {
                IsInitializing = true;
                _cts?.Cancel();
                _cts = new CancellationTokenSource();

                await SequenceService.DTableLoadComplete(_cts.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
            }
            finally
            {
                IsInitializing = false;
                InitializeCommand.NotifyCanExecuteChanged();
            }
        }

        [RelayCommand(CanExecute = nameof(CanStartInitialize))]
        public async Task WaferLoad()
        {
            try
            {
                IsInitializing = true;
                _cts?.Cancel();
                _cts = new CancellationTokenSource();

                await SequenceService.WTableLoading(_cts.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
            }
            finally
            {
                IsInitializing = false;
                InitializeCommand.NotifyCanExecuteChanged();
            }
        }

        [RelayCommand(CanExecute = nameof(CanStartInitialize))]
        public async Task WaferComplete()
        {
            try
            {
                IsInitializing = true;
                _cts?.Cancel();
                _cts = new CancellationTokenSource();

                await SequenceService.WTableLoadComplete(_cts.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
            }
            finally
            {
                IsInitializing = false;
                InitializeCommand.NotifyCanExecuteChanged();
            }
        }

        [RelayCommand(CanExecute = nameof(CanStartInitialize))]
        public async Task WaferAlign() { 
            try
            {
                IsInitializing = true;
                _cts?.Cancel();
                _cts = new CancellationTokenSource();

                await SequenceService.WTableAlign(_cts.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
            }
            finally
            {
                IsInitializing = false;
                InitializeCommand.NotifyCanExecuteChanged();
            }
        }

        [RelayCommand]
        private async Task Step1Start()
        {
            _cts = new CancellationTokenSource();
            await SequenceService.StepMoveWaferCenter(_cts.Token);
            await SequenceService.StepWaferAlign(_cts.Token);
        }

        //[RelayCommand]
        //private void Step1Stop()
        //{
        //    cts?.Cancel();
        //}

        //[RelayCommand]
        //private async Task Step2Start()
        //{
        //    _cts = new CancellationTokenSource();
        //    await SequenceService.StepMoveDTableCenter(_cts.Token);
        //    await SequenceService.StepDieCarrierAlign(_cts.Token);
        //    await SequenceService.StepDiePickUp(    _cts.Token);
        //}

        //[RelayCommand]
        //private void Step2Stop()
        //{
        //    _cts?.Cancel();
        //}

        //[RelayCommand]
        //private async Task Step3Start()
        //{
        //    cts = new CancellationTokenSource();
        //    await SequenceService.StepMovePTableCenter(cts.Token);
        //    await SequenceService.StepLeftFiducialMarkAlign(cts.Token);
        //    await SequenceService.StepRightFiducialMarkAlign(cts.Token);
        //    await SequenceService.StepCalculateFiducialMarkPosition(cts.Token);
        //    await SequenceService.StepLeftDieMarkDetect(cts.Token);
        //    await SequenceService.StepRightDieMarkDetect(cts.Token);
        //    await SequenceService.StepDieAlignment(cts.Token);
        //}

        //[RelayCommand]
        //private void Step3Stop()
        //{
        //    cts?.Cancel();
        //}

        //[RelayCommand]
        //private async Task Step4Start()
        //{
        //    cts = new CancellationTokenSource();
        //    await SequenceService.StepMoveBondingPosition(cts.Token);
        //    await SequenceService.StepWaferLogicMarkDetecting(cts.Token);
        //    await SequenceService.StepDieFinalAlign(cts.Token);
        //    await SequenceService.StepBondingProcess(cts.Token);
        //}

        //[RelayCommand]
        //private void Step4Stop()
        //{
        //    cts?.Cancel();
        //}

        //[RelayCommand]
        //public async Task ServoAllOn(CancellationToken ct)
        //{
        //    await SequenceService.Init_ServoAllOn(ct);
        //}

        //[RelayCommand]
        //public async Task ServoAllOff(CancellationToken ct)
        //{
        //    await SequenceService.Init_ServoAllOff(ct);
        //}

        //[RelayCommand]
        //public async Task WaferPinUp(CancellationToken ct)
        //{
        //    await _sequenceHelper.WTableLiftPin(eUpDown.Up, ct); // W-Table 리프트 핀 다운
        //}
        //[RelayCommand]
        //public async Task WaferPinDown(CancellationToken ct)
        //{
        //    await _sequenceHelper.WTableLiftPin(eUpDown.Down, ct); // W-Table 리프트 핀 다운
        //}

        private bool CanStartInitialize() => !IsInitializing;
    }
}
