
using HCB.Data.Entity.Type;
using HCB.Data.Entity;

namespace HCB.UI
{

    // 1. 인터페이스 정의
    public interface IDeviceFactory
    {
        IDevice Create(Device entity);
    }

}