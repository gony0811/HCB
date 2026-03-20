using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.IoC;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Xml.Linq;
using Telerik.Windows.Documents.Fixed.Model.Preferences;
using Telerik.Windows.Documents.Spreadsheet.Expressions.Functions;
using static HCB.UI.SERVICE.CalibrationService;

namespace HCB.UI
{
    [ViewModel(Lifetime.Singleton)]
    public partial class StepSeqTabViewModel : ObservableObject
    {
        private CancellationTokenSource _cts;
        private readonly ILogger _logger;
        private readonly SequenceService SequenceService;
        private readonly DialogService _dialogService;
        private readonly DeviceManager _deviceManager;
        private readonly EqpCommunicationService eqpCommunicationService;
        private readonly CalibrationViewModel calibrationViewModel;
        private IOManager ioManager;

        [ObservableProperty]
        private int topDie = 0;

        [ObservableProperty]
        private int bottomDie = 0;

        [ObservableProperty]
        private int pressureTime = 1000;

        [ObservableProperty]
        private int blowTime = 1000;

        [ObservableProperty]
        private int waitTime = 8000;


        [ObservableProperty]
        private VisionMarkPositionResponse visionBtmLowAlign;

        [ObservableProperty]
        private VisionMarkPositionResponse visionTopLowAlign;

        [ObservableProperty]
        private VisionMarkResult topRightAlign;

        [ObservableProperty]
        private VisionMarkResult topRightFid;

        [ObservableProperty]
        private VisionMarkResult topLeftAlign;

        [ObservableProperty]
        private VisionMarkResult topLeftFid;

        [ObservableProperty]
        private double errorX;
        
        [ObservableProperty]
        private double errorY;

        [ObservableProperty]
        private double errorT;

        [ObservableProperty]
        private DieMeasurement topDieAlign;

        [ObservableProperty]
        private DieMeasurement topDieFid;


        [ObservableProperty]
        private DieMeasurement bottomDieAlign;

        [ObservableProperty]
        private DieMeasurement bottomDieFid;

        public SequenceServiceVM SequenceServiceVM { get; }
        private SequenceHelper _sequenceHelper;
        [ObservableProperty]
        private bool isInitializing;

        [ObservableProperty]
        private ObservableCollection<DieType> _dieTypeList = new ObservableCollection<DieType>
        {
            DieType.TOP,
            DieType.BOTTOM
        };

        [ObservableProperty]
        private ObservableCollection<MarkType> _markTypeList = new ObservableCollection<MarkType>
        {
            MarkType.ALIGN_MARK,
            MarkType.FIDUCIAL
        };

        [ObservableProperty]
        private ObservableCollection<DirectType> _directTypeList = new ObservableCollection<DirectType>
        {
            DirectType.LEFT,
            DirectType.RIGHT,
        };
        [ObservableProperty]
        private DirectType _selectedDirectType = DirectType.LEFT;

        [ObservableProperty]
        private MarkType _selectedMarkType = MarkType.ALIGN_MARK;

        [ObservableProperty]
        private DieType _selectedDieType = DieType.BOTTOM;

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

        public StepSeqTabViewModel(SequenceServiceVM sequenceServiceVM, SequenceService sequenceService, SequenceHelper sequenceHelper, DeviceManager deviceManager, IOManager ioManager, DialogService dialogService
           , EqpCommunicationService eqpCommunicationService, CalibrationViewModel calibrationViewModel, ILogger logger
            )
        {
            _logger = logger.ForContext<StepSeqTabViewModel>();
            this.SequenceServiceVM = sequenceServiceVM;
            this.SequenceService = sequenceService;
            this._sequenceHelper = sequenceHelper;
            this._deviceManager = deviceManager;
            this.ioManager = ioManager;
            this._dialogService = dialogService;
            this.eqpCommunicationService = eqpCommunicationService;
            this.calibrationViewModel = calibrationViewModel;
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
                await Task.Delay(1000);
                await this.SequenceService.MachineInitAsync(_cancellationTokenSource.Token);
            }
            catch (Exception e)
            {

            }
            finally
            {
                IsInitializing = false;
            }
        }
        // 중지 명령
        [RelayCommand]
        public async Task Stop()
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel(); // 토큰에 취소 신호 전달
                await SequenceService.StopAsync(_cts.Token);
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
                _cts?.Cancel();
                _cts = new CancellationTokenSource();
                await SequenceService.MotionsMove(MotionExtensions.D_Y, MotionExtensions.LOAD_POSITION, _cts.Token);
            }
            catch(Exception e)
            {

            }

            var tcs = new TaskCompletionSource<bool>();

            var dialog = new VacuumSelector();
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            dialog.Closed += (s, e) =>
            {
                if (dialog.DialogResult == true
                    && dialog.TopDieVacuum.HasValue
                    && dialog.BotDieVacuum.HasValue)
                {
                    TopDie = dialog.TopDieVacuum.Value;
                    BottomDie = dialog.BotDieVacuum.Value;
                    DTableList[TopDie-1].On();
                    DTableList[BottomDie-1].On();
                    tcs.SetResult(true);
                }
                else
                {
                    tcs.SetResult(false);
                }
            };

            dialog.ShowDialog(); // UI 스레드에서 직접 호출

            bool confirmed = await tcs.Task;
            if (!confirmed) return;
        }

        [RelayCommand(CanExecute = nameof(CanStartInitialize))]
        public void DieReset()
        {
            try
            {
                _cts?.Cancel();
                _cts = new CancellationTokenSource();
                
                if (TopDie != 0)
                {
                    DTableList[TopDie - 1].Off();
                }
                if (BottomDie != 0)
                {
                    DTableList[BottomDie - 1].Off();
                }
            }
            catch (Exception e)
            {

            }
        }

        [RelayCommand(CanExecute = nameof(CanStartInitialize))]
        public async Task DieLowAlign()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            try
            {
                switch (SelectedDieType)
                {
                    case DieType.BOTTOM:
                        if (BottomDie == 0)
                        {
                            _logger.Information("Bottom Die를 Load해주세요");
                            return;
                        }
                        VisionBtmLowAlign = await SequenceService.DTableCarrierAlign(
                            BottomDie, MarkType.DIE_CENTER_BOTTOM, _cts.Token);
                        break;

                    case DieType.TOP:
                        if (TopDie == 0)
                        {
                            _logger.Information("Top Die를 Load해주세요");
                            return;
                        }
                        VisionTopLowAlign = await SequenceService.DTableCarrierAlign(
                            TopDie, MarkType.DIE_CENTER_TOP, _cts.Token);
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Information("DieLowAlign 작업이 취소되었습니다");
            }
            catch (Exception e)
            {
                _logger.Warning(e.Message);
            }
        }

        [RelayCommand(CanExecute = nameof(CanStartInitialize))]
        public async Task DiePickup()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            try
            {
                switch (SelectedDieType)
                {
                    case DieType.BOTTOM:
                        if (BottomDie == 0)
                        {
                            _logger.Information("Bottom Die를 Load해주세요");
                            return;
                        }
                        if (VisionBtmLowAlign == null)
                        {
                            _logger.Information("Bottom Die Align 해주세요");
                            return;
                        }
                        await SequenceService.DTableBTMPickup(BottomDie, VisionBtmLowAlign, _cts.Token);
                        DTableList[BottomDie - 1].Off();
                        //BottomDie = 0;
                        //VisionBtmLowAlign = null;
                        break;

                    case DieType.TOP:
                        if (TopDie == 0)
                        {
                            _logger.Information("Top Die를 Load해주세요");
                            return;
                        }
                        if (VisionTopLowAlign == null)
                        {
                            _logger.Information("Top Die Align 해주세요");
                            return;
                        }
                        await SequenceService.DTableTOPPickup(TopDie, VisionTopLowAlign, _cts.Token);
                        DTableList[TopDie - 1].Off();
                        //TopDie = 0;
                        //VisionTopLowAlign = null;
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Information("DiePickup 작업이 취소되었습니다");
            }
            catch (Exception e)
            {
                _logger.Warning(e.Message);
            }
        }

        [RelayCommand(CanExecute = nameof(CanStartInitialize))]
        public async Task DiePlace()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            try
            {
                switch (SelectedDieType)
                {
                    case DieType.BOTTOM:
                        await SequenceService.BtmDieDrop(1, _cts.Token);
                        break;

                    case DieType.TOP:
                        ErrorX = 0;
                        ErrorY = 0;
                        ErrorT = 0;
                        Dictionary<string, VisionMarkResult> topVisionMarkResult = new Dictionary<string, VisionMarkResult>
                        {
                            { "RIGHT_FID", TopRightFid },
                            { "RIGHT_ALIGN", TopRightAlign },
                            { "LEFT_FID", TopLeftFid },
                            { "LEFT_ALIGN", TopLeftAlign }
                        }; 

                        int[] delayTimes = { PressureTime, BlowTime, WaitTime };

                        //var result = await SequenceService.TopDieDrop(topVisionMarkResult, _cts.Token, delayTimes);
                        await SequenceService.TopDieDrop(_cts.Token, delayTimes);
                        //ErrorX = result.moveX;
                        //ErrorY = result.moveY;
                        //ErrorT = result.moveTheta;

                        //TopDieAlign.RightX = xyt.rightAlign.StageX;
                        //TopDieAlign.RightXError= xyt.rightAlign.DxCamToMark;
                        //TopDieAlign.RightXCorrect = xyt.rightAlign.CenterX;

                        //TopDieAlign.RightY = xyt.rightAlign.StageY;
                        //TopDieAlign.RightYError = xyt.rightAlign.DyCamToMark;
                        //TopDieAlign.RightYCorrect = xyt.rightAlign.CenterY;

                        //TopDieAlign.RightX = xyt.rightAlign.StageX;
                        //TopDieAlign.RightXError = xyt.rightAlign.DxCamToMark;
                        //TopDieAlign.RightXCorrect = xyt.rightAlign.CenterX;

                        //TopDieAlign.RightX = xyt.rightAlign.StageX;
                        //TopDieAlign.RightXError = xyt.rightAlign.DxCamToMark;
                        //TopDieAlign.RightXCorrect = xyt.rightAlign.CenterX;

                        //TopDieAlign.RightX = xyt.rightAlign.StageX;
                        //TopDieAlign.RightXError = xyt.rightAlign.DxCamToMark;
                        //TopDieAlign.RightXCorrect = xyt.rightAlign.CenterX;

                        break;
                }
                
            }
            catch (OperationCanceledException)
            {
                _logger.Information("DiePickup 작업이 취소되었습니다");
            }
            catch (Exception e)
            {
                _logger.Warning(e.Message);
            }
        }

        [RelayCommand]
        public async Task HeaderTest()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            await _sequenceHelper.HeadPickerVacuum(eOnOff.Off, _cts.Token);
        }


        [RelayCommand(CanExecute = nameof(CanStartInitialize))]
        public async Task DieHighAlign()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            try
            {
                VisionMarkResult result = (SelectedDieType, SelectedDirectType, SelectedMarkType) switch
                {
                    // TOP + RIGHT
                    (DieType.TOP, DirectType.RIGHT, MarkType.FIDUCIAL) => TopRightFid = await SequenceService.TopDieVisionRightFid(_cts.Token),
                    (DieType.TOP, DirectType.RIGHT, MarkType.ALIGN_MARK) => TopRightAlign = await SequenceService.TopDieVisionRightAlign(_cts.Token),

                    // TOP + LEFT
                    (DieType.TOP, DirectType.LEFT, MarkType.FIDUCIAL) => TopLeftFid = await SequenceService.TopDieVisionLeftFid(_cts.Token),
                    (DieType.TOP, DirectType.LEFT, MarkType.ALIGN_MARK) => TopLeftAlign = await SequenceService.TopDieVisionLeftAlign(_cts.Token),

                    //// BOTTOM + RIGHT
                    //(DieType.BOTTOM, DirectType.RIGHT, MarkType.FIDUCIAL) => await SequenceService.Bottom(_cts.Token),
                    //(DieType.BOTTOM, DirectType.RIGHT, MarkType.ALIGN_MARK) => await SequenceService.BottomDieVisionRightAlign(_cts.Token),

                    //// BOTTOM + LEFT
                    //(DieType.BOTTOM, DirectType.LEFT, MarkType.FIDUCIAL) => await SequenceService.BottomDieVisionLeftFid(_cts.Token),
                    //(DieType.BOTTOM, DirectType.LEFT, MarkType.ALIGN_MARK) => await SequenceService.BottomDieVisionLeftAlign(_cts.Token),

                    _ => throw new InvalidOperationException($"지원하지 않는 조합: {SelectedDieType}, {SelectedDirectType}, {SelectedMarkType}")
                };

                _logger.Information($"DieHighAlign 완료: {SelectedDieType} / {SelectedDirectType} / {SelectedMarkType}");
            }
            catch (OperationCanceledException)
            {
                _logger.Information("DieHighAlign 작업이 취소되었습니다");
            }
            catch (Exception e)
            {
                _logger.Warning(e.Message);
            }
        }



        //[RelayCommand(CanExecute = nameof(CanStartInitialize))]
        //public async Task BottomVision()
        //{
        //    try
        //    {
        //        _cts?.Cancel();
        //        _cts = new CancellationTokenSource();

        //        await SequenceService.BottomVision(_cts.Token);
        //        _dialogService.ShowMessage("Bottom Vision완료", "Bottom Vision 완료");
        //    }
        //    catch (OperationCanceledException)
        //    {
        //    }
        //    catch (Exception ex)
        //    {
        //    }
        //    finally
        //    {
        //        InitializeCommand.NotifyCanExecuteChanged();
        //    }
        //}


        //[RelayCommand(CanExecute = nameof(CanStartInitialize))]
        //public async Task WaferLoad()
        //{
        //    try
        //    {
        //        IsInitializing = true;
        //        _cts?.Cancel();
        //        _cts = new CancellationTokenSource();

        //         await SequenceService.WTableLoading(_cts.Token);
        //    }
        //    catch (OperationCanceledException)
        //    {
        //    }
        //    catch (Exception ex)
        //    {
        //    }
        //    finally
        //    {
        //        IsInitializing = false;
        //        InitializeCommand.NotifyCanExecuteChanged();
        //    }
        //}

        //[RelayCommand(CanExecute = nameof(CanStartInitialize))]
        //public async Task WaferComplete()
        //{
        //    try
        //    {
        //        IsInitializing = true;
        //        _cts?.Cancel();
        //        _cts = new CancellationTokenSource();

        //        await SequenceService.WTableLoadComplete(_cts.Token);
        //    }
        //    catch (OperationCanceledException)
        //    {
        //    }
        //    catch (Exception ex)
        //    {
        //    }
        //    finally
        //    {
        //        IsInitializing = false;
        //        InitializeCommand.NotifyCanExecuteChanged();
        //    }
        //}




        //[RelayCommand(CanExecute = nameof(CanStartInitialize))]
        //public async Task WaferAlign()
        //{
        //    try
        //    {
        //        IsInitializing = true;
        //        _cts?.Cancel();
        //        _cts = new CancellationTokenSource();

        //        await SequenceService.WTableAlign(_cts.Token);
        //    }
        //    catch (OperationCanceledException)
        //    {
        //    }
        //    catch (Exception ex)
        //    {
        //    }
        //    finally
        //    {
        //        IsInitializing = false;
        //        InitializeCommand.NotifyCanExecuteChanged();
        //    }
        //}

        //[RelayCommand(CanExecute = nameof(CanStartInitialize))]
        //public async Task Bonding()
        //{
        //    try
        //    {
        //        IsInitializing = true;
        //        _cts?.Cancel();
        //        _cts = new CancellationTokenSource();

        //        await SequenceService.Bonding(_cts.Token);
        //        _dialogService.ShowMessage("Bonding 완료", "Bonding 완료");
        //    }
        //    catch (OperationCanceledException)
        //    {
        //    }
        //    catch (Exception ex)
        //    {
        //    }
        //    finally
        //    {
        //        IsInitializing = false;
        //        InitializeCommand.NotifyCanExecuteChanged();
        //    }
        //}


        //[RelayCommand]
        //private async Task Step1Start()
        //{
        //    _cts = new CancellationTokenSource();
        //    await SequenceService.StepMoveWaferCenter(_cts.Token);
        //    await SequenceService.StepWaferAlign(_cts.Token);
        //}
        //private List<DieData> GenerateWafer(int rows, int cols)
        //{
        //    var list = new List<DieData>();
        //    double centerX = cols / 2.0;
        //    double centerY = rows / 2.0;
        //    double radius = Math.Min(rows, cols) / 2.0;

        //    for (int r = 0; r < rows; r++)
        //    {
        //        for (int c = 0; c < cols; c++)
        //        {
        //            // 원형 웨이퍼 영역 안에 있는지 계산 (피타고라스 정리)
        //            double distance = Math.Sqrt(Math.Pow(c - centerX, 2) + Math.Pow(r - centerY, 2));

        //            if (distance < radius)
        //            {
        //                list.Add(new DieData
        //                {
        //                    Row = r,
        //                    Col = c,
        //                    DieBrush = Brushes.DimGray, // 기본 색상
        //                    Information = $"Die [{r}, {c}] - Ready"
        //                });
        //            }
        //        }
        //    }
        //    return list;
        //}

        // 특정 조건(예: 테스트 완료)에 따라 데이터를 한 번에 업데이트하는 예시


        //[RelayCommand]
        //public async Task DryRun()
        //{
        //    _cts?.Cancel();
        //    _cts = new CancellationTokenSource();
        //    var token = _cts.Token;

        //    try
        //    {
        //        while (!token.IsCancellationRequested)
        //        {
        //            for (int dieNumber = 1; dieNumber <= 9; dieNumber++)
        //            {
        //                token.ThrowIfCancellationRequested();

        //                // 1. DTable에서 Die 픽업
        //                await SequenceService.DTablePickup(dieNumber, null, token);
        //                //// 2. PTable로 이동 후 Align
        //                await SequenceService.BottomVision(token);
        //                // 3. Wafer에 본딩
        //                await SequenceService.Bonding(token);
        //            }
        //        }
        //    }
        //    catch (OperationCanceledException)
        //    {
        //        // 정지 버튼으로 인한 취소 처리
        //    }
        //    catch (Exception ex)
        //    {
        //        // 예외 처리
        //    }
        //}

        [RelayCommand(CanExecute = nameof(CanStartInitialize))]
        public async Task WPinDown()
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            await SequenceService.WTablePinControll(eUpDown.Down, _cts.Token);

        }
        [RelayCommand(CanExecute = nameof(CanStartInitialize))]
        public async Task WPinUp()
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            await SequenceService.WTablePinControll(eUpDown.Up, _cts.Token);

        }

        [RelayCommand(CanExecute = nameof(CanStartInitialize))]
        public async Task AllHome()
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            try
            {
                await SequenceService.Init_ServoAllOn(_cts.Token);

                //await SequenceService.All_Home(_cts.Token);
            }
            catch (Exception e)
            {

            }

        }
        [RelayCommand(CanExecute = nameof(CanStartInitialize))]
        public async Task RequesRightVisionMark()
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            try
            {
                // 397.403, 19.841
                var rightFid = await eqpCommunicationService.RequestVisionMarkPosition(MarkType.FIDUCIAL, CameraType.PC_HIGH, "RIGHT");
                
            }
            catch (Exception e)
            {

            }

        }
        [RelayCommand(CanExecute = nameof(CanStartInitialize))]
        public async Task RequesLeftVisionMark()
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            try
            {

                var rightFid = await eqpCommunicationService.RequestVisionMarkPosition(MarkType.FIDUCIAL, CameraType.PC_HIGH, "LEFT");

            }
            catch (Exception e)
            {

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

        [RelayCommand]
        public async Task RequestMarks
            ()
        {
            await calibrationViewModel.RequestAlignMark(DieType.TOP, DirectType.LEFT);
        }
        private bool CanStartInitialize() => !IsInitializing;
    }
}
