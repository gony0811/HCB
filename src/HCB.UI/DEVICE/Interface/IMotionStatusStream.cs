using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HCB.UI
{
    public interface IMotionStatusStream
    {
        IObservable<MotionStatus> StatusChanged { get; }

        void Publish(MotionStatus status);
    }

}
