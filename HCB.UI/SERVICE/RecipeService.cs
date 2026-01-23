using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity;
using HCB.Data.Interface;
using HCB.Data.Repository;
using HCB.IoC;
using Microsoft.EntityFrameworkCore;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HCB.UI
{
    [Service(Lifetime.Singleton)]   
    public partial class RecipeService : ObservableObject
    {
        private readonly RecipeRepository _recipeRepo;
        private readonly ParameterRepository _parameterRepo;

        [ObservableProperty] private ObservableCollection<RecipeDto> recipeList = new ObservableCollection<RecipeDto>();
        [ObservableProperty] private RecipeDto useRecipe;

        public RecipeService(RecipeRepository recipeRepo, ParameterRepository parameterRepo)
        {
            _recipeRepo = recipeRepo;
            _parameterRepo = parameterRepo;
        }

        public async Task Initialize(CancellationToken ct = default)
        { 
            var list = await _recipeRepo.ListAsync(
                include: q => q.Include(r => r.ParamList),
                orderBy: q => q.OrderBy(r => r.Id),
                asNoTracking: true,
                ct: ct);

            foreach(var recipe in list)
            {
                RecipeDto dto = new RecipeDto().ToDto(recipe);
                RecipeList.Add(dto);
                if(dto.IsActive)
                {
                    UseRecipe = dto;
                }
            }
        }

        public async Task AddRecipe(RecipeDto recipeDto)
        {
            // 새 레시피가 활성(true)라면 먼저 기존 활성 레시피를 끄기
            if (recipeDto.IsActive)
                await DisableCurrentActiveRecipeAsync(recipeDto);

            // Insert
            var entity = await _recipeRepo.AddAsync(recipeDto.ToEntity());
            var addedDto = new RecipeDto().ToDto(entity);

            // Insert 후에 활성으로 저장해야 함 (제약조건 때문에 이 순서 필수)
            if (recipeDto.IsActive)
            {
                addedDto.IsActive = true;
                await _recipeRepo.Update(addedDto.ToEntity());
                UseRecipe = addedDto;
            }

            RecipeList.Add(addedDto);
        }

        public async Task UpdateRecipe(RecipeDto recipeDto)
        {
            if (recipeDto.IsActive)
                await DisableCurrentActiveRecipeAsync(recipeDto);

            var updatedEntity = await _recipeRepo.Update(recipeDto.ToEntity());

            // 활성 레시피 설정
            if (recipeDto.IsActive)
                UseRecipe = recipeDto;
        }
        public async Task DeleteRecipe(RecipeDto recipeDto)
        {
            await _recipeRepo.Remove(recipeDto.Id);
            RecipeList.Remove(recipeDto);
        }

        public async Task CopyRecipe(RecipeDto recipDto)
        {
            var newRecipe = new Recipe
            {
                Name = CreateCopyName(recipDto.Name),
                IsActive = false,
                ParamList = new List<RecipeParam>()
            };

            foreach (var p in recipDto.ParamList) 
            {
                newRecipe.ParamList.Add(new RecipeParam
                {
                    Name = p.Name,
                    Value = p.Value,
                    Minimum = p.Minimum,
                    Maximum = p.Maximum,
                    Description = p.Description,
                    UnitType = p.UnitType,
                    ValueType = p.ValueType
                });
            }
            var created = await _recipeRepo.AddAsync(newRecipe);

            var dto = new RecipeDto().ToDto(created);
            RecipeList.Add(dto);
        }

        private string CreateCopyName(string originalName)
        {
            return $"{originalName}_Copy";
        }

        private async Task DisableCurrentActiveRecipeAsync(RecipeDto newRecipe)
        {
            var activeRecipe = RecipeList.FirstOrDefault(r => r.IsActive && r.Id != newRecipe.Id);

            if (activeRecipe != null)
            {
                activeRecipe.IsActive = false; // 메모리 DTO 반영
                await _recipeRepo.Update(activeRecipe.ToEntity()); // DB 반영
            }
        }

        public async Task AddRecipeParam(RecipeParamDto paramDto)
        {
            var param = await _parameterRepo.AddAsync(paramDto.ToEntity());
            var recipe = RecipeList.FirstOrDefault(r => r.Id == param.RecipeId);
            recipe?.ParamList.Add(new RecipeParamDto().ToDto(param));
        }

        public async Task UpdateRecipeParam(RecipeParamDto paramDto)
        {
            await _parameterRepo.Update(paramDto.ToEntity());
        }

        public async Task DeleteRecipeParam(RecipeParamDto paramDto)
        {
            await _parameterRepo.Remove(paramDto.Id);
            var recipe = RecipeList.FirstOrDefault(r => r.Id == paramDto.RecipeId);
            var targetParam = recipe?.ParamList.FirstOrDefault(p => p.Id == paramDto.Id);
            if (targetParam != null)
            {
                recipe.ParamList.Remove(targetParam);
            }
        }

    }
}
