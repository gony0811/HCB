namespace HCB.UI
{



    #region HomeExcutionMode 홈 실행 모드
    public enum HomeExecutionMode
    {
        PLC = 0,
        PC = 1
    }
    #endregion
    #region JogMoveType 조그 이동 명령 타입
    public enum JogMoveType
    {
        Plus,
        Minus,
        Stop
    }

    public enum MoveType
    {
        Absolute,
        Relative
    }
    #endregion
}

