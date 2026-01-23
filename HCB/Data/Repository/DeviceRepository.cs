
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
        DbSet<IoDeviceDetail> IoDeviceDetailRepository;

        public DeviceRepository(IDbContextFactory<AppDb> factory, AppDb db) : base(factory, db)
        {
            MotionDeviceDetailRepository = db.Set<MotionDeviceDetail>();
            IoDeviceDetailRepository = db.Set<IoDeviceDetail>();
        }

        public async Task<MotionDeviceDetail?> FindMotionDeviceDetail(int deviceId, CancellationToken ct = default)
        {
            return await MotionDeviceDetailRepository
                            .FirstOrDefaultAsync(x => x.DeviceId == deviceId, ct);
        }

        public async Task<IoDeviceDetail?> FindIoDeviceDetail(int deviceId, CancellationToken ct = default)
        {
            return await IoDeviceDetailRepository
                            .FirstOrDefaultAsync(x => x.DeviceId == deviceId,  ct);
        }

        public async Task<MotionDeviceDetail> AddMotionDeviceDetail(MotionDeviceDetail entity, CancellationToken ct = default)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            MotionDeviceDetailRepository.Add(entity);
            await SaveAsync(ct);
            return entity;
        }

        public async Task<MotionDeviceDetail> UpdateMotionDeviceDetail(MotionDeviceDetail entity, CancellationToken ct = default)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            MotionDeviceDetailRepository.Update(entity);
            await SaveAsync(ct);
            return entity;
        }

        public async Task<IoDeviceDetail> AddIoDeviceDetail(IoDeviceDetail entity, CancellationToken ct = default)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            IoDeviceDetailRepository.Add(entity);
            await SaveAsync(ct);
            return entity;
        }
        public async Task<IoDeviceDetail> UpdateIoDeviceDetail(IoDeviceDetail entity, CancellationToken ct = default)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            IoDeviceDetailRepository.Update(entity);
            await SaveAsync(ct);
            return entity;
        }
    }
}
