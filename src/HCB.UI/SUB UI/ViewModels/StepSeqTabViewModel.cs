using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.IoC;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace HCB.UI
{
    [ViewModel(Lifetime.Singleton)]
    public partial class StepSeqTabViewModel : ObservableObject
    {
        private CancellationTokenSource _cts;
        private readonly SequenceService SequenceService;
        private readonly DialogService _dialogService;
        private readonly DeviceManager _deviceManager;
        private IOManager ioManager;

        public SequenceServiceVM SequenceServiceVM { get; }
        private SequenceHelper _sequenceHelper;
        [ObservableProperty]
        private bool isInitializing;

        private List<DieData> _waferData;
        public List<DieData> WaferData
        {
            get => _waferData;
            set { _waferData = value; OnPropertyChanged(); }
        }

        public ObservableCollection<string> WaferSizeList { get; } =
                new ObservableCollection<string> { "12x12", "4x4" };

        [ObservableProperty]
        private string selectedWaferSize;

        [ObservableProperty]
        private ImageSource waferImage;

        [ObservableProperty]
        private ObservableCollection<SensorIoItemViewModel> dTableList = new ObservableCollection<SensorIoItemViewModel>();
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private List<string> dTableNameList = new List<string>()
        {
            "DIE 1","DIE 2", "DIE 3", "DIE 4", "DIE 5", "DIE 6", "DIE 7", "DIE 8", "DIE 9",
        };

        private List<string> dIoNameList = new List<string>()
        {
            IoExtensions.DO_DTABLE_VAC_1_ON, IoExtensions.DO_DTABLE_VAC_2_ON, IoExtensions.DO_DTABLE_VAC_3_ON, IoExtensions.DO_DTABLE_VAC_4_ON, IoExtensions.DO_DTABLE_VAC_5_ON, IoExtensions.DO_DTABLE_VAC_6_ON, IoExtensions.DO_DTABLE_VAC_7_ON, IoExtensions.DO_DTABLE_VAC_8_ON, IoExtensions.DO_DTABLE_VAC_9_ON,
        };

        public StepSeqTabViewModel(SequenceServiceVM sequenceServiceVM, SequenceService sequenceService, SequenceHelper sequenceHelper, DeviceManager deviceManager, IOManager ioManager, DialogService dialogService)
        {
            this.SequenceServiceVM = sequenceServiceVM;
            this.SequenceService = sequenceService;
            this._sequenceHelper = sequenceHelper;
            this._deviceManager = deviceManager;
            this.ioManager = ioManager;
            this._dialogService = dialogService;

            var ioDevice = this._deviceManager.GetDevice<PmacIoDevice>(IoExtensions.IoDeviceName);

            //if (ioDevice != null)
            //{
            //    for (var i = 0; i < dTableNameList.Count; i++)
            //    {
            //        var result = ioManager.CreateIoVM(dTableNameList[i], dIoNameList[i], dTableNameList[i]);
            //        if (result != null) DTableList.Add(result);
                    
            //    }
            //}

            SelectedWaferSize = "12x12";   // 기본 선택
            ApplyWaferSize(SelectedWaferSize);
        }

        [RelayCommand(CanExecute = nameof(CanStartInitialize))]
        public async Task Initialize()
        {
            try
            {
                IsInitializing = true;
                SequenceServiceVM.SystemCheck = StepState.Idle;
                SequenceServiceVM.ServoOn = StepState.Idle;
                SequenceServiceVM.HZBreakOff= StepState.Idle;
                SequenceServiceVM.HZHome = StepState.Idle;
                SequenceServiceVM.HzHome = StepState.Idle;
                SequenceServiceVM.HXHome = StepState.Idle;
                SequenceServiceVM.HTHome= StepState.Idle;
                SequenceServiceVM.DYHome= StepState.Idle;
                SequenceServiceVM.PYHome= StepState.Idle;
                SequenceServiceVM.WYHome= StepState.Idle;
                SequenceServiceVM.WTHome= StepState.Idle;
                await Task.Delay(1000);
                await this.SequenceService.MachineInitAsync(_cancellationTokenSource.Token);
            }catch(Exception e)
            {
                
            }
            finally
            {
                IsInitializing = false;
            }
        }
        // 중지 명령
        [RelayCommand]
        public void Stop()
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel(); // 토큰에 취소 신호 전달
                SequenceServiceVM.SystemCheck = StepState.Idle;
                SequenceServiceVM.ServoOn = StepState.Idle;
                SequenceServiceVM.HZBreakOff = StepState.Idle;
                SequenceServiceVM.HZHome = StepState.Idle;
                SequenceServiceVM.HzHome = StepState.Idle;
                SequenceServiceVM.HXHome = StepState.Idle;
                SequenceServiceVM.HTHome = StepState.Idle;
                SequenceServiceVM.DYHome = StepState.Idle;
                SequenceServiceVM.PYHome = StepState.Idle;
                SequenceServiceVM.WYHome = StepState.Idle;
                SequenceServiceVM.WTHome = StepState.Idle;
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
                var result = await _dialogService.ShowConfirmAsync("DIE 로딩중", "로딩이 완료된 후 확인을 눌러주세요");
                if (result)
                {
                    await DieComplete();
                }
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
        private List<DieData> GenerateWafer(int rows, int cols)
        {
            var list = new List<DieData>();
            double centerX = cols / 2.0;
            double centerY = rows / 2.0;
            double radius = Math.Min(rows, cols) / 2.0;

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    // 원형 웨이퍼 영역 안에 있는지 계산 (피타고라스 정리)
                    double distance = Math.Sqrt(Math.Pow(c - centerX, 2) + Math.Pow(r - centerY, 2));

                    if (distance < radius)
                    {
                        list.Add(new DieData
                        {
                            Row = r,
                            Col = c,
                            DieBrush = Brushes.DimGray, // 기본 색상
                            Information = $"Die [{r}, {c}] - Ready"
                        });
                    }
                }
            }
            return list;
        }

        // 특정 조건(예: 테스트 완료)에 따라 데이터를 한 번에 업데이트하는 예시
        [RelayCommand]
        public void UpdateTestResults()
        {
            var random = new Random();
            // 기존 리스트를 바탕으로 새로운 리스트 생성 (참조 변경)
            var newList = new List<DieData>(WaferData);

            foreach (var die in newList)
            {
                die.DieBrush = random.Next(0, 10) > 1 ? Brushes.LimeGreen : Brushes.Red;
                die.Information = die.DieBrush == Brushes.Red ? "Status: Fail" : "Status: Pass";
            }

            // 새로운 리스트 주소를 할당 -> Dependency Property 콜백 실행됨
            WaferData = newList;
        }

        partial void OnSelectedWaferSizeChanged(string value)
        {
            ApplyWaferSize(value);
        }

        private void ApplyWaferSize(string size)
        {
            switch (size)
            {
                case "12x12":
                    WaferData = GenerateWafer(12, 12);
                    break;

                case "4x4":
                    WaferData = GenerateWafer(4, 4);
                    break;
            }
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
