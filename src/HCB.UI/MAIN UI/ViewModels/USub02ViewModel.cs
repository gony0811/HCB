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
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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
        [ObservableProperty] private bool isBusy;

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
        } 

        [RelayCommand]
        public async Task CreateRecipe(CancellationToken ct = default(CancellationToken))
        {
            var recipe = new RecipeCreateDto();

            bool? result = await _dialogService.ShowEditDialog(recipe);

            if (result != true) return;

            try
            {
                
                await _recipeService.AddRecipe(new RecipeDto { Name = recipe.Name, IsActive = recipe.IsActive });

                _dialogService.ShowMessage("저장", "저장되었습니다");
            }
            catch (DbUpdateException ex)
            {
                MessageBox.Show(ex.Message);
                MessageBox.Show("저장 중 오류가 발생했습니다.", "저장 오류");
            }
        }

        [RelayCommand]
        public async Task DeleteRecipe()
        {
            if (SelectedRecipe != null)
            {
                if(SelectedRecipe.IsActive)
                {
                    _dialogService.ShowMessage("경고", "사용중인 레시피는 삭제하실 수 없습니다");
                    return;
                }
                await _recipeService.DeleteRecipe(SelectedRecipe);
                SelectedRecipe = null;
            }else
            {
                _dialogService.ShowMessage("레시피 선택 필요", "레시피를 선택해주세요");
            }
        }

        [RelayCommand]
        public async Task CopyRecipe()
        {
            if ( SelectedRecipe != null)
            {
                bool result = _dialogService.ShowMessage("레시피 복사", "복사하시겠습니까?");
                if (result)
                {
                    await _recipeService.CopyRecipe(SelectedRecipe);
                }
            }
        }

        [RelayCommand]
        public async Task UseChange()
        {
            if (SelectedRecipe == null) return;
            try
            {
                SelectedRecipe.IsActive = true;
                await _recipeService.UpdateRecipe(SelectedRecipe);
                _dialogService.ShowMessage("변경", "사용 레시피가 변경되었습니다");
            }catch(Exception ex)
            {
                _dialogService.ShowMessage("에러", ex.GetBaseException().Message);
            }
        }
    }
}
