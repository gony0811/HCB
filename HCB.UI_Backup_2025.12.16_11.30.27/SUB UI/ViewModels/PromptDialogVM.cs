using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace HCB.UI
{
    public interface IDialogRequestClose<T>
    {
        event EventHandler<DialogCloseRequestedEventArgs<T>> CloseRequested;
    }

    public struct DialogResult<T>
    {
        public DialogResult(bool? dialogResult, T value)
        {
            DialogResultValue = dialogResult;
            Value = value;
        }
        public bool? DialogResultValue { get; }
        public T Value { get; }
    }

    public sealed class DialogCloseRequestedEventArgs<T> : EventArgs
    {
        public DialogCloseRequestedEventArgs(DialogResult<T> result) { Result = result; }
        public DialogResult<T> Result { get; }
    }

    // C# 7.3 호환: nullable 참조 제거 (T는 default(T)로 '없음' 표현)
    public abstract partial class PromptDialogVM<T> : ObservableObject, IDialogRequestClose<T>
    {
        public event EventHandler<DialogCloseRequestedEventArgs<T>> CloseRequested;

        // ===== Properties =====
        private string _title = string.Empty;
        public string Title
        {
            get { return _title; }
            set { SetProperty(ref _title, value); }
        }

        private T _value = default(T);
        public T Value
        {
            get { return _value; }
            set { SetProperty(ref _value, value); }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get { return _isBusy; }
            set
            {
                if (SetProperty(ref _isBusy, value))
                    InvalidateButtons();
            }
        }

        public ObservableCollection<string> Errors { get; } = new ObservableCollection<string>();

        protected PromptDialogVM(T initial = default(T), string title = null)
        {
            Value = initial;
            Title = title ?? string.Empty;
        }

        // ===== Commands =====
        [RelayCommand]
        private async Task OkAsync(CancellationToken ct)
        {
            Errors.Clear();
            try
            {
                IsBusy = true;

                var errs = await ValidateForOkAsync(ct).ConfigureAwait(false);
                if (errs != null && errs.Count > 0)
                {
                    foreach (var e in errs) Errors.Add(e);
                    OnValidationFailed(errs);
                    return;
                }

                var result = new DialogResult<T>(true, GetResult());
                var handler = CloseRequested;
                if (handler != null) handler(this, new DialogCloseRequestedEventArgs<T>(result));
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            var handler = CloseRequested;
            if (handler != null)
                handler(this, new DialogCloseRequestedEventArgs<T>(new DialogResult<T>(false, default(T))));
        }

        protected virtual bool CanOk() => !IsBusy;
        protected virtual bool CanCancel() => !IsBusy;

        // 결과 변환 훅
        protected virtual T GetResult() => Value;

        // ===== Validation Pipeline =====
        protected virtual async Task<IReadOnlyList<string>> ValidateForOkAsync(CancellationToken ct)
        {
            var list = new List<string>();

            if (EqualityComparer<T>.Default.Equals(Value, default(T)))
            {
                list.Add("값이 비어 있습니다.");
                return list;
            }

            // (1) DataAnnotations (Value에 어노테이션이 붙어있는 경우)
            var ctx = new ValidationContext(Value, serviceProvider: null, items: null);
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(Value, ctx, results, validateAllProperties: true);
            list.AddRange(results.Select(r => r.ErrorMessage ?? r.ToString()));

            // (2) 파생 클래스의 추가/비동기 규칙
            var extra = await ValidateCoreAsync(Value, ct).ConfigureAwait(false);
            if (extra != null && extra.Count > 0) list.AddRange(extra);

            return list;
        }

        // 커스텀/비동기 검증 훅(파생에서 필요 시 override)
        protected virtual Task<IReadOnlyList<string>> ValidateCoreAsync(T value, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<string>)Array.Empty<string>());

        // 기본 에러 표시(파생에서 UI로 대체 가능)
        protected virtual void OnValidationFailed(IReadOnlyList<string> errors)
        {
            MessageBox.Show(string.Join(Environment.NewLine, errors), "입력 오류",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        // 버튼 활성화 갱신
        protected void InvalidateButtons()
        {
            var ok = OkCommand as IRelayCommand;
            if (ok != null) ok.NotifyCanExecuteChanged();

            var cancel = CancelCommand as IRelayCommand;
            if (cancel != null) cancel.NotifyCanExecuteChanged();
        }
    }
}
