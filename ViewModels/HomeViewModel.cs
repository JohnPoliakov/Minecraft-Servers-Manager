using Minecraft_Server_Manager.Models;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Minecraft_Server_Manager.ViewModels
{
    public class HomeViewModel
    {
        #region Collections & Data
        /// <summary>
        /// Liste des serveurs affichés sur la page d'accueil.
        /// </summary>
        public ObservableCollection<ServerProfile> Servers { get; }
        #endregion

        #region Commands
        /// <summary>
        /// Commande déclenchée lorsqu'on clique sur un serveur pour le gérer.
        /// </summary>
        public ICommand SelectServerCommand { get; }
        #endregion

        #region Constructor
        public HomeViewModel(ObservableCollection<ServerProfile> servers, Action<ServerProfile> onServerSelected)
        {
            // Initialisation des données
            Servers = servers;

            // Initialisation des commandes
            // On exécute l'action 'onServerSelected' passée par le MainViewModel
            SelectServerCommand = new RelayCommand(param => onServerSelected?.Invoke((ServerProfile)param));
        }
        #endregion
    }
}