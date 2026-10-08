using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity.Type;
using System;
using System.ComponentModel;
using System.Globalization;
using ValueType = HCB.Data.Entity.Type.ValueType;

namespace HCB.UI
{
    /// <summary>
    /// 파라미터 편집 화면의 한 행. 원본 DTO(RecipeParamDto / ECParamDto)를 감싸고,
    /// 사용자가 입력한 값(EditValue)은 저장 전까지 DTO에 반영하지 않는다.
    /// </summary>
    public partial class ParamRow : ObservableObject
    {
        private readonly INotifyPropertyChanged _source;

        public object Source => _source;
        public string Name { get; }
        public string Category { get; }
        public int CategoryOrder { get; }
        public int SortIndex { get; }
        public bool IsRequired { get; }
        public string Description { get; }
        public UnitType UnitType { get; }
        public ValueType ValueType { get; }
        public string Minimum { get; }
        public string Maximum { get; }

        public string UnitText => UnitType == UnitType.None ? "" : UnitType.ToString();

        public string RangeText
        {
            get
            {
                bool hasMin = !string.IsNullOrWhiteSpace(Minimum);
                bool hasMax = !string.IsNullOrWhiteSpace(Maximum);
                if (!hasMin && !hasMax) return "";
                return $"{(hasMin ? Minimum : "")} ~ {(hasMax ? Maximum : "")}";
            }
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsDirty))]
        private string originalValue;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsDirty))]
        private string editValue;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasError))]
        private string error = "";

        public bool IsDirty => !string.Equals(EditValue ?? "", OriginalValue ?? "", StringComparison.Ordinal);
        public bool HasError => !string.IsNullOrEmpty(Error);

        private ParamRow(INotifyPropertyChanged source, ParamCatalog catalog, ComponentType? component, string name, string value,
                         string min, string max, UnitType unit, ValueType valueType, string description)
        {
            _source = source;
            Name = name ?? "";
            var def = catalog.Find(Name);
            Category = def?.Category ?? ParamCatalog.EtcCategory;
            CategoryOrder = catalog.CategoryOrder(Category);
            SortIndex = catalog.IndexOf(Name);
            IsRequired = def?.IsRequiredFor(component) ?? false;
            Description = string.IsNullOrWhiteSpace(description) ? def?.Description ?? "" : description;
            UnitType = unit;
            ValueType = valueType;
            Minimum = min;
            Maximum = max;
            originalValue = value ?? "";
            editValue = value ?? "";
            error = Validate(editValue);

            _source.PropertyChanged += OnSourcePropertyChanged;
        }

        /// <param name="component">레시피 종류 (필수 표시 기준). 종류 구분 없는 EC는 null.</param>
        public static ParamRow From(RecipeParamDto dto, ParamCatalog catalog, ComponentType? component = null)
            => new ParamRow(dto, catalog, component, dto.Name, dto.Value, dto.Minimum, dto.Maximum, dto.UnitType, dto.ValueType, dto.Description);

        public static ParamRow From(ECParamDto dto, ParamCatalog catalog)
            => new ParamRow(dto, catalog, null, dto.Name, dto.Value, dto.Minimum, dto.Maximum, dto.UnitType, dto.ValueType, dto.Description);

        /// <summary>다른 화면(캘리브레이션 등)에서 DTO 값이 바뀌면 원래값을 갱신. 편집 중이 아니면 입력값도 따라간다.</summary>
        private void OnSourcePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != "Value") return;
            string newValue = (sender as RecipeParamDto)?.Value ?? (sender as ECParamDto)?.Value ?? "";
            bool wasDirty = IsDirty;
            OriginalValue = newValue;
            if (!wasDirty) EditValue = newValue;
        }

        partial void OnEditValueChanged(string value) => Error = Validate(value);

        private string Validate(string value) => ValidateValue(value, ValueType, Minimum, Maximum);

        /// <summary>값 형식/범위 검사. 문제 없으면 빈 문자열.</summary>
        public static string ValidateValue(string value, ValueType valueType, string minimum, string maximum)
        {
            string v = value?.Trim() ?? "";
            if (v.Length == 0) return "값을 입력하세요";

            switch (valueType)
            {
                case ValueType.Integer:
                    if (!int.TryParse(v, NumberStyles.Integer, CultureInfo.CurrentCulture, out _)) return "정수가 아닙니다";
                    break;
                case ValueType.Double:
                case ValueType.Float:
                    if (!double.TryParse(v, NumberStyles.Float, CultureInfo.CurrentCulture, out _)) return "숫자가 아닙니다";
                    break;
                case ValueType.Boolean:
                    if (!bool.TryParse(v, out _)) return "True / False";
                    return "";
                default:
                    return "";
            }

            double d = double.Parse(v, NumberStyles.Float, CultureInfo.CurrentCulture);
            if (double.TryParse(minimum, NumberStyles.Float, CultureInfo.CurrentCulture, out var min) && d < min)
                return $"최소 {minimum}";
            if (double.TryParse(maximum, NumberStyles.Float, CultureInfo.CurrentCulture, out var max) && d > max)
                return $"최대 {maximum}";
            return "";
        }

        /// <summary>입력값을 원본 DTO에 반영 (DB 저장은 호출측 서비스가 수행)</summary>
        public void ApplyToSource()
        {
            string v = EditValue?.Trim() ?? "";
            if (_source is RecipeParamDto r) r.Value = v;
            else if (_source is ECParamDto e) e.Value = v;
            OriginalValue = v;
            EditValue = v;
        }

        public void Revert() => EditValue = OriginalValue;

        public void Detach() => _source.PropertyChanged -= OnSourcePropertyChanged;
    }
}
