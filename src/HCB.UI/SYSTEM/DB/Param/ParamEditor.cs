using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.Data.Entity.Type;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;

namespace HCB.UI
{
    public partial class ParamCategoryItem : ObservableObject
    {
        public string Name { get; init; } = "";
        public bool IsAll { get; init; }
        [ObservableProperty] private int count;
        [ObservableProperty] private int dirtyCount;
    }

    /// <summary>
    /// 파라미터 목록 편집 상태 (구역 분류, 검색, 인라인 수정, 변경 추적).
    /// USub02(Recipe), USub09(EC Param)에서 공용으로 사용한다.
    /// </summary>
    public partial class ParamEditor : ObservableObject
    {
        private readonly ParamCatalog _catalog;
        private readonly ParamCategoryItem _allCategory = new() { Name = "전체", IsAll = true };

        public ObservableCollection<ParamRow> Rows { get; } = new();
        public ICollectionView View { get; }
        public ObservableCollection<ParamCategoryItem> Categories { get; } = new();

        [ObservableProperty] private ParamCategoryItem selectedCategory;
        [ObservableProperty] private string searchText = string.Empty;
        [ObservableProperty] private bool showDirtyOnly;
        [ObservableProperty] private ParamRow selectedRow;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasChanges))]
        private int dirtyCount;

        [ObservableProperty] private int errorCount;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasMissing))]
        private string missingText = "";

        public bool HasChanges => DirtyCount > 0;
        public bool HasMissing => !string.IsNullOrEmpty(MissingText);

        /// <summary>변경 건수가 바뀔 때 (VM 쪽 저장 버튼 활성화 갱신용)</summary>
        public event EventHandler ChangesUpdated;

        public ParamEditor(ParamCatalog catalog)
        {
            _catalog = catalog;
            View = CollectionViewSource.GetDefaultView(Rows);
            View.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ParamRow.Category)));
            View.SortDescriptions.Add(new SortDescription(nameof(ParamRow.CategoryOrder), ListSortDirection.Ascending));
            View.SortDescriptions.Add(new SortDescription(nameof(ParamRow.SortIndex), ListSortDirection.Ascending));
            View.SortDescriptions.Add(new SortDescription(nameof(ParamRow.Name), ListSortDirection.Ascending));
            View.Filter = Filter;
            selectedCategory = _allCategory;
        }

        /// <summary>
        /// 행 목록을 다시 구성한다. 같은 DTO에 대해 저장 전 입력값이 있으면 유지한다.
        /// </summary>
        /// <summary>현재 표시 중인 레시피 종류 (필수 항목/누락 경고 기준). EC는 null.</summary>
        public ComponentType? Component { get; private set; }

        public void Load(IEnumerable<ParamRow> rows, ComponentType? component = null)
        {
            Component = component;
            var pending = Rows.Where(r => r.IsDirty).ToDictionary(r => r.Source, r => r.EditValue);
            object selectedSource = SelectedRow?.Source;

            foreach (var r in Rows)
            {
                r.PropertyChanged -= OnRowPropertyChanged;
                r.Detach();
            }
            Rows.Clear();

            foreach (var r in rows)
            {
                if (pending.TryGetValue(r.Source, out var edit)) r.EditValue = edit;
                r.PropertyChanged += OnRowPropertyChanged;
                Rows.Add(r);
            }

            RebuildCategories();
            UpdateCounts();
            SelectedRow = Rows.FirstOrDefault(r => r.Source == selectedSource);
            View.Refresh();
        }

        public void Clear() => Load(Enumerable.Empty<ParamRow>());

        public IReadOnlyList<ParamRow> DirtyRows => Rows.Where(r => r.IsDirty).ToList();

        /// <summary>새 파라미터 기본값 (선택 중인 구역의 카탈로그 항목 중 아직 없는 첫 이름을 제안)</summary>
        public ParameterCreateDto NewParamTemplate()
        {
            var names = new HashSet<string>(Rows.Select(r => r.Name));
            bool InScope(ParamDef d) => !names.Contains(d.Name)
                          && (SelectedCategory == null || SelectedCategory.IsAll || d.Category == SelectedCategory.Name);
            // 이 종류의 빠진 필수 항목을 먼저, 없으면 카탈로그의 다른 항목을 제안
            var suggest = _catalog.RequiredFor(Component).FirstOrDefault(InScope)
                          ?? _catalog.Defs.FirstOrDefault(InScope);
            return new ParameterCreateDto
            {
                Name = suggest?.Name ?? "",
                Value = suggest?.Default ?? "",
                ValueType = suggest?.Type ?? HCB.Data.Entity.Type.ValueType.Double,
                UnitType = suggest?.Unit ?? UnitType.None,
                Description = suggest?.Description ?? "",
            };
        }

        /// <summary>생성/상세 수정 입력 검사. 문제 없으면 null.</summary>
        public string ValidateParam(ParameterCreateDto p, object editingSource = null)
        {
            string name = p.Name?.Trim() ?? "";
            if (name.Length == 0) return "Name을 입력하세요";
            if (name.Length > 100) return "Name은 100자 이하로 입력하세요";
            if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^[A-Z][A-Z0-9]*(_[A-Z0-9]+)*$"))
                return $"Name은 대문자 스네이크 케이스로 입력하세요 (예: TOP_DIE_THICKNESS)\n입력값: {name}";
            if (Rows.Any(r => r.Source != editingSource && string.Equals(r.Name, name, StringComparison.Ordinal)))
                return $"'{name}' 파라미터가 이미 있습니다";
            if (!string.IsNullOrWhiteSpace(p.Minimum) && !string.IsNullOrWhiteSpace(p.Maximum)
                && double.TryParse(p.Minimum, out var min) && double.TryParse(p.Maximum, out var max) && min > max)
                return "Minimum이 Maximum보다 큽니다";
            string err = ParamRow.ValidateValue(p.Value, p.ValueType, p.Minimum, p.Maximum);
            return string.IsNullOrEmpty(err) ? null : $"Value: {err}";
        }

        /// <summary>이름으로 행을 선택하고 해당 구역을 보이게 한다 (생성 직후 사용)</summary>
        public void SelectByName(string name)
        {
            var row = Rows.FirstOrDefault(r => r.Name == name);
            if (row == null) return;
            if (SelectedCategory != null && !SelectedCategory.IsAll && SelectedCategory.Name != row.Category)
                SelectedCategory = _allCategory;
            if (!string.IsNullOrEmpty(SearchText) && !row.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) SearchText = "";
            SelectedRow = row;
        }

        /// <summary>저장 확인창용 변경 요약 (최대 15줄)</summary>
        public static string BuildSummary(IReadOnlyList<ParamRow> rows)
        {
            const int max = 15;
            var lines = rows.Take(max).Select(r => $"{r.Name}: {r.OriginalValue} → {r.EditValue?.Trim()}").ToList();
            if (rows.Count > max) lines.Add($"… 외 {rows.Count - max}건");
            return string.Join("\n", lines);
        }

        [RelayCommand]
        private void RevertAll()
        {
            foreach (var r in Rows.Where(r => r.IsDirty)) r.Revert();
        }

        [RelayCommand]
        private void RevertRow(ParamRow row) => row?.Revert();

        partial void OnSelectedCategoryChanged(ParamCategoryItem value) => View.Refresh();
        partial void OnSearchTextChanged(string value) => View.Refresh();
        partial void OnShowDirtyOnlyChanged(bool value) => View.Refresh();

        private bool Filter(object obj)
        {
            if (obj is not ParamRow row) return false;
            if (ShowDirtyOnly && !row.IsDirty) return false;
            if (SelectedCategory != null && !SelectedCategory.IsAll && row.Category != SelectedCategory.Name) return false;
            if (string.IsNullOrWhiteSpace(SearchText)) return true;
            return row.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || row.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
        }

        private void OnRowPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ParamRow.IsDirty) || e.PropertyName == nameof(ParamRow.HasError))
            {
                UpdateCounts();
                if (ShowDirtyOnly && e.PropertyName == nameof(ParamRow.IsDirty) && sender is ParamRow r && !r.IsDirty)
                    View.Refresh();
            }
        }

        private void RebuildCategories()
        {
            string selectedName = SelectedCategory?.Name;
            Categories.Clear();
            Categories.Add(_allCategory);

            // 카탈로그 순서대로, 행이 있거나 이 종류의 필수 항목이 있는 구역만 표시
            foreach (var cat in _catalog.Categories)
            {
                bool hasRows = Rows.Any(r => r.Category == cat);
                bool hasDefs = _catalog.RequiredFor(Component).Any(d => d.Category == cat);
                if (hasRows || hasDefs)
                    Categories.Add(new ParamCategoryItem { Name = cat });
            }

            SelectedCategory = Categories.FirstOrDefault(c => c.Name == selectedName) ?? _allCategory;

            // 누락된 필수 파라미터
            var names = new HashSet<string>(Rows.Select(r => r.Name));
            var missing = _catalog.RequiredFor(Component).Where(d => !names.Contains(d.Name)).Select(d => d.Name).ToList();
            string kind = Component.HasValue ? $"[{Component}] " : "";
            MissingText = missing.Count == 0 ? "" : $"{kind}필수 파라미터 누락 {missing.Count}건: {string.Join(", ", missing)}";
        }

        private void UpdateCounts()
        {
            DirtyCount = Rows.Count(r => r.IsDirty);
            ErrorCount = Rows.Count(r => r.IsDirty && r.HasError);
            _allCategory.Count = Rows.Count;
            _allCategory.DirtyCount = DirtyCount;
            foreach (var c in Categories.Where(c => !c.IsAll))
            {
                c.Count = Rows.Count(r => r.Category == c.Name);
                c.DirtyCount = Rows.Count(r => r.Category == c.Name && r.IsDirty);
            }
            ChangesUpdated?.Invoke(this, EventArgs.Empty);
        }
    }
}
