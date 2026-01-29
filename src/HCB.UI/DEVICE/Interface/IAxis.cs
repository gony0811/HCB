using HCB.Data.Entity;
using HCB.Data.Entity.Type;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace HCB.UI
{
    public interface IAxis 
    {
        int Id { get; set; }
        string Name { get; set; }
        int MotorNo { get; set; }
        //string ControlType { get; set; }
        bool IsEnabled { get; set; }
        bool IsBusy { get; set; }
        bool IsError { get; set; }
        bool InPosition { get; set; }
        bool IsHome { get; set; }
        bool IsPlusLimit { get; set; }
        bool IsMinusLimit { get; set; }
        bool IsMotionDone { get; set; }
        bool IsHomeDone { get; set; }
        double CurrentSpeed { get; set; }
        double LimitMinSpeed { get; set; }
        double LimitMaxSpeed { get; set; }
        double SetSpeed { get; set; }

        double CommandPosition { get; set; }        // 명령 위치
        double CurrentPosition { get; set; }           // 현재 위치
        double LimitMinPosition { get; set; }           // 최소 위치
        double LimitMaxPosition { get; set; }           // 최대 위치
        double EncoderCountPerUnit { get; set; }        // 모터의 단위당 엔코더 펄스 수

        int HommingProgramNumber { get; set; }    // 홈 프로그램 번호
        double InpositionRange { get; set; }       // Inposition 허용 범위

        UnitType Unit { get; set; }
        IMotionDevice Device { get; set; }      // 부모 디바이스

        ObservableCollection<DMotionParameter> ParameterList { get; set; }
        ObservableCollection<DMotionPosition> PositionList { get; set; }

        Task<bool> ServoOn();
        Task<bool> ServoOff();

        Task ServoReady(bool ready);    // 서보 온

        Task Move(MoveType moveType, double velocity, double position);
        Task Move(MoveType moveType, double jerk, double velocity, double position);

        Task JogMove(JogMoveType moveType, double jogSpeed);   // 조그 이동

        Task MoveStop();    // 이동 정지

        Task EStop();    // 비상 정지

        Task Home();
    }
}
