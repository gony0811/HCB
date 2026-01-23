using System;

namespace HCB.UI
{
    /// <summary>
    /// 값이 변경되었을 때 이전 값과 새 값을 전달하는 이벤트 인수를 나타냅니다.
    /// </summary>
    /// <typeparam name="T">값의 형식입니다.</typeparam>
    public class ValueChangedEventArgs<T> : EventArgs
    {
        /// <summary>
        /// 이전 값입니다.
        /// </summary>
        public T? OldValue { get; }

        /// <summary>
        /// 새로운 값입니다.
        /// </summary>
        public T NewValue { get; }

        /// <summary>
        /// ValueChangedEventArgs 클래스의 새 인스턴스를 초기화합니다.
        /// </summary>
        /// <param name="oldValue">이전 값입니다.</param>
        /// <param name="newValue">새로운 값입니다.</param>
        public ValueChangedEventArgs(T? oldValue, T newValue)
        {
            OldValue = oldValue;
            NewValue = newValue;
        }
    }
}