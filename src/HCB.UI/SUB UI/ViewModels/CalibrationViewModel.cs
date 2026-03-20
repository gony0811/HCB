using CommunityToolkit.Mvvm.ComponentModel;
using HCB.IoC;
using System;
using System.Threading.Tasks;
using static HCB.UI.SERVICE.CalibrationService;

namespace HCB.UI
{
    [Service(Lifetime.Singleton)]
    public partial class CalibrationViewModel : ObservableObject
    {
        private readonly EqpCommunicationService _communicationService;

        [ObservableProperty] private Point2D bottomLeft;
        [ObservableProperty] private Point2D bottomRight;

        // Top 좌표
        [ObservableProperty] private Point2D topLeft;
        [ObservableProperty] private Point2D topRight;

        public CalibrationViewModel(EqpCommunicationService eqpCommunicationService)
        {
            _communicationService = eqpCommunicationService;
        }

        public async Task RequestAlignMark(DieType dieType, DirectType directType)
        {
            await _communicationService.RequestAFStart(CameraType.HC2_HIGH, MarkType.ALIGN_MARK_TOP);
            MarkType markType = MarkType.ALIGN_MARK;
            if(dieType == DieType.TOP)
            {
                markType = MarkType.ALIGN_MARK_TOP;
            }
            var result =  await _communicationService.RequestVisionMarkPosition(markType, CameraType.HC2_HIGH, directType.ToString());

            Console.WriteLine(result);
            //switch (directType)
            //{
            //    case DirectType.LEFT:
            //        if (dieType == DieType.TOP) 
            //            Top
            //        if (dieType == DieType.BOTTOM)
            //                    break;
            //    case DirectType.RIGHT:
            //        break;
            //}

            
        }
    }
}
