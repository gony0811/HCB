using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.IoC;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace HCB.UI
{
    [ViewModel(Lifetime.Singleton)]
    public partial class USub09ViewModel : ObservableObject
    {
        private readonly DialogService _dialogService;
        private readonly ECParamService _ecParamService;

        [ObservableProperty] private ObservableCollection<ECParamDto> paramList;
        [ObservableProperty] private ECParamDto selectedParam;

        public USub09ViewModel(DialogService dialogService, ECParamService ecParamService)
        {
            _dialogService = dialogService;
            _ecParamService = ecParamService;
            ParamList = _ecParamService.ParamList;
        }

        [RelayCommand]
        public async Task CreateParam()
        {
            try
            {
                var param = new ParameterCreateDto();
                bool? result = await _dialogService.ShowEditDialog(param);
                if (result != true) return;

                var dto = new ECParamDto
                {
                    Name = param.Name,
                    Value = param.Value,
                    Minimum = param.Minimum,
                    Maximum = param.Maximum,
                    ValueType = param.ValueType,
                    UnitType = param.UnitType,
                    Description = param.Description
                };

                await _ecParamService.AddParam(dto);
                _dialogService.ShowMessage("저장", "저장되었습니다");
            }
            catch (Exception)
            {
                _dialogService.ShowMessage("저장 실패", "파라미터 저장 실패");
            }
        }

        [RelayCommand]
        public async Task UpdateParam()
        {
            if (SelectedParam == null) return;
            try
            {
                var param = new ParameterCreateDto
                {
                    Name = SelectedParam.Name,
                    Value = SelectedParam.Value,
                    Minimum = SelectedParam.Minimum,
                    Maximum = SelectedParam.Maximum,
                    ValueType = SelectedParam.ValueType,
                    UnitType = SelectedParam.UnitType,
                    Description = SelectedParam.Description
                };
                bool? result = await _dialogService.ShowEditDialog(param);
                if (result != true) return;

                SelectedParam.Name = param.Name;
                SelectedParam.Value = param.Value;
                SelectedParam.Minimum = param.Minimum;
                SelectedParam.Maximum = param.Maximum;
                SelectedParam.ValueType = param.ValueType;
                SelectedParam.UnitType = param.UnitType;
                SelectedParam.Description = param.Description;

                await _ecParamService.UpdateParam(SelectedParam);
                _dialogService.ShowMessage("저장", "저장되었습니다");
            }
            catch (Exception)
            {
                _dialogService.ShowMessage("저장 실패", "파라미터 저장 실패");
            }
        }

        [RelayCommand]
        public async Task DeleteParam()
        {
            if (SelectedParam != null)
            {
                bool result = _dialogService.ShowConfirm("파라미터 삭제", $"{SelectedParam.Name}을 삭제하시겠습니까?");
                if (!result) return;

                await _ecParamService.DeleteParam(SelectedParam);
                SelectedParam = null;
            }
            else
            {
                _dialogService.ShowMessage("파라미터 선택 필요", "파라미터를 선택해주세요");
            }
        }
    }
}
