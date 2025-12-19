using HCB.Data.Entity.Type;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telerik.Windows.Documents.Spreadsheet.Expressions.Functions;

namespace HCB.UI
{

    public class StatusChangedEventArgs : EventArgs
    {
        public OperationMode Mode { get; private set; }
        public Availability Availability { get; private set; }
        public RunStop RunStop { get; private set; }
        public AlarmLevel AlarmLevel { get; private set; }

        public StatusChangedEventArgs(OperationMode mode, Availability availability, RunStop runStop, AlarmLevel alarmLevel)
        {
            Mode = mode;
            Availability = availability;
            RunStop = runStop;
            AlarmLevel = alarmLevel;

        }
    }
    public partial class SequenceService
    {
        #region Status Changed Event
        public delegate void StatusChangedEventHandler(object sender, StatusChangedEventArgs e);
        public event StatusChangedEventHandler StatusChanged;
        #endregion
    }
}
