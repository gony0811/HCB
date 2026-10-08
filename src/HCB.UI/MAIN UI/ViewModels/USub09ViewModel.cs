using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.IoC;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
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

        /// <summary>구역별 표 + 인라인 수정 상태</summary>
        public ParamEditor Editor { get; } = new ParamEditor(ParamCatalog.EC);

        public USub09ViewModel(DialogService dialogService, ECParamService ecParamService)
        {
            _dialogService = dialogService;
            _ecParamService = ecParamService;
            ParamList = _ecParamService.ParamList;
            ParamList.CollectionChanged += OnParamListChanged;
            Editor.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ParamEditor.SelectedRow))
                    SelectedParam = Editor.SelectedRow?.Source as ECParamDto;
            };
            ReloadRows();
        }

        private void OnParamListChanged(object sender, NotifyCollectionChangedEventArgs e) => ReloadRows();

        private void ReloadRows()
            => Editor.Load(ParamList.Select(p => ParamRow.From(p, ParamCatalog.EC)));

        [RelayCommand]
        public async Task SaveChanges()
        {
            var dirty = Editor.DirtyRows;
            if (dirty.Count == 0) return;

            var invalid = dirty.Where(r => r.HasError).ToList();
            if (invalid.Count > 0)
            {
                _dialogService.ShowMessage("입력 오류",
                    string.Join("\n", invalid.Select(r => $"{r.Name}: {r.Error}")));
                return;
            }

            string summary = ParamEditor.BuildSummary(dirty);
            if (!_dialogService.ShowConfirm("EC 파라미터 저장", $"{dirty.Count}건을 저장하시겠습니까?\n\n{summary}")) return;

            int saved = 0;
            try
            {
                foreach (var row in dirty)
                {
                    row.ApplyToSource();
                    await _ecParamService.UpdateParam((ECParamDto)row.Source);
                    saved++;
                }
                _dialogService.ShowMessage("저장", $"{saved}건 저장되었습니다");
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage("저장 실패", $"{saved}건 저장 후 실패: {ex.GetBaseException().Message}");
            }
        }

        [RelayCommand]
        public async Task CreateParam()
        {
            // 입력 오류가 있으면 입력값을 유지한 채 다시 연다
            var param = Editor.NewParamTemplate();
            while (true)
            {
                bool? result = await _dialogService.ShowEditDialog(param);
                if (result != true) return;

                string error = Editor.ValidateParam(param);
                if (error == null) break;
                _dialogService.ShowMessage("입력 확인", error);
            }

            try
            {
                var dto = new ECParamDto
                {
                    Name = param.Name.Trim(),
                    Value = param.Value.Trim(),
                    Minimum = param.Minimum,
                    Maximum = param.Maximum,
                    ValueType = param.ValueType,
                    UnitType = param.UnitType,
                    Description = param.Description
                };

                await _ecParamService.AddParam(dto);
                Editor.SelectByName(dto.Name);
                _dialogService.ShowMessage("저장", $"{dto.Name} 생성되었습니다");
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage("저장 실패", $"파라미터 저장 실패: {ex.GetBaseException().Message}");
            }
        }

        [RelayCommand]
        public async Task UpdateParam()
        {
            if (SelectedParam == null)
            {
                _dialogService.ShowMessage("파라미터 선택 필요", "파라미터를 선택해주세요");
                return;
            }
            // 표에서 입력 중이던 값이 있으면 그 값으로 상세 수정을 시작한다
            var row = Editor.Rows.FirstOrDefault(r => ReferenceEquals(r.Source, SelectedParam));
            try
            {
                var param = new ParameterCreateDto
                {
                    Name = SelectedParam.Name,
                    Value = row?.IsDirty == true ? row.EditValue : SelectedParam.Value,
                    Minimum = SelectedParam.Minimum,
                    Maximum = SelectedParam.Maximum,
                    ValueType = SelectedParam.ValueType,
                    UnitType = SelectedParam.UnitType,
                    Description = SelectedParam.Description
                };
                while (true)
                {
                    bool? result = await _dialogService.ShowEditDialog(param);
                    if (result != true) return;

                    string error = Editor.ValidateParam(param, SelectedParam);
                    if (error == null) break;
                    _dialogService.ShowMessage("입력 확인", error);
                }

                // 상세 수정으로 저장한 값이 표의 미저장 입력값보다 우선
                row?.Revert();

                SelectedParam.Name = param.Name.Trim();
                SelectedParam.Value = param.Value.Trim();
                SelectedParam.Minimum = param.Minimum;
                SelectedParam.Maximum = param.Maximum;
                SelectedParam.ValueType = param.ValueType;
                SelectedParam.UnitType = param.UnitType;
                SelectedParam.Description = param.Description;

                await _ecParamService.UpdateParam(SelectedParam);
                ReloadRows();
                _dialogService.ShowMessage("저장", "저장되었습니다");
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage("저장 실패", $"파라미터 저장 실패: {ex.GetBaseException().Message}");
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
