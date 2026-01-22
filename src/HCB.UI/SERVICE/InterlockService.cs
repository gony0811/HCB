using HCB.Data.Entity.Type;
using HCB.IoC;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace HCB.UI
{
    [Service(Lifetime.Singleton)]
    internal class InterlockService : BackgroundService
    {
        private ILogger _logger;
        private DeviceManager _deviceManager;
        private readonly ISequenceHelper _sequenceHelper;
        private readonly Timer _timer;
        private readonly SemaphoreSlim _pollingLock = new SemaphoreSlim(1, 1);
        private readonly AlarmService _alarmService;

        private PmacIoDevice _powerPmacDevice;

        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private IAxis _HX;
        private IAxis _HZ;
        private IAxis _HT;
        private IAxis _hz;
        private IAxis _PY;
        private IAxis _WY;
        private IAxis _WT;
        private IAxis _DY;        

        public InterlockService(ILogger logger, ISequenceHelper sequenceHelper, DeviceManager deviceManager, AlarmService alarmService)
        {
            _logger = logger.ForContext<InterlockService>();
            _deviceManager = deviceManager;
            _sequenceHelper = sequenceHelper;
            _alarmService = alarmService;

            this.Initialize();

            // 디바이스 데이터 폴링 타이머 설정 (100ms 주기)
            // 중복 실행 방지 및 종료 시 대기를 위해 SemaphoreSlim 사용
            _timer = new Timer(async _ =>
            {
                // 락 획득 시도 (이미 실행 중이면 스킵)
                if (await _pollingLock.WaitAsync(0))
                {
                    try
                    {
                        if (EQStatus.Availability == Availability.Down) return;

                        await MonitoringSafety(_cancellationTokenSource.Token);

                        await InterlockMotion(_cancellationTokenSource.Token);

                    }
                    catch (OperationCanceledException)
                    {
                        // 작업이 취소된 경우 무시
                        _cancellationTokenSource = new CancellationTokenSource();
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "InterlockService Monitoring Error");
                    }
                    finally
                    {
                        _pollingLock.Release();
                    }
                }
            }, null, Timeout.Infinite, Timeout.Infinite);
        }

        private void Initialize()
        {
            _HX = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName).FindMotionByName(MotionExtensions.H_X);
            _HZ = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName).FindMotionByName(MotionExtensions.H_Z);
            _HT = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName).FindMotionByName(MotionExtensions.H_T);
            _hz = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName).FindMotionByName(MotionExtensions.h_z);
            _PY = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName).FindMotionByName(MotionExtensions.P_Y);
            _WY = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName).FindMotionByName(MotionExtensions.W_Y);
            _WT = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName).FindMotionByName(MotionExtensions.W_T);
            _DY = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName).FindMotionByName(MotionExtensions.D_Y);

            _powerPmacDevice = _deviceManager.GetDevice<PmacIoDevice>(MotionExtensions.PMacIoDeviceName);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.Information(new SysLog("InterlockService", EQStatus.Availability.ToString(), EQStatus.Run.ToString(), EQStatus.Alarm.ToString(), EQStatus.Operation.ToString(), "").ToString());


                // 1. 알람 발생시 운전 정지 및 장비 다운 처리
                if (EQStatus.Alarm == AlarmState.HEAVY)
                {
                    EQStatus.Run = RunStop.Stop;
                    EQStatus.Operation = OperationMode.Manual;
                    EQStatus.Availability = Availability.Down;

                    /**** 모든 모션 축 정지 ****/
                    await _sequenceHelper.StopAllAsync(stoppingToken);
                    _sequenceHelper.SetTowerLamp(green: false, red: true, yellow: false, buzzer: true);

                    _logger.Warning(new SysLog("OperationService", EQStatus.Availability.ToString(), EQStatus.Run.ToString(), EQStatus.Alarm.ToString(), EQStatus.Operation.ToString(), "Heavy Alarm Detected - Stopping Operation").ToString());
                }

                _timer.Change(0, 10); // 10ms 주기로 타이머 시작    s
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "InterlockService: PowerPMAC 연결 실패");
            }
        }


        
        public async Task MonitoringSafety(CancellationToken token)
        {
            if (_powerPmacDevice.GetDigital(IoExtensions.DI_EMO_1_SWITCH) == true)
            {
                // EMS 1 스위치가 눌렸을 때 처리
                await _alarmService.SetAlarm("E001");
                await _sequenceHelper.StopAllAsync(token);
            }

            if (_powerPmacDevice.GetDigital(IoExtensions.DI_EMO_2_SWITCH) == true)
            {
                // EMS 2 스위치가 눌렸을 때 처리
                await _alarmService.SetAlarm("E002");
                await _sequenceHelper.StopAllAsync(token);
            }


            if (_powerPmacDevice.GetDigital(IoExtensions.DI_LIGHT_CURTAIN) == true)
            {
                // 라이트 커튼이 차단되었을 때 처리
                await _alarmService.SetAlarm("E003");
                await _sequenceHelper.StopAllAsync(token);
            }

            if (_powerPmacDevice.GetDigital(IoExtensions.DI_FRONT_LEFT_DOOR) == true)
            {                 
                // 전면 왼쪽 도어가 열렸을 때 처리
                await _alarmService.SetAlarm("E004");
                await _sequenceHelper.StopAllAsync(token);
            }

            if (_powerPmacDevice.GetDigital(IoExtensions.DI_FRONT_RIGHT_DOOR) == true)
            {
                // 전면 오른쪽 도어가 열렸을 때 처리
                await _alarmService.SetAlarm("E005");
                await _sequenceHelper.StopAllAsync(token);
            }

            if (_powerPmacDevice.GetDigital(IoExtensions.DI_SIDE_LEFT_DOOR) == true)
            {
                // 측면 왼쪽 도어가 열렸을 때 처리
                await _alarmService.SetAlarm("E006");
                await _sequenceHelper.StopAllAsync(token);
            }

            if (_powerPmacDevice.GetDigital(IoExtensions.DI_SIDE_RIGHT_DOOR) == true)
            {
                // 측면 오른쪽 도어가 열렸을 때 처리
                await _alarmService.SetAlarm("E0007");
                await _sequenceHelper.StopAllAsync(token);
            }

            if (_powerPmacDevice.GetDigital(IoExtensions.DI_FAN_1_ALARM) == true)
            {
                // 팬 1 알람 발생 시 처리
                await _alarmService.SetAlarm("E0008");
            }

            if (_powerPmacDevice.GetDigital(IoExtensions.DI_FAN_2_ALARM) == true)
            {
                // 팬 2 알람 발생 시 처리
                await _alarmService.SetAlarm("E0009");
            }
            if (_powerPmacDevice.GetDigital(IoExtensions.DI_FAN_3_ALARM) == true)
            {
                // 팬 3 알람 발생 시 처리
                await _alarmService.SetAlarm("E0010");
            }

            if (_powerPmacDevice.GetDigital(IoExtensions.DI_FAN_4_ALARM) == true)
            {
                // 팬 4 알람 발생 시 처리
                await _alarmService.SetAlarm("E0011");
            }

            if (_powerPmacDevice.GetDigital(IoExtensions.DI_EPU_ALARM) == true)
            {
                // CP03 알람 발생 시 처리
                await _alarmService.SetAlarm("E0012");
            }

            if (_powerPmacDevice.GetDigital(IoExtensions.DI_CP04_TRIP) == false)
            {
                // CP04 알람 발생 시 처리
                await _alarmService.SetAlarm("E0013");
            }
            
            if (_powerPmacDevice.GetDigital(IoExtensions.DI_CP05_TRIP) == false)
            {
                // CP05 알람 발생 시 처리
                await _alarmService.SetAlarm("E0014");
            }


            if (_powerPmacDevice.GetDigital(IoExtensions.DI_CP06_TRIP) == false)
            {
                // CP06 알람 발생 시 처리
                await _alarmService.SetAlarm("E0015");
            }

            if (_powerPmacDevice.GetDigital(IoExtensions.DI_CP07_TRIP) == false)
            {
                // CP07 알람 발생 시 처리
                await _alarmService.SetAlarm("E0016");
            }

            if (_powerPmacDevice.GetDigital(IoExtensions.DI_CP08_TRIP) == false)
            {
                // CP08 알람 발생 시 처리
                await _alarmService.SetAlarm("E0017");
            }
            
            if (_powerPmacDevice.GetDigital(IoExtensions.DI_CP10_TRIP) == false)
            {
                // CP10 알람 발생 시 처리
                await _alarmService.SetAlarm("E0018");
            }
            if (_powerPmacDevice.GetDigital(IoExtensions.DI_CP11_TRIP) == false)
            {
                // CP11 알람 발생 시 처리
                await _alarmService.SetAlarm("E0019");
            }
            
            if (_powerPmacDevice.GetDigital(IoExtensions.DI_CP12_TRIP) == false)
            {
                // CP12 알람 발생 시 처리
                await _alarmService.SetAlarm("E0020");
            }
            if (_powerPmacDevice.GetDigital(IoExtensions.DI_CP22_TRIP) == false)
            {
                // CP22 알람 발생 시 처리
                await _alarmService.SetAlarm("E0021");
            }
            
            if (_powerPmacDevice.GetDigital(IoExtensions.DI_CP23_TRIP) == false)
            {
                // CP23 알람 발생 시 처리
                await _alarmService.SetAlarm("E0022");
            }

            if (_powerPmacDevice.GetDigital(IoExtensions.DI_CP24_TRIP) == false)
            {
                // CP24 알람 발생 시 처리
                await _alarmService.SetAlarm("E0023");
            }

            if (_powerPmacDevice.GetDigital(IoExtensions.DI_DRIVER_BUS_DC) == false)
            {
                // 드라이버 DC BUS 알람 발생 시 처리
                await _alarmService.SetAlarm("E0024");
            }

            if (_powerPmacDevice.GetDigital(IoExtensions.DI_MAIN_CDA_PRESSURE_SWITCH_ALARM) == true)
            {
                // 메인 CDA 압력 스위치 알람 발생 시 처리
                await _alarmService.SetAlarm("E0025");
            }
            
            if (_powerPmacDevice.GetDigital(IoExtensions.DI_MAIN_VAC_PRESSURE_SWITCH_1_ALARM) == true)
            {
                // 메인 VAC 압력 스위치 1 알람 발생 시 처리
                await _alarmService.SetAlarm("E0026");
            }

            if (_powerPmacDevice.GetDigital(IoExtensions.DI_MAIN_VAC_PRESSURE_SWITCH_2_ALARM) == true)
            {
                // 메인 VAC 압력 스위치 2 알람 발생 시 처리
                await _alarmService.SetAlarm("E0027");
            }

            if (_powerPmacDevice.GetDigital(IoExtensions.DI_MAIN_N2_PRESSURE_SWITCH_ALARM) == true)
            {
                // 메인 N2 압력 스위치 알람 발생 시 처리
                await _alarmService.SetAlarm("E0028");
            }

        }

        public async Task InterlockMotion(CancellationToken token)
        {
            var safeHZPosition = 90.0;

            if (_HZ.CurrentPosition > safeHZPosition) // HZ 축이 안전 위치 이상으로 내려와 있을때, HX 축 이동 금지
            {
                if (_HX.IsBusy || _DY.IsBusy || _WY.IsBusy || _PY.IsBusy)
                {
                    await _sequenceHelper.StopAllAsync(token);
                    await _alarmService.SetAlarm("E0029");
                    _logger.Warning(new SysLog("InterlockService", EQStatus.Availability.ToString(), EQStatus.Run.ToString(), EQStatus.Alarm.ToString(), EQStatus.Operation.ToString(), "Interlock: HZ axis is not in safe position. Stopping HX axis movement.").ToString());
                }
            }

        }
    }
}
