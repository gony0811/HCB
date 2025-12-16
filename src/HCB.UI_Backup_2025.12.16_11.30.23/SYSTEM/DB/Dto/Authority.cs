using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity;

namespace HCB.UI
{
    public partial class Authority : ObservableObject
    {
        [ObservableProperty] private int id;
        [ObservableProperty] private string name;
        [ObservableProperty] private string description;

        public Authority()
        {

        }
        private Authority(int id, string name , string description)
        {
            Id = id;
            Name = name;
            Description = description;
        }

        public static Authority of(Role role)
        {
            return new Authority(id: role.Id, name: role.Name, description: role.Description);
        }
    }
}
