using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog;

namespace HCB.UI
{

    internal class UILog
    {
        public string Page { get; set; }
        public string User { get; set; }
        public string Message { get; set; }

        public UILog()
        {

        }

        public UILog(string page, string user, string message)
        {
            Page = page;
            User = user;
            Message = message;
        }

        public override string ToString()
        {
            return $"[UI] Page: {Page}, User: {User}, Message: {Message}";
        }
    }

    internal class SysLog
    {
        public SysLog()
        {
        }

        public SysLog(string system, string availability, string run, string alarm, string operation, string message)
        {
            System = system;
            Availability = availability;
            Run = run;
            Alarm = alarm;
            Operation = operation;
            Message = message;
        }

        /// <summary>
        /// 로그를 남기는 시스템 이름 (예: Interlock Service, Sequence Service 등)
        /// </summary>
        public string System { get; set; }

        /// <summary>
        /// 현재 시스템 상태 
        /// 예: Availability (DOWN/UP), Run (Ready/Run/Stop/Pause), Alarm (Heavy/Light/NoAlarm), Operation (MANUAL/AUTO)
        /// </summary>
        public string Availability { get; set; }

        public string Run { get; set; }

        public string Alarm { get; set; }

        public string Operation { get; set; }

        public string Message { get; set; }

        public override string ToString()
        {
            return $"[{System}] Operation: {Operation}, Run: {Run}, Alarm: {Alarm}, Message: {Message}";
        }
    }
}
