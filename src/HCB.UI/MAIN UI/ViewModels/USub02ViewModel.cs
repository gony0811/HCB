using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.Data.Entity;
using HCB.Data.Entity.Type;
using HCB.Data.Repository;
using HCB.IoC;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using ValueType = HCB.Data.Entity.Type.ValueType;

namespace HCB.UI
{
    [ViewModel(Lifetime.Singleton)]
    public partial class USub02ViewModel : ObservableObject
    {

        private readonly DialogService _dialogService;
        private RecipeService _recipeService;

        // 목록들
        [ObservableProperty] private ObservableCollection<RecipeDto> recipes = new ObservableCollection<RecipeDto>();
        [ObservableProperty] private ObservableCollection<RecipeParam> recipeParam = new ObservableCollection<RecipeParam>();

        // 선택/상태
        [ObservableProperty] private RecipeDto selectedRecipe;
        [ObservableProperty] private RecipeParamDto selectedParam;
        [ObservableProperty] private StepRecipeDto selectedStep;
        [ObservableProperty] private bool isBusy;

        /// <summary>구역별 표 + 인라인 수정 상태</summary>
        public ParamEditor Editor { get; } = new ParamEditor(ParamCatalog.Recipe);

        // (필요 시) 기타 UI 상태
        [ObservableProperty] private string currentDevice;
        [ObservableProperty] private bool parameterType = true; // False: 공용, True: 기본
        [ObservableProperty] private ObservableCollection<DeviceItem> devices = new ObservableCollection<DeviceItem>();
        [ObservableProperty] private DeviceItem selectedDevice;
        [ObservableProperty] private DeviceItem activeDevice;
        [ObservableProperty] private ObservableCollection<ParameterModel> items = new ObservableCollection<ParameterModel>();

        // 룩업(다이얼로그에 전달 용)
        public UnitType UnitType { get; set; }
        public ValueType ValueType { get; set; }

        public USub02ViewModel(DialogService dialogService, RecipeService recipeService)
        {
            this._dialogService = dialogService;
            this._recipeService = recipeService;
            Recipes = _recipeService.RecipeList;
            Editor.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ParamEditor.SelectedRow))
                    SelectedParam = Editor.SelectedRow?.Source as RecipeParamDto;
            };
        }

        partial void OnSelectedRecipeChanging(RecipeDto oldValue, RecipeDto newValue)
        {
            // 레시피를 바꾸기 전에 저장하지 않은 입력값 처리
            var dirty = Editor.DirtyRows;
            if (dirty.Count == 0 || oldValue == null || ReferenceEquals(oldValue, newValue)) return;

            bool save = _dialogService.ShowConfirm("저장하지 않은 변경",
                $"[{oldValue.Name}] 레시피에 저장하지 않은 변경 {dirty.Count}건이 있습니다.\n저장하시겠습니까? (아니오: 변경 취소)");
            if (save && dirty.All(r => !r.HasError))
                _ = SaveRowsAsync(dirty);
            else
                Editor.RevertAllCommand.Execute(null);
        }

        partial void OnSelectedRecipeChanged(RecipeDto oldValue, RecipeDto newValue)
        {
            if (oldValue?.ParamList != null) oldValue.ParamList.CollectionChanged -= OnParamListChanged;
            if (newValue?.ParamList != null) newValue.ParamList.CollectionChanged += OnParamListChanged;
            ReloadRows();
        }

        private void OnParamListChanged(object sender, NotifyCollectionChangedEventArgs e) => ReloadRows();

        private void ReloadRows()
        {
            if (SelectedRecipe?.ParamList == null) Editor.Clear();
            else Editor.Load(SelectedRecipe.ParamList.Select(p => ParamRow.From(p, ParamCatalog.Recipe, SelectedRecipe.Component)),
                             SelectedRecipe.Component);
        }

        [RelayCommand]
        public async Task SaveParamChanges()
        {
            var dirty = Editor.DirtyRows;
            if (dirty.Count == 0) return;

            var invalid = dirty.Where(r => r.HasError).ToList();
            if (invalid.Count > 0)
            {
                _dialogService.ShowMessage("입력 오류", string.Join("\n", invalid.Select(r => $"{r.Name}: {r.Error}")));
                return;
            }

            if (!_dialogService.ShowConfirm("레시피 파라미터 저장",
                    $"[{SelectedRecipe?.Name}] {dirty.Count}건을 저장하시겠습니까?\n\n{ParamEditor.BuildSummary(dirty)}")) return;

            await SaveRowsAsync(dirty);
        }

        private async Task SaveRowsAsync(IReadOnlyList<ParamRow> rows)
        {
            int saved = 0;
            try
            {
                foreach (var row in rows)
                {
                    row.ApplyToSource();
                    await _recipeService.UpdateRecipeParam((RecipeParamDto)row.Source);
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
        public async Task CreateRecipe(CancellationToken ct = default(CancellationToken))
        {
            var recipe = new RecipeCreateDto();

            bool? result = await _dialogService.ShowEditDialog(recipe);

            if (result != true) return;

            RecipeDto added;
            try
            {
                added = await _recipeService.AddRecipe(new RecipeDto { Name = recipe.Name, IsActive = recipe.IsActive, Component = recipe.Component });
            }
            catch (DbUpdateException ex)
            {
                _dialogService.ShowMessage("저장 오류", $"저장중 오류가 발생했습니다: {ex.GetBaseException().Message}");
                return;
            }

            SelectedRecipe = added;
            var (addedCount, emptyCount, error) = await AddMissingRequiredAsync(added);
            if (error != null)
            {
                _dialogService.ShowMessage("필수 파라미터 추가 실패",
                    $"[{added.Name}] 레시피는 생성되었지만 필수 파라미터 추가 중 오류가 발생했습니다 ({addedCount}개 추가됨): {error}");
                return;
            }

            _dialogService.ShowMessage("저장",
                $"[{added.Name}] {added.Component} 레시피를 생성하고 필수 파라미터 {addedCount}개를 추가했습니다." +
                (emptyCount > 0 ? $"\n값이 비어 있는 {emptyCount}개(빨간색)는 직접 입력 후 [변경 저장] 해주세요." : ""));
        }

        /// <summary>
        /// 레시피 종류(DIE/WAFER)별 필수 파라미터 중 없는 것을 추가한다.
        /// 보정 오프셋은 0, 두께·위치 등 물리값은 빈 값(직접 입력 필요)으로 넣는다.
        /// </summary>
        private async Task<(int Added, int Empty, string Error)> AddMissingRequiredAsync(RecipeDto recipe)
        {
            int addedCount = 0, emptyCount = 0;
            try
            {
                foreach (var def in ParamCatalog.Recipe.RequiredFor(recipe.Component))
                {
                    if (recipe.ParamList.Any(p => p.Name == def.Name)) continue;
                    await _recipeService.AddRecipeParam(new RecipeParamDto
                    {
                        RecipeId = recipe.Id,
                        Name = def.Name,
                        Value = def.Default ?? "",
                        ValueType = def.Type,
                        UnitType = def.Unit,
                        Description = def.Description
                    });
                    addedCount++;
                    if (def.Default == null) emptyCount++;
                }
                return (addedCount, emptyCount, null);
            }
            catch (Exception ex)
            {
                return (addedCount, emptyCount, ex.GetBaseException().Message);
            }
        }

        /// <summary>선택 레시피에 빠진 필수 파라미터 추가 (누락 경고의 [필수 항목 추가])</summary>
        [RelayCommand]
        public async Task AddMissingRequired()
        {
            if (SelectedRecipe == null) return;
            var recipe = SelectedRecipe;
            var (addedCount, emptyCount, error) = await AddMissingRequiredAsync(recipe);
            if (error != null)
            {
                _dialogService.ShowMessage("필수 파라미터 추가 실패", $"{addedCount}개 추가 후 오류: {error}");
                return;
            }
            _dialogService.ShowMessage("추가",
                $"[{recipe.Name}] {recipe.Component} 필수 파라미터 {addedCount}개를 추가했습니다." +
                (emptyCount > 0 ? $"\n값이 비어 있는 {emptyCount}개(빨간색)는 직접 입력 후 [변경 저장] 해주세요." : ""));
        }

        [RelayCommand]
        public async Task UpdateRecipe()
        {
            if (SelectedRecipe == null) return;

            var recipe = new RecipeCreateDto
            {
                Name = SelectedRecipe.Name,
                IsActive = SelectedRecipe.IsActive,
                Component = SelectedRecipe.Component
            };

            bool? result = await _dialogService.ShowEditDialog(recipe);

            if (result != true) return;

            try
            {
                SelectedRecipe.Name = recipe.Name;
                SelectedRecipe.IsActive = recipe.IsActive;
                SelectedRecipe.Component = recipe.Component;
                await _recipeService.UpdateRecipe(SelectedRecipe);
                ReloadRows();   // 종류(DIE/WAFER)가 바뀌면 필수 항목 기준도 바뀜
                _dialogService.ShowMessage("저장", "저장되었습니다");
            }
            catch (DbUpdateException ex)
            {
                _dialogService.ShowMessage("저장 오류", "저장중 오류가 발생했습니다");
            }
        }

        [RelayCommand]
        public async Task DeleteRecipe()
        {
            if (SelectedRecipe != null)
            {
                if (SelectedRecipe.IsActive)
                {
                    _dialogService.ShowMessage("경고", "사용중인 레시피는 삭제하실 수 없습니다");
                    return;
                }
                await _recipeService.DeleteRecipe(SelectedRecipe);
                SelectedRecipe = null;
            }
            else
            {
                _dialogService.ShowMessage("레시피 선택 필요", "레시피를 선택해주세요");
            }
        }

        [RelayCommand]
        public async Task CopyRecipe()
        {
            if (SelectedRecipe != null)
            {
                bool result =  _dialogService.ShowConfirm("레시피 복사", "복사하시겠습니까?");
                if (result)
                {
                    try
                    {
                        await _recipeService.CopyRecipe(SelectedRecipe);
                    }
                    catch (DbUpdateException ex)
                    {
                        _dialogService.ShowMessage("중복 오류", "동일한 이름의 레시피가 존재합니다");
                    }
                }
            }
        }

        [RelayCommand]
        public async Task UseChange()
        {
            if (SelectedRecipe == null) return;

            bool result = _dialogService.ShowConfirm("사용 레시피 변경", "사용할 레시피를 변경하시겠습니까?");
            if (!result) return;

            try
            {
                bool visionNotified = await _recipeService.SetUseRecipeAsync(SelectedRecipe);
                if (!visionNotified)
                    _dialogService.ShowMessage("알림", "VISION_RECIPE 파라미터가 없어 비전에 통보하지 못했습니다");

                _dialogService.ShowMessage("변경", "사용 레시피가 변경되었습니다");
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage("에러", ex.GetBaseException().Message);
            }
        }

        [RelayCommand]
        public async Task CreateParam()
        {
            if (SelectedRecipe == null)
            {
                _dialogService.ShowMessage("레시피 선택 필요", "왼쪽 RECIPE LIST에서 파라미터를 추가할 레시피를 먼저 선택해주세요");
                return;
            }
            var recipe = SelectedRecipe;

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
                var dto = new RecipeParamDto
                {
                    RecipeId = recipe.Id,
                    Name = param.Name.Trim(),
                    Value = param.Value.Trim(),
                    Minimum = param.Minimum,
                    Maximum = param.Maximum,
                    ValueType = param.ValueType,
                    UnitType = param.UnitType,
                    Description = param.Description
                };

                await _recipeService.AddRecipeParam(dto);
                Editor.SelectByName(dto.Name);
                _dialogService.ShowMessage("저장", $"[{recipe.Name}] {dto.Name} 생성되었습니다");
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
                await _recipeService.UpdateRecipeParam(SelectedParam);
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

                await _recipeService.DeleteRecipeParam(SelectedParam);
                SelectedParam = null;
            }
            else
            {
                _dialogService.ShowMessage("파라미터 선택 필요", "파라미터를 선택해주세요");
            }
        }

        [RelayCommand]
        public async Task AddStep()
        {
            if (SelectedRecipe == null)
            {
                _dialogService.ShowMessage("레시피 선택 필요", "레시피를 선택해주세요");
                return;
            }

            try
            {
                int nextStep = SelectedRecipe.StepList.Count > 0
                    ? SelectedRecipe.StepList.Max(s => s.StepNumber) + 1
                    : 1;

                var dto = new StepRecipeDto
                {
                    RecipeId = SelectedRecipe.Id,
                    StepNumber = nextStep,
                    Name = "",
                    Description = ""
                };

                await _recipeService.AddStep(dto);
            }
            catch (Exception e)
            {
                _dialogService.ShowMessage("저장 실패", "스텝 추가 실패");
            }
        }

        [RelayCommand]
        public async Task UpdateStep()
        {
            if (SelectedStep == null) return;
            try
            {
                var stepEdit = new StepRecipeCreateDto
                {
                    Name = SelectedStep.Name,
                    StepNumber = SelectedStep.StepNumber,
                    AccTime = SelectedStep.AccTime,
                    AccTime2 = SelectedStep.AccTime2,
                    ContTime = SelectedStep.ContTime,
                    DecTime = SelectedStep.DecTime,
                    LoadCell = SelectedStep.LoadCell,
                    Current = SelectedStep.Current,
                    Current2 = SelectedStep.Current2,
                    VacOffTime = SelectedStep.VacOffTime,
                    BlowEnable = SelectedStep.BlowEnable,
                    BlowOnTime = SelectedStep.BlowOnTime,
                    BlowDuration = SelectedStep.BlowDuration,
                    Description = SelectedStep.Description
                };
                bool? result = await _dialogService.ShowEditDialog(stepEdit);
                if (result != true) return;

                SelectedStep.Name = stepEdit.Name;
                SelectedStep.AccTime = stepEdit.AccTime;
                SelectedStep.AccTime2 = stepEdit.AccTime2;
                SelectedStep.ContTime = stepEdit.ContTime;
                SelectedStep.DecTime = stepEdit.DecTime;
                SelectedStep.LoadCell = stepEdit.LoadCell;
                SelectedStep.Current = stepEdit.Current;
                SelectedStep.Current2 = stepEdit.Current2;
                SelectedStep.VacOffTime = stepEdit.VacOffTime;
                SelectedStep.BlowEnable = stepEdit.BlowEnable;
                SelectedStep.BlowOnTime = stepEdit.BlowOnTime;
                SelectedStep.BlowDuration = stepEdit.BlowDuration;
                SelectedStep.Description = stepEdit.Description;

                await _recipeService.UpdateStep(SelectedStep);
                _dialogService.ShowMessage("저장", "저장되었습니다");
            }
            catch (Exception e)
            {
                _dialogService.ShowMessage("저장 실패", "스텝 수정 실패");
            }
        }

        [RelayCommand]
        public async Task DeleteStep()
        {
            if (SelectedStep != null)
            {
                bool result = _dialogService.ShowConfirm("스텝 삭제", $"Step {SelectedStep.StepNumber}을 삭제하시겠습니까?");
                if (!result) return;

                await _recipeService.DeleteStep(SelectedStep);
                SelectedStep = null;
            }
            else
            {
                _dialogService.ShowMessage("스텝 선택 필요", "스텝을 선택해주세요");
            }
        }
    }
}
