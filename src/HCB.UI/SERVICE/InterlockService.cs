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

        private IAxis _HX;
        private IAxis _HZ;
        private IAxis _HT;
        private IAxis _hz;
        private IAxis _PY;
        private IAxis _WY;
        private IAxis _WT;
        private IAxis _DY;        

        public InterlockService(ILogger logger, ISequenceHelper sequenceHelper, DeviceManager deviceManager)
        {
            _logger = logger.ForContext<InterlockService>();
            _deviceManager = deviceManager;
            _sequenceHelper = sequenceHelper;

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
                    await _sequenceHelper?.StopAllAsync(stoppingToken);
                    _sequenceHelper?.SetTowerLamp(green: false, red: true, yellow: false, buzzer: true);

                    _logger.Warning(new SysLog("OperationService", EQStatus.Availability.ToString(), EQStatus.Run.ToString(), EQStatus.Alarm.ToString(), EQStatus.Operation.ToString(), "Heavy Alarm Detected - Stopping Operation").ToString());
                }

                _timer.Change(0, 10); // 10ms 주기로 타이머 시작    s
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "InterlockService: PowerPMAC 연결 실패");
            }
        }

        public async Task InterlockMotion()
        {
            //if (_HZ.CurrentPosition > )
            //{
                
            //}
        }
    }
}
