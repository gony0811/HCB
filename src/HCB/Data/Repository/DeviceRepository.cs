
using HCB.Data.Entity;
using HCB.Data.Interface;
using HCB.IoC;
using Microsoft.EntityFrameworkCore;

namespace HCB.Data.Repository
{

    [Service(Lifetime.Singleton)]
    public class DeviceRepository : DbRepository<Device, AppDb>
    {
        DbSet<MotionDeviceDetail> MotionDeviceDetailRepository;

        public DeviceRepository(AppDb db) : base(db)
        {
            MotionDeviceDetailRepository = db.Set<MotionDeviceDetail>();
        }


        public async Task<MotionDeviceDetail> AddMotionDeviceDetail(MotionDeviceDetail entity, CancellationToken ct = default)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            MotionDeviceDetailRepository.Add(entity);
            await SaveAsync(ct);
            return entity;
        }

    }
}
