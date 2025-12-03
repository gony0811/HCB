using CommunityToolkit.Mvvm.ComponentModel;

namespace HCB.UI
{
    public partial class StateCellVM : ObservableObject
    {
        [ObservableProperty] private string statusName; // 상태를 표현할 오브젝트 이름
        [ObservableProperty] private State state;       // 상태 정보.

        public StateCellVM()
        {
            State = State.Offline;
            StatusName = "";
        }

        public StateCellVM(string statusName, State state)
        {
            StatusName = statusName;
            State = state;
        }
    }
}
