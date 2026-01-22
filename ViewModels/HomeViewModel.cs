using Minecraft_Server_Manager.Models;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Minecraft_Server_Manager.ViewModels
{
    public class HomeViewModel
    {
        public ObservableCollection<ServerProfile> Servers { get; }

        public ICommand SelectServerCommand { get; }

        public HomeViewModel(ObservableCollection<ServerProfile> servers, Action<ServerProfile> onServerSelected)
        {
            Servers = servers;
            SelectServerCommand = new RelayCommand(param => onServerSelected?.Invoke((ServerProfile)param));
        }
    }
}