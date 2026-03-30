using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Repository;
using HCB.IoC;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HCB.UI
{
    [Service(Lifetime.Singleton)]
    public partial class ECParamService : ObservableObject
    {
        private readonly ECParamRepository _ecParamRepo;

        [ObservableProperty] private ObservableCollection<ECParamDto> paramList = new ObservableCollection<ECParamDto>();

        public ECParamService(ECParamRepository ecParamRepo)
        {
            _ecParamRepo = ecParamRepo;
        }

        public async Task Initialize(CancellationToken ct = default)
        {
            var list = await _ecParamRepo.ListAsync(
                orderBy: q => q.OrderBy(p => p.Id),
                asNoTracking: true,
                ct: ct);

            foreach (var entity in list)
            {
                ParamList.Add(new ECParamDto().ToDto(entity));
            }
        }

        public async Task AddParam(ECParamDto dto)
        {
            var entity = await _ecParamRepo.AddAsync(dto.ToEntity());
            ParamList.Add(new ECParamDto().ToDto(entity));
        }

        public async Task UpdateParam(ECParamDto dto)
        {
            await _ecParamRepo.Update(dto.ToEntity());
        }

        public async Task DeleteParam(ECParamDto dto)
        {
            await _ecParamRepo.Remove(dto.Id);
            ParamList.Remove(dto);
        }

        public ECParamDto FindByName(string name)
        {
            var param = ParamList.FirstOrDefault(x => x.Name.Equals(name));
            if (param == null) throw new Exception($"{name} EC 파라미터가 없습니다");
            return param;
        }
    }
}
