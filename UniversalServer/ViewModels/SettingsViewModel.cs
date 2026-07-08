using System.Windows.Input;
using UniversalServer.Model;
using UniversalServer.ViewModelBase;

namespace UniversalServer.ViewModels
{
    public class SettingsViewModel : ViewModel
    {
        private string _messageFromFileAccess;
        private string _dbIPAddress;
        private ICommand _saveSettingsCommand;
        private readonly ISettingsRepository _settingsRepository;

        public SettingsViewModel() : this(new FileAccess()) { }

        public SettingsViewModel(ISettingsRepository settingsRepository)
        {
            _settingsRepository = settingsRepository;
            _settingsRepository.DataSaved += OnDataSaved;
        }

        public string MessageFromFileAccess
        {
            get => _messageFromFileAccess;
            set
            {
                _messageFromFileAccess = value;
                OnPropertyChanged("MessageFromFileAccess");
            }
        }

        public string DBIPAddress
        {
            get => _dbIPAddress;
            set
            {
                _dbIPAddress = value;
                OnPropertyChanged("DBIPAddress");
            }
        }

        public ICommand SaveSettingsCommand
        {
            get
            {
                if (_saveSettingsCommand == null)
                    _saveSettingsCommand = new RelayCommand(c => ExecuteSaveSettingsCommand());
                return _saveSettingsCommand;
            }
        }

        private void ExecuteSaveSettingsCommand()
        {
            _settingsRepository.SaveSettings(DBIPAddress);
        }

        private void OnDataSaved(string msg)
        {
            MessageFromFileAccess = msg;
        }
    }
}
