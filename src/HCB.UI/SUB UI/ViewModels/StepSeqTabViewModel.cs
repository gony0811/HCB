using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.IoC;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Telerik.Windows.Documents.Spreadsheet.Expressions.Functions;
using static HCB.UI.SERVICE.CalibrationService;

namespace HCB.UI
{
    [ViewModel(Lifetime.Singleton)]
    public partial class StepSeqTabViewModel : ObservableObject
    {
        private CancellationTokenSource _cts;
        private readonly ILogger _logger;
        private readonly SequenceHelper _sequenceHelper;
        private readonly SequenceService _sequenceService;
        private readonly DeviceManager _deviceManager;
        private readonly RecipeService _recipeService;
        private readonly ECParamService _ecParamService;
        private IOManager ioManager;

        public RecipeService RecipeService => _recipeService;

        [ObservableProperty] private int topDie = 0;
        [ObservableProperty] private int bottomDie = 0;

        [ObservableProperty] private VisionMarkPositionResponse visionBtmLowAlign;
        [ObservableProperty] private VisionMarkPositionResponse visionTopLowAlign;

        [ObservableProperty] private VisionMarkResult topRightAlign;
        [ObservableProperty] private VisionMarkResult topRightFid;
        [ObservableProperty] private VisionMarkResult topLeftAlign;
        [ObservableProperty] private VisionMarkResult topLeftFid;

        [ObservableProperty] private VisionMarkResult btmRightAlign;
        [ObservableProperty] private VisionMarkResult btmRightFid;
        [ObservableProperty] private VisionMarkResult btmLeftAlign;
        [ObservableProperty] private VisionMarkResult btmLeftFid;


        [ObservableProperty] private double topOffsetX;
        [ObservableProperty] private double topOffsetY;
        [ObservableProperty] private double topOffsetT;

        [ObservableProperty] private double btmOffsetX;
        [ObservableProperty] private double btmOffsetY;
        [ObservableProperty] private double btmOffsetT;

        [ObservableProperty] private RecipeDto selectedRecipe;

        [ObservableProperty]
        private ObservableCollection<BondingDataPoint> bondingHistory = new ObservableCollection<BondingDataPoint>();

        // ── Step Lamp States ─────────────────────────────────
        [ObservableProperty] private StepState initState          = StepState.Idle;
        [ObservableProperty] private StepState dieLoadState       = StepState.Idle;
        [ObservableProperty] private StepState waferLoadState     = StepState.Idle;
        [ObservableProperty] private StepState recipeSelectState  = StepState.Idle;

        [ObservableProperty] private StepState btmLowAlignState  = StepState.Idle;
        [ObservableProperty] private StepState btmPickupState    = StepState.Idle;
        [ObservableProperty] private StepState btmHighAlignState = StepState.Idle;
        [ObservableProperty] private StepState btmPlaceState     = StepState.Idle;

        [ObservableProperty] private StepState topLowAlignState  = StepState.Idle;
        [ObservableProperty] private StepState topPickupState    = StepState.Idle;
        [ObservableProperty] private StepState topHighAlignState = StepState.Idle;
        [ObservableProperty] private StepState topPlaceState     = StepState.Idle;
        [ObservableProperty] private StepState topBondingState   = StepState.Idle;

        [ObservableProperty] private bool isInitInfoOpen;

        public SequenceServiceVM SequenceServiceVM { get; }

        [ObservableProperty]
        private ObservableCollection<SensorIoItemViewModel> dTableList = new ObservableCollection<SensorIoItemViewModel>();

        private readonly List<string> dTableNameList = new List<string>
        {
            "DIE 1", "DIE 2", "DIE 3", "DIE 4", "DIE 5", "DIE 6", "DIE 7", "DIE 8", "DIE 9",
        };

        private readonly List<string> dIoNameList = new List<string>
        {
            IoExtensions.DO_DTABLE_VAC_1_ON, IoExtensions.DO_DTABLE_VAC_2_ON, IoExtensions.DO_DTABLE_VAC_3_ON,
            IoExtensions.DO_DTABLE_VAC_4_ON, IoExtensions.DO_DTABLE_VAC_5_ON, IoExtensions.DO_DTABLE_VAC_6_ON,
            IoExtensions.DO_DTABLE_VAC_7_ON, IoExtensions.DO_DTABLE_VAC_8_ON, IoExtensions.DO_DTABLE_VAC_9_ON,
        };

        public StepSeqTabViewModel(
            SequenceServiceVM sequenceServiceVM,
            SequenceService sequenceService,
            SequenceHelper sequenceHelper,
            DeviceManager deviceManager,
            IOManager ioManager,
            RecipeService recipeService,
            ILogger logger)
        {
            _logger = logger.ForContext<StepSeqTabViewModel>();
            this.SequenceServiceVM = sequenceServiceVM;
            this._sequenceService = sequenceService;
            this._sequenceHelper = sequenceHelper;
            this._deviceManager = deviceManager;
            this._recipeService = recipeService;
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

        // ── STOP ─────────────────────────────────────────────
        [RelayCommand]
        public async Task Stop()
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
                await _sequenceService.StopAsync(_cts.Token);
                InitState = StepState.Idle;
                BtmLowAlignState = BtmPickupState = BtmPlaceState = StepState.Idle;
                TopLowAlignState = TopPickupState = TopHighAlignState = TopPlaceState = StepState.Idle;
            }
        }

        // ── INIT ─────────────────────────────────────────────
        [RelayCommand]
        public async Task Init()
        {
            _cts?.Cancel(); _cts?.Dispose(); _cts = new CancellationTokenSource();
            try
            {
                SequenceServiceVM.ResetInitProgress();
                InitState = StepState.InProgress;
                await _sequenceService.MachineInitAsync(_cts.Token);
                InitState = StepState.Completed;
            }
            catch (OperationCanceledException) { InitState = StepState.Idle; }
            catch (Exception e) { InitState = StepState.Failed; _logger.Warning(e.Message); }
        }

        // Die Load
        [RelayCommand]
        public async Task DieLoad()
        {
            _cts?.Cancel(); _cts?.Dispose(); _cts = new CancellationTokenSource();
            try
            {
                await _sequenceService.DTableLoading(_cts.Token);

                bool confirmed = false;
                List<int> topList = new List<int>();
                List<int> botList = new List<int>();

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var dialog = new VacuumSelector
                    {
                        WindowStartupLocation = WindowStartupLocation.CenterScreen
                    };
                    dialog.ShowDialog();
                    confirmed = dialog.DialogResult == true;
                    if (confirmed)
                    {
                        topList = dialog.TopDieVacuums;
                        botList = dialog.BotDieVacuums;
                    }
                });

                if (!confirmed) return;

                if (topList.Count > 0) TopDie = topList[0];
                if (botList.Count > 0) BottomDie = botList[0];

                _logger.Information(
                    "Die Load 선택 완료 — TOP: [{Top}]  BOT: [{Bot}]",
                    string.Join(", ", topList),
                    string.Join(", ", botList));

                List<int> vacs = new List<int> { TopDie, BottomDie };
                await _sequenceService.DTableLoadComplete(vacs, _cts.Token);
            }
            catch (Exception e)
            {
                _logger.Error(e.Message);
            }
        }

        [RelayCommand]
        public void InitInfo() => IsInitInfoOpen = true;

        [RelayCommand]
        public void OpenTopHighAlignInfo()
        {
            var rightFid   = TopRightFid;
            var rightAlign = TopRightAlign;
            var leftFid    = TopLeftFid;
            var leftAlign  = TopLeftAlign;

            _ = RunDialogOnNewThread(() =>
            {
                var window = new TopHighAlignInfoWindow(rightFid, rightAlign, leftFid, leftAlign)
                {
                    Header = "고배율 보정 정보 (Top Die)",
                    WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen
                };
                window.ShowDialog();
            });
        }

        [RelayCommand]
        public void BtmHighAlignInfo()
        {
            var rightFid   = BtmRightFid;
            var rightAlign = BtmRightAlign;
            var leftFid    = BtmLeftFid;
            var leftAlign  = BtmLeftAlign;

            _ = RunDialogOnNewThread(() =>
            {
                var window = new TopHighAlignInfoWindow(rightFid, rightAlign, leftFid, leftAlign, useWaferY: true)
                {
                    Header = "고배율 보정 정보 (Bottom Die)",
                    WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen
                };
                window.ShowDialog();
            });
        }

        [RelayCommand]
        public async Task BtmHighAlign()
        {
            _cts?.Cancel(); _cts?.Dispose(); _cts = new CancellationTokenSource();
            try
            {
                BtmHighAlignState = StepState.InProgress;

                // HC1_T (Left 카메라), HC2_T (Right 카메라) 각도 로드
                var hc1Param = _ecParamService.FindByName(MotionExtensions.HC1_T);
                var hc2Param = _ecParamService.FindByName(MotionExtensions.HC2_T);
                if (hc1Param.Id == 0) throw new Exception("HC1_T Not Found");
                if (hc2Param.Id == 0) throw new Exception("HC2_T Not Found");
                double hc1Rad = double.Parse(hc1Param.Value);
                double hc2Rad = double.Parse(hc2Param.Value);

                // 피에조 오프셋
                double lPiezoOffsetX = -6.1245;
                double lPiezoOffsetY =  3.479;
                double rPiezoOffsetX =  6.1245;
                double rPiezoOffsetY = -3.479;

                await _sequenceService.TopDiePlace(_cts.Token);

                // ── Right 마크 (HC2 카메라) ───────────────────────────
                BtmRightFid = await _sequenceService.BtmDieVisionRightFid(_cts.Token);
                BtmRightFid.StageX -= rPiezoOffsetX;
                BtmRightFid.StageY -= rPiezoOffsetY;

                BtmRightAlign = await _sequenceService.BtmDieVisionRightAlign(_cts.Token);
                BtmRightAlign.StageX -= rPiezoOffsetX;
                BtmRightAlign.StageY -= rPiezoOffsetY;

                // ── Left 마크 (HC1 카메라) ────────────────────────────
                BtmLeftFid = await _sequenceService.BtmDieVisionLeftFid(_cts.Token);
                BtmLeftFid.StageX -= lPiezoOffsetX;
                BtmLeftFid.StageY -= lPiezoOffsetY;

                BtmLeftAlign = await _sequenceService.BtmDieVisionLeftAlign(_cts.Token);
                BtmLeftAlign.StageX -= lPiezoOffsetX;
                BtmLeftAlign.StageY -= lPiezoOffsetY;

                // ── 카메라 각도 보정 (HC1 → Left, HC2 → Right) ────────
                var corrRF = CalibrationMath.ApplyRotation(Point2D.of(BtmRightFid.DxCamToMark,   BtmRightFid.DyCamToMark),   hc2Rad);
                var corrRA = CalibrationMath.ApplyRotation(Point2D.of(BtmRightAlign.DxCamToMark, BtmRightAlign.DyCamToMark), hc2Rad);
                var corrLF = CalibrationMath.ApplyRotation(Point2D.of(BtmLeftFid.DxCamToMark,    BtmLeftFid.DyCamToMark),    hc1Rad);
                var corrLA = CalibrationMath.ApplyRotation(Point2D.of(BtmLeftAlign.DxCamToMark,  BtmLeftAlign.DyCamToMark),  hc1Rad);

                BtmRightFid.DxCamToMark   = corrRF.X;  BtmRightFid.DyCamToMark   = corrRF.Y;
                BtmRightAlign.DxCamToMark = corrRA.X;  BtmRightAlign.DyCamToMark = corrRA.Y;
                BtmLeftFid.DxCamToMark    = corrLF.X;  BtmLeftFid.DyCamToMark    = corrLF.Y;
                BtmLeftAlign.DxCamToMark  = corrLA.X;  BtmLeftAlign.DyCamToMark  = corrLA.Y;

                // ── X/Y 보정량: Fid 중심 - Align 중심 ────────────────
                double fidCenterX   = (BtmRightFid.CenterX   + BtmLeftFid.CenterX)   / 2.0;
                double fidCenterY   = (BtmRightFid.CenterY   + BtmLeftFid.CenterY)   / 2.0;
                double alignCenterX = (BtmRightAlign.CenterX + BtmLeftAlign.CenterX) / 2.0;
                double alignCenterY = (BtmRightAlign.CenterY + BtmLeftAlign.CenterY) / 2.0;

                BtmOffsetX = fidCenterX - alignCenterX;
                BtmOffsetY = fidCenterY - alignCenterY;

                // ── T 보정량: Fid 기준선 각도 - Align 기준선 각도 ────
                double fidTheta   = CalcTheta(BtmLeftFid,   BtmRightFid);
                double alignTheta = CalcTheta(BtmLeftAlign, BtmRightAlign);
                BtmOffsetT = fidTheta - alignTheta;

                BtmHighAlignState = StepState.Completed;
            }
            catch (OperationCanceledException) { BtmHighAlignState = StepState.Idle; }
            catch (Exception e) { BtmHighAlignState = StepState.Failed; _logger.Warning(e.Message); }
        }

        [RelayCommand]
        public void CloseInitInfo() => IsInitInfoOpen = false;

        // ── Bonding Info (BONDING 스텝 INFO 버튼) ────────────────────
        [RelayCommand]
        public void TopHighAlignInfo()
        {
            var recipeService = _recipeService;
            var history       = BondingHistory.ToList();

            _ = RunDialogOnNewThread(() =>
            {
                var window = new BondingInfoWindow(recipeService, history)
                {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };
                window.ShowDialog();
            });
        }

        // BOTTOM ALIGN 
        [RelayCommand]
        public async Task BtmLowAlign()
        {
            _cts?.Cancel(); _cts?.Dispose(); _cts = new CancellationTokenSource();
            try
            {
                if (BottomDie == 0) { _logger.Information("Bottom Die를 Load해주세요"); return; }
                BtmLowAlignState = StepState.InProgress;
                VisionBtmLowAlign = await _sequenceService.DTableCarrierAlign(BottomDie, MarkType.DIE_CENTER_BOTTOM, _cts.Token);
                BtmLowAlignState = StepState.Completed;
            }
            catch (OperationCanceledException) { BtmLowAlignState = StepState.Idle; }
            catch (Exception e)
            {
                BtmLowAlignState =  StepState.Failed;
                _logger.Warning(e.Message); }
        }

        [RelayCommand]
        public async Task BtmPickup()
        {
            _cts?.Cancel(); _cts?.Dispose(); _cts = new CancellationTokenSource();
            try
            {
                if (BottomDie == 0) { _logger.Information("Bottom Die를 Load해주세요"); return; }
                if (VisionBtmLowAlign == null) { _logger.Information("Bottom Die Align 해주세요"); return; }
                BtmPickupState = StepState.InProgress;
                await _sequenceService.DTableBTMPickup(BottomDie, VisionBtmLowAlign, _cts.Token);
                DTableList[BottomDie - 1].Off();
                BtmPickupState = StepState.Completed;
                
            }
            catch (OperationCanceledException) { BtmPickupState = StepState.Idle; }
            catch (Exception e) { BtmPickupState = StepState.Failed; _logger.Warning(e.Message); }
        }

        [RelayCommand]
        public async Task BtmPlace()
        {
            _cts?.Cancel(); _cts?.Dispose(); _cts = new CancellationTokenSource();
            try
            {
                BtmPlaceState = StepState.InProgress;
                await _sequenceService.BtmDieDrop(1, _cts.Token);
                BtmPlaceState = StepState.Completed;
                
            }
            catch (OperationCanceledException) { BtmPlaceState = StepState.Idle; }
            catch (Exception e) { BtmPlaceState = StepState.Failed; _logger.Warning(e.Message); }
        }

        // ── TOP ALIGN ────────────────────────────────────────
        [RelayCommand]
        public async Task TopLowAlign()
        {
            _cts?.Cancel(); _cts?.Dispose(); _cts = new CancellationTokenSource();
            try
            {
                if (TopDie == 0) { _logger.Information("Top Die를 Load해주세요"); return; }
                TopLowAlignState = StepState.InProgress;
                VisionTopLowAlign = await _sequenceService.DTableCarrierAlign(TopDie, MarkType.DIE_CENTER_TOP, _cts.Token);
                TopLowAlignState = StepState.Completed;
            }
            catch (OperationCanceledException) { TopLowAlignState = StepState.Idle; }
            catch (Exception e) { TopLowAlignState = StepState.Failed; _logger.Warning(e.Message); }
        }

        [RelayCommand]
        public async Task TopPickup()
        {
            _cts?.Cancel(); _cts?.Dispose(); _cts = new CancellationTokenSource();
            try
            {
                if (TopDie == 0) { _logger.Information("Top Die를 Load해주세요"); return; }
                if (VisionTopLowAlign == null) { _logger.Information("Top Die Align 해주세요"); return; }
                TopPickupState = StepState.InProgress;
                await _sequenceService.DTableTOPPickup(TopDie, VisionTopLowAlign, _cts.Token);
                DTableList[TopDie - 1].Off();
                TopPickupState = StepState.Completed;
            }
            catch (OperationCanceledException) { TopPickupState = StepState.Idle; }
            catch (Exception e) { TopPickupState = StepState.Failed; _logger.Warning(e.Message); }
        }

        [RelayCommand]
        public async Task TopHighAlign()
        {
            _cts?.Cancel(); _cts?.Dispose(); _cts = new CancellationTokenSource();
            try
            {
                TopHighAlignState = StepState.InProgress;
                TopRightFid = await _sequenceService.TopDieVisionRightFid(_cts.Token);
                TopRightAlign = await _sequenceService.TopDieVisionRightAlign(_cts.Token);
                TopLeftFid = await _sequenceService.TopDieVisionLeftFid(_cts.Token);
                TopLeftAlign = await _sequenceService.TopDieVisionLeftAlign(_cts.Token);

                // PC_T (degrees → radians)
                var pcT = _ecParamService.FindByName(MotionExtensions.PC_T);
                if (pcT.Id == 0) throw new Exception("PC_T Not Found");
                double pcTRad = double.Parse(pcT.Value);

                // 카메라 오프셋에 PC_T 회전 행렬 적용 → Stage 좌표계로 변환
                var corrRF = CalibrationMath.ApplyRotation(Point2D.of(TopRightFid.DxCamToMark,   TopRightFid.DyCamToMark),   pcTRad);
                var corrRA = CalibrationMath.ApplyRotation(Point2D.of(TopRightAlign.DxCamToMark, TopRightAlign.DyCamToMark), pcTRad);
                var corrLF = CalibrationMath.ApplyRotation(Point2D.of(TopLeftFid.DxCamToMark,    TopLeftFid.DyCamToMark),    pcTRad);
                var corrLA = CalibrationMath.ApplyRotation(Point2D.of(TopLeftAlign.DxCamToMark,  TopLeftAlign.DyCamToMark),  pcTRad);

                // DxCamToMark / DyCamToMark 갱신 → CenterX/CenterY 자동 반영
                TopRightFid.DxCamToMark   = corrRF.X;  TopRightFid.DyCamToMark   = corrRF.Y;
                TopRightAlign.DxCamToMark = corrRA.X;  TopRightAlign.DyCamToMark = corrRA.Y;
                TopLeftFid.DxCamToMark    = corrLF.X;  TopLeftFid.DyCamToMark    = corrLF.Y;
                TopLeftAlign.DxCamToMark  = corrLA.X;  TopLeftAlign.DyCamToMark  = corrLA.Y;

                // X/Y 보정량 : Fid 중심 - Align 중심
                double fidCenterX   = (TopRightFid.CenterX   + TopLeftFid.CenterX)   / 2.0;
                double fidCenterY   = (TopRightFid.CenterY   + TopLeftFid.CenterY)   / 2.0;
                double alignCenterX = (TopRightAlign.CenterX + TopLeftAlign.CenterX) / 2.0;
                double alignCenterY = (TopRightAlign.CenterY + TopLeftAlign.CenterY) / 2.0;

                TopOffsetX = fidCenterX  - alignCenterX;
                TopOffsetY = fidCenterY  - alignCenterY;

                // T 보정량 : Fid 기준선 각도 - Align 기준선 각도
                double fidTheta   = CalcTheta(TopLeftFid,   TopRightFid);
                double alignTheta = CalcTheta(TopLeftAlign, TopRightAlign);
                TopOffsetT = fidTheta - alignTheta;

                TopHighAlignState = StepState.Completed;
            }
            catch (OperationCanceledException) { TopHighAlignState = StepState.Idle; }
            catch (Exception e) { TopHighAlignState = StepState.Failed; _logger.Warning(e.Message); }
        }

        


        // ── 모달창 별도 STA 스레드 실행 헬퍼 ────────────────────────
        // ShowDialog()를 새 STA 스레드에서 실행하여 메인 UI가 블로킹되지 않도록 한다.
        private static Task RunDialogOnNewThread(Action dialogAction)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() =>
            {
                try
                {
                    dialogAction();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
                finally
                {
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            return tcs.Task;
        }

        public async Task BtmInit()
        {
            _cts?.Cancel(); _cts?.Dispose(); _cts = new CancellationTokenSource();
            await _sequenceHelper.HeadPickerVacuum(eOnOff.Off, _cts.Token);
            BottomDie = 0;
            BtmLowAlignState = StepState.Idle;
            BtmPickupState = StepState.Idle;
            BtmPlaceState = StepState.Idle;
            VisionBtmLowAlign = null;
        }

        public async Task TopInit()
        {
            
            _cts?.Cancel(); _cts?.Dispose(); _cts = new CancellationTokenSource();
            try
            {
                await _sequenceHelper.HeadPickerVacuum(eOnOff.Off, _cts.Token);
                TopDie = 0;
                TopLowAlignState = StepState.Idle;
                TopPickupState = StepState.Idle;
                TopHighAlignState = StepState.Idle;
                TopPlaceState = StepState.Idle;
                VisionTopLowAlign = null;
            }
            catch(Exception e)
            {
                throw;
            }
            

            
        }
    }
}
