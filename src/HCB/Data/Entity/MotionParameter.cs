using System.ComponentModel.DataAnnotations;
using System.Windows.Navigation;
using HCB.Data.Entity.Type;
using HCB.Data.Interface;
using ValueType = HCB.Data.Entity.Type.ValueType;

namespace HCB.Data.Entity
{
    public sealed class MotionParameter : IEntity
    {

        [Required]
        public string Name { get; set; } = "";

        [Required]
        public ValueType ValueType { get; set; }

        public int? IntValue { get; set; }
        public double? DoubleValue { get; set; }
        public bool? BoolValue { get; set; }
        public string? StringValue { get; set; }

        public UnitType UnitType { get; set; }

        public MotionEntity? Motion { get; set; }
        public int MotionId { get; set; }


        public void AddValue(object? value)
        {
            if (value == null)
                return;

            try
            {
                switch (ValueType)
                {
                    case ValueType.Integer:
                        IntValue = Convert.ToInt32(value);
                        break;

                    case ValueType.Double:
                    case ValueType.Float:
                        DoubleValue = Convert.ToDouble(value);
                        break;

                    case ValueType.Boolean:
                        BoolValue = Convert.ToBoolean(value);
                        break;

                    case ValueType.String:
                        StringValue = value?.ToString() ?? "";
                        break;
                }
            }
            catch
            {
                // 잘못된 타입일 경우 예외 대신 값을 초기화
                IntValue = null;
                DoubleValue = null;
                BoolValue = null;
                StringValue = null;
            }
        }


        public string Value()
        {
            return ValueType switch
            {
                ValueType.Integer => IntValue?.ToString() ?? "",
                ValueType.Double => DoubleValue?.ToString() ?? "",
                ValueType.Float => DoubleValue?.ToString() ?? "",
                ValueType.Boolean => BoolValue?.ToString() ?? "",
                ValueType.String => StringValue ?? "",
                _ => ""
            };
        }

    }
}
