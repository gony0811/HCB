using Microsoft.Xaml.Behaviors.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HCB.UI
{
    public static partial class MotionExtensions
    {
        public const string PowerPmacDeviceName = "PMAC";
        public const string PMacIoDeviceName = "PMacIO";

        public const string D_Y = "D_Y";
        public const string P_Y = "P_Y";
        public const string W_Y = "W_Y";

        public const string W_T = "W_T";
        public const string H_X = "H_X";
        public const string H_T = "H_T";

        public const string H_Z = "H_Z";
        public const string h_z = "h_z";


        // -----------POSITION NAMES----------------------------
        // WAFER LOAD

        // 사용 축: H-Z, h-z
        public const string HEAD_SAFETY = "SAFETY";
        public const string BTM_VISION_LOW = "BTM_VISION_LOW";
        public const string BTM_VISION_HIGH = "BTM_VISION_HIGH";
        public const string DIE_CARRIER_ALIGN_LOW = "DIE_CARRIER_ALIGN_LOW";
        public const string WAFER_ALIGN_LOW = "WAFER_ALIGN_LOW";
        public const string WAFER_ALIGN_1 = "WAFER_ALIGN_1";
        public const string WAFER_ALIGN_2 = "WAFER_ALIGN_2";
        public const string WAFER_ALIGN_3 = "WAFER_ALIGN_3";
        public const string TOP_DIE_VISION = "TOP_DIE_VISION";
        public const string PICKUP_STANBY = "PICKUP_STANBY";
        

        // h-z
        public const string DIE_PICKUP = "DIE_PICKUP";

        // 사용 축: H-X, D-Y
        public const string DIE_CARRIER_ALIGN_1 = "DIE_CARRIER_ALIGN_1";
        public const string DIE_CARRIER_ALIGN_2 = "DIE_CARRIER_ALIGN_2";
        public const string DIE_CARRIER_ALIGN_3 = "DIE_CARRIER_ALIGN_3";
        public const string DIE_LOADING = "DIE_LOAD";
        public const string DIE_READY = "DIE_READY";

        // 사용 축: H-X, W-Y
        public const string BONDING_ALIGN_1 = "BONDING_ALIGN_1";
        public const string BONDING_ALIGN_2 = "BONDING_ALIGN_2";
        public const string BONDING = "BONDING";
        public const string WAFER_CENTER_POSITION = "WAFER_CENTER";
        public const string WAFER_RIGHT_POSITION = "WAFER_RIGHT";
        public const string WAFER_LEFT_POSITION = "WAFER_LEFT";
        public const string WAFER_LOADING = "WAFER_LOAD";
        public const string WAFER_READY = "WAFER_READY";

        // 사용 축: H_X, P_Z
        public const string BTM_VISION = "BTM_VISION";


        // -- 
        public const string READY_POSITION = "READY";
        public const string LOAD_POSITION = "LOAD";

        public const string DIE_COLUMN_1 = "DIE_COLUMN_1";
        public const string DIE_COLUMN_2 = "DIE_COLUMN_2";
        public const string DIE_COLUMN_3 = "DIE_COLUMN_3";
        public const string DIE_ROW_1 = "DIE_ROW_1";
        public const string DIE_ROW_2 = "DIE_ROW_2";
        public const string DIE_ROW_3 = "DIE_ROW_3";

        public const string DTABLE_CENTER_POSITION = "DTABLE_CENTER";
        public const string DIE_1_PICK_POSITION = "DIE_1_PICK";
        public const string DIE_2_PICK_POSITION = "DIE_2_PICK";
        public const string DIE_3_PICK_POSITION = "DIE_3_PICK";
        public const string DIE_4_PICK_POSITION = "DIE_4_PICK";
        public const string DIE_5_PICK_POSITION = "DIE_5_PICK";
        public const string DIE_6_PICK_POSITION = "DIE_6_PICK";
        public const string DIE_7_PICK_POSITION = "DIE_7_PICK";
        public const string DIE_8_PICK_POSITION = "DIE_8_PICK";
        public const string DIE_9_PICK_POSITION = "DIE_9_PICK";
        public const string DIE_VISION_LOW= "DIE_VISION_LOW";

        public const string ORIGIN = "ORIGIN";
        public const string P_LEFT_FIDUCIAL_HIGH = "P_LEFT_FIDUCIAL_HIGH";
        public const string P_RIGHT_FIDUCIAL_HIGH = "P_RIGHT_FIDUCIAL_HIGH";
        public const string P_LEFT_CORNER_HIGH = "P_LEFT_CORNER_HIGH";
        public const string P_RIGHT_CORNER_HIGH = "P_RIGHT_CORNER_HIGH";
    }
}
