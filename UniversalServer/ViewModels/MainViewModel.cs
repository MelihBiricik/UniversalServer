using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Input;
using UniversalServer.Model;
using UniversalServer.ViewModelBase;

namespace UniversalServer.ViewModels
{
    class MainViewModel : ViewModel
    {
        #region Fields
        private readonly ISensorRepository _sensorRepository;
        private IServerContract _serv;
        private volatile bool _isRunning = false;
        private bool _isDbConnected = false;

        private List<IPAddress> _avIPAdresses;
        private int _portToListen;
        private IPAddress _selectedIPAdress;
        private List<Raum> _availableRooms;
        private Raum _selectedRaum;
        private string _status;

        private readonly SensorReadingTracker<TempValue>     _tempTracker     = new SensorReadingTracker<TempValue>();
        private readonly SensorReadingTracker<HumidValue>    _humidTracker    = new SensorReadingTracker<HumidValue>();
        private readonly SensorReadingTracker<PressureValue> _pressureTracker = new SensorReadingTracker<PressureValue>();

        private ICommand _windowLoadedCommand;
        private ICommand _startListeningCommand;
        private ICommand _stopListeningCommand;
        #endregion

        #region Constructors
        public MainViewModel() : this(new DBAccess()) { }

        public MainViewModel(ISensorRepository sensorRepository)
        {
            _sensorRepository = sensorRepository;
            _portToListen = 11000;
        }
        #endregion

        #region Network Properties
        public int PortToListen
        {
            get => _portToListen;
            set { _portToListen = value; OnPropertyChanged("PortToListen"); }
        }

        public ObservableCollection<IPAddress> AvailableIPAdresses
        {
            get => _avIPAdresses != null
                ? new ObservableCollection<IPAddress>(_avIPAdresses)
                : new ObservableCollection<IPAddress>();
            set { _avIPAdresses = value.ToList(); OnPropertyChanged("AvailableIPAdresses"); }
        }

        public IPAddress SelectedIPAdress
        {
            get => _selectedIPAdress;
            set { _selectedIPAdress = value; OnPropertyChanged("SelectedIPAdress"); }
        }
        #endregion

        #region Room Properties
        public ObservableCollection<Raum> AvailableRooms
        {
            get => _availableRooms != null
                ? new ObservableCollection<Raum>(_availableRooms)
                : new ObservableCollection<Raum>();
            set { _availableRooms = value.ToList(); OnPropertyChanged("AvailableRooms"); }
        }

        public Raum SelectedRaum
        {
            get => _selectedRaum;
            set
            {
                _selectedRaum = value;
                OnPropertyChanged("SelectedRaum");
                if (_selectedRaum != null)
                    LoadRoomData(_selectedRaum.RaumID);
            }
        }
        #endregion

        #region Sensor Value Properties
        public TempValue TempMaxValue     => _tempTracker.Max;
        public TempValue TempAktuellValue => _tempTracker.Current;
        public TempValue TempMinValue     => _tempTracker.Min;

        public HumidValue FeuchteMaxValue     => _humidTracker.Max;
        public HumidValue FeuchteAktuellValue => _humidTracker.Current;
        public HumidValue FeuchteMinValue     => _humidTracker.Min;

        public PressureValue PressMaxVal    => _pressureTracker.Max;
        public PressureValue PressCurrentVal => _pressureTracker.Current;
        public PressureValue PressMinVal    => _pressureTracker.Min;

        public string ShortValuesString =>
            _tempTracker.Current?.Value + ", " +
            _humidTracker.Current?.Value + ", " +
            _pressureTracker.Current?.Value;

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged("Status"); }
        }
        #endregion

        #region Commands
        public ICommand WindowLoaded
        {
            get
            {
                if (_windowLoadedCommand == null)
                    _windowLoadedCommand = new RelayCommand(c => ExecuteWindowLoadedCommand());
                return _windowLoadedCommand;
            }
        }

        // XAML bindet per Behavior an diesen Namen
        public ICommand LoadedCommand => WindowLoaded;

        public ICommand StartListeningCommand
        {
            get
            {
                if (_startListeningCommand == null)
                    _startListeningCommand = new RelayCommand(c => StartListening(), c => !_isRunning);
                return _startListeningCommand;
            }
        }

        public ICommand StopListeningCommand
        {
            get
            {
                if (_stopListeningCommand == null)
                    _stopListeningCommand = new RelayCommand(c => StopListening(), c => _isRunning);
                return _stopListeningCommand;
            }
        }
        #endregion

        #region Initialization
        private void ExecuteWindowLoadedCommand()
        {
            try
            {
                IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
                var ipList = new ObservableCollection<IPAddress>(ipHostInfo.AddressList);
                ipList.Add(IPAddress.Loopback);
                AvailableIPAdresses = ipList;
                SelectedIPAdress = ipList.First(adr => adr == IPAddress.Loopback);
            }
            catch (Exception ex)
            {
                Status = ex.Message;
            }

            try
            {
                var rooms = _sensorRepository.GetRooms();
                AvailableRooms = new ObservableCollection<Raum>(rooms);
                _isDbConnected = true;
            }
            catch (Exception ex)
            {
                Status = ex.Message;
                _isDbConnected = false;
                AvailableRooms = new ObservableCollection<Raum>(GetMockRooms());
            }
        }
        #endregion

        #region Server Control
        private void StartListening()
        {
            try
            {
                if (_serv == null)
                {
                    _serv = new ServerMockUp();
                    _serv.StatusPropertyChanged += Serv_StatusPropertyChanged;
                    _serv.MessageReceived += OnMessageReceived;
                }

                _serv.Start(SelectedIPAdress, PortToListen);
                _isRunning = true;
                Status = "Listening...";
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                Status = "Error while starting: " + ex.Message;
            }
        }

        private void StopListening()
        {
            try
            {
                if (_serv != null)
                {
                    _isRunning = false;
                    _serv.Stop();
                    Status = "Paused listening.";
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
                else
                {
                    Status = "Server is not running.";
                }
            }
            catch (Exception ex)
            {
                Status = "Error while stopping: " + ex.Message;
            }
        }

        private void Serv_StatusPropertyChanged(string s) => Status = s;
        #endregion

        #region Message Handling
        private void OnMessageReceived(string raw)
        {
            Task.Run(() =>
            {
                try
                {
                    SensorMessage msg = SensorMessageParser.Parse(raw);

                    try
                    {
                        _sensorRepository.InsertData(
                            msg.ToTempValue(), msg.ToHumidValue(), msg.ToPressValue(),
                            msg.ReceivedAt);
                    }
                    catch (Exception dbEx)
                    {
                        DispatchStatus(dbEx.Message);
                    }

                    System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (!_isRunning) return;

                        Status = DateTime.Now.ToShortTimeString() + ": " + raw;
                        ApplySensorUpdate(msg);
                    }));
                }
                catch (Exception ex)
                {
                    DispatchStatus("Fehler beim Interpretieren der Werte. " + ex.Message + Environment.NewLine + raw);
                }
            });
        }

        private void ApplySensorUpdate(SensorMessage msg)
        {
            _tempTracker.Update(msg.ToTempValue());
            OnPropertyChanged("TempAktuellValue");
            OnPropertyChanged("TempMaxValue");
            OnPropertyChanged("TempMinValue");

            _humidTracker.Update(msg.ToHumidValue());
            OnPropertyChanged("FeuchteAktuellValue");
            OnPropertyChanged("FeuchteMaxValue");
            OnPropertyChanged("FeuchteMinValue");

            _pressureTracker.Update(msg.ToPressValue());
            OnPropertyChanged("PressCurrentVal");
            OnPropertyChanged("PressMaxVal");
            OnPropertyChanged("PressMinVal");

            OnPropertyChanged("ShortValuesString");
        }

        private void DispatchStatus(string message) =>
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => Status = message));
        #endregion

        #region Room Data
        private void LoadRoomData(int sensorId)
        {
            if (!_isDbConnected)
            {
                Status = $"Keine DB-Verbindung – Mock-Daten für: {_selectedRaum.Name}";
                var (mockTemp, mockHumid, mockPress) = GetMockDataForRoom(sensorId);
                ResetTrackers(mockTemp, mockHumid, mockPress);
                return;
            }

            try
            {
                var (temp, humid, press) = _sensorRepository.GetLatestDataForRoom(sensorId);
                if (temp != null)
                {
                    ResetTrackers(temp, humid, press);
                    Status = $"Raumdaten geladen: {_selectedRaum.Name}";
                }
                else
                {
                    Status = $"Keine Daten für Raum: {_selectedRaum.Name}";
                }
            }
            catch (Exception ex)
            {
                Status = "Fehler beim Laden: " + ex.Message;
            }
        }

        private void ResetTrackers(TempValue temp, HumidValue humid, PressureValue press)
        {
            _tempTracker.Reset(temp);
            _humidTracker.Reset(humid);
            _pressureTracker.Reset(press);

            OnPropertyChanged("TempAktuellValue");
            OnPropertyChanged("TempMaxValue");
            OnPropertyChanged("TempMinValue");
            OnPropertyChanged("FeuchteAktuellValue");
            OnPropertyChanged("FeuchteMaxValue");
            OnPropertyChanged("FeuchteMinValue");
            OnPropertyChanged("PressCurrentVal");
            OnPropertyChanged("PressMaxVal");
            OnPropertyChanged("PressMinVal");
            OnPropertyChanged("ShortValuesString");
        }
        #endregion

        #region Mock Data
        private List<Raum> GetMockRooms()
        {
            return new List<Raum>
            {
                new Raum { RaumID = 1, Name = "Wohnzimmer" },
                new Raum { RaumID = 2, Name = "Küche" },
                new Raum { RaumID = 3, Name = "Bad" },
                new Raum { RaumID = 4, Name = "Kinderzimmer" },
                new Raum { RaumID = 5, Name = "Schlafzimmer" },
            };
        }

        private (TempValue temp, HumidValue humid, PressureValue press) GetMockDataForRoom(int raumId)
        {
            DateTime now = DateTime.Now;
            switch (raumId)
            {
                case 1: return (new TempValue { DateAndTime = now, Value = 21.5 },
                                new HumidValue { DateAndTime = now, Value = 48.0 },
                                new PressureValue { DateAndTime = now, Value = 1013.0 });
                case 2: return (new TempValue { DateAndTime = now, Value = 23.0 },
                                new HumidValue { DateAndTime = now, Value = 55.0 },
                                new PressureValue { DateAndTime = now, Value = 1012.0 });
                case 3: return (new TempValue { DateAndTime = now, Value = 22.0 },
                                new HumidValue { DateAndTime = now, Value = 65.0 },
                                new PressureValue { DateAndTime = now, Value = 1011.0 });
                case 4: return (new TempValue { DateAndTime = now, Value = 20.0 },
                                new HumidValue { DateAndTime = now, Value = 50.0 },
                                new PressureValue { DateAndTime = now, Value = 1013.5 });
                case 5: return (new TempValue { DateAndTime = now, Value = 18.5 },
                                new HumidValue { DateAndTime = now, Value = 60.0 },
                                new PressureValue { DateAndTime = now, Value = 1010.0 });
                default: return (new TempValue { DateAndTime = now, Value = 0.0 },
                                 new HumidValue { DateAndTime = now, Value = 0.0 },
                                 new PressureValue { DateAndTime = now, Value = 0.0 });
            }
        }
        #endregion
    }
}
