using HCB.Data.Entity.Type; 
using System.Threading.Tasks;

namespace HCB.UI
{
    public interface IDevice 
    {
        int Id { get; set; }
        string Name { get; set; }
        DeviceType DeviceType { get; set; }
        string FileName { get; set; }  // 드라이버 파일명 또는 어셈블리 경로
        string InstanceName { get; set; } // 드라이버 클래스명 또는 인스턴스 이름
        bool IsConnected { get; set; }
        bool IsEnabled { get; set; }
        string Description { get; set; }
            

        Task Initialize();
        Task Connect();
        Task Disconnect();
        Task RefreshStatus();  // 상태 갱신
        Task SendCommand(string command);

        Task<TResult> SendCommand<TResult>(string command);
        Task<bool> TestConnection(); // 연결 테스트 (Ping 등)

    }
}
