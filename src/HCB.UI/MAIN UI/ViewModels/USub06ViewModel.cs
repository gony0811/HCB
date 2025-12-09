using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.IoC;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;

namespace HCB.UI
{
    [ViewModel(Lifetime.Singleton)]
    public partial class USub06ViewModel : ObservableObject
    {

        public USub06ViewModel()
        {
           

        }


        #region Position CRUD
        //[RelayCommand]
        //public async void PositionCreate()
        //{
        //    if (SelectedMotion == null)
        //        return;

        //    var newPosition = new MotionPositionCreate();

        //    var dlg = new ObjectInspectorWindow(newPosition)
        //    {
        //        Owner = Application.Current.MainWindow
        //    };

        //    if (dlg.ShowDialog() == true)
        //    {
        //        var position = (MotionPositionCreate) dlg.ResultObject;
        //        var createPosition = new DMotionPosition
        //        {
        //            Name = position.Name,
        //            Location = position.Location,
        //            MaximumLocation = position.MaximumLocation,
        //            MinimumLocation = position.MinimumLocation,
        //            Speed = position.Speed,
        //            MaximumSpeed = position.MaximumSpeed,
        //            MinimumSpeed = position.MinimumSpeed,
        //            ParentMotionId = SelectedMotion.Id
        //        };

        //        var result = await _powerPmacManager.CreatePosition(createPosition);

        //        if (result != null)
        //        {
        //            SelectedMotion.PositionList.Add(result);
        //            MessageBox.Show("저장완료");
        //        }else
        //        {
        //            MessageBox.Show("저장실패");
        //        }

        //    }
        //    else
        //    {
        //        MessageBox.Show("저장취소");
        //    }
        //}

        //[RelayCommand]
        //public async void PositionUpdate(DMotionPosition pos)
        //{
        //    if (pos is null)
        //    {
        //        MessageBox.Show("편집할 위치를 선택하세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
        //        return;
        //    }

        //    SelectedPosition = pos;

        //    // 기존 Position을 복사해서 모달창에 전달 (원본 보호)
        //    var editCopy = new MotionPositionCreate
        //    {
        //        Name = SelectedPosition.Name,
        //        Location = SelectedPosition.Location,
        //        MaximumLocation = SelectedPosition.MaximumLocation,
        //        MinimumLocation = SelectedPosition.MinimumLocation,
        //        Speed = SelectedPosition.Speed,
        //        MaximumSpeed = SelectedPosition.MaximumSpeed,
        //        MinimumSpeed = SelectedPosition.MinimumSpeed
        //    };

        //    var dlg = new ObjectInspectorWindow(editCopy)
        //    {
        //        Owner = Application.Current.MainWindow
        //    };

        //    if (dlg.ShowDialog() == true)
        //    {
        //        var edited = (MotionPositionCreate)dlg.ResultObject;

        //        // DB 업데이트용 객체 생성
        //        var updated = new DMotionPosition
        //        {
        //            Id = SelectedPosition.Id,
        //            Name = edited.Name,
        //            Location = edited.Location,
        //            MaximumLocation = edited.MaximumLocation,
        //            MinimumLocation = edited.MinimumLocation,
        //            Speed = edited.Speed,
        //            MaximumSpeed = edited.MaximumSpeed,
        //            MinimumSpeed = edited.MinimumSpeed,
        //            ParentMotionId = SelectedMotion.Id
        //        };

        //        var result = await _powerPmacManager.UpdatePosition(updated);

        //        if (result != null)
        //        {
        //            // 리스트 내 기존 항목 갱신
        //            var index = SelectedMotion.PositionList.IndexOf(SelectedPosition);
        //            if (index >= 0)
        //                SelectedMotion.PositionList[index] = result;

        //            SelectedPosition = result;
        //            MessageBox.Show("수정 완료", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
        //        }
        //        else
        //        {
        //            MessageBox.Show("수정 실패", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        //        }
        //    }
        //    else
        //    {
        //        MessageBox.Show("수정 취소");
        //    }
        //}

        //[RelayCommand]
        //public async void PositionDelete(DMotionPosition pos)
        //{
        //    if (pos is null)
        //    {
        //        MessageBox.Show("삭제할 위치를 선택하세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
        //        return;
        //    }
        //    SelectedPosition = pos;

        //    var confirm = MessageBox.Show(
        //        $"정말로 '{SelectedPosition.Name}' 위치를 삭제하시겠습니까?",
        //        "삭제 확인",
        //        MessageBoxButton.YesNo,
        //        MessageBoxImage.Question);

        //    if (confirm != MessageBoxResult.Yes)
        //        return;

        //    var success = await _powerPmacManager.DeletePosition(SelectedPosition);

        //    if (success)
        //    {
        //        SelectedMotion.PositionList.Remove(SelectedPosition);
        //        MessageBox.Show("삭제 완료", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
        //    }
        //    else
        //    {
        //        MessageBox.Show("삭제 실패", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        //    }
        //}
        #endregion
    }
}
