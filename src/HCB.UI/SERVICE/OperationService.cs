using HCB.Data.Entity.Type;
using HCB.IoC;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HCB.UI
{
    [Service(Lifetime.Singleton)]
    public class OperationService : BackgroundService, IOperationService
    {
        public EQStatus Status { get; private set; } = new EQStatus();

        public event Action<EQStatus> EQStatusChanged = delegate { };

        public void SetAlarm(AlarmState state)
        {
            Status.Alarm = state;
            EQStatusChanged(Status);
        }

        public void SetAvailability(Availability availability)
        {
            Status.Availability = availability;
            EQStatusChanged(Status);
        }

        public void SetOperationMode(OperationMode operation)
        {
            Status.Operation = operation;
            EQStatusChanged(Status);
        }

        public void SetRun(RunStop run)
        {
            Status.Run = run;
            EQStatusChanged(Status);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.CompletedTask;
        }
    }
}
