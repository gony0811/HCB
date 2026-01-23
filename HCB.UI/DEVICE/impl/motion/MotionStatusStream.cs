using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;

namespace HCB.UI
{
    public class MotionStatusStream : IMotionStatusStream
    {
        private readonly Subject<MotionStatus> subject = new();

        public IObservable<MotionStatus> StatusChanged => subject;

        public void Publish(MotionStatus status)
        {
            subject.OnNext(status);
        }
    }
}
