using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity.Type;
using HCB.Data.Repository;
using HCB.IoC;
using HCB.UI;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telerik.Windows.Persistence.Core;
using ValueType = HCB.Data.Entity.Type.ValueType;

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
        if (param == null) return new ECParamDto();
        return param;
    }

    public double GetDouble(string name)
    {
        var param = ParamList.FirstOrDefault(x => x.Name.Equals(name));
        if (param == null) throw new Exception("파라미터를 찾을 수 없습니다");
        return double.Parse(param.Value);
    }

    public async Task SetOrUpdate(string name, double value, string description = "")
    {
        var param = FindByName(name);
        if (string.IsNullOrEmpty(param.Name))
        {
            await AddParam(new ECParamDto
            {
                Name = name,
                Value = value.ToString("F8"),
                ValueType = ValueType.Double,
                UnitType = UnitType.None,
                Description = description
            });
        }
        else
        {
            param.Value = value.ToString("F8");
            await UpdateParam(param);
        }
    }
}