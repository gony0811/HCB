namespace HCB.Data.Entity.Type
{
    public enum AlarmLevel
    {
        Light = 1,      // 설비 가동은 하지만 추후 문제가 발생할 가능성이 있는 경우 발생시
        HEAVY = 2,      // 즉시 설비 가동을 중지해야하는 중대한 문제 발생시
    }
    public enum AlarmStatus
    {
        RESET = 0,       // 알람이 해제된 상태
        SET = 1,         // 알람이 설정된 상태  
    }

    public enum AlarmEnable
    {

        DISABLED = 0,    // 알람이 비활성화된 상태
        ENABLED = 1,     // 알람 사용
    }
}
