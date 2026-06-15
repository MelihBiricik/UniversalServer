using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using UniversalServer.Model;
using UniversalServer.ViewModelBase;


namespace UniversalServer.ViewModels
{
    class MainViewModel : ViewModel
    {

        #region Fields
        DBAccess _dba = new DBAccess();
        List<IPAddress> _avIPAdresses;
        int _portToListen;
        IPAddress _selectedIPAdress;
        List<Raum> _availableRooms;
        Raum _selectedRaum;
        private bool _isDbConnected = false;

        private ICommand _windowLoadedCommand;
        private ICommand _startListeningCommand;
        private ICommand _stopListeningCommand;
        private IServerContract _serv;
        private string _status;
        private volatile bool _isRunning = false;

        TempValue _tempMaxVal;
        TempValue _tempCurrentVal;
        TempValue _tempMinVal;

        HumidValue _humiMaxVal;
        HumidValue _humiCurrentVal;
        HumidValue _humiMinVal;

        PressureValue _pressMaxVal;
        PressureValue _pressCurrentVal;
        PressureValue _pressMinVal;
        #endregion

        #region Properties
        public int PortToListen
        {
            get
            {
                return _portToListen;
            }
            set
            {
                _portToListen = value;
                OnPropertyChanged("PortToListen");
            }
        }
        public ObservableCollection<IPAddress> AvailableIPAdresses
        {
            get
            {
                if(_avIPAdresses != null)
                    return new ObservableCollection<IPAddress>(_avIPAdresses);
                else
                    return new ObservableCollection<IPAddress>();
            }
            set
            {
                _avIPAdresses = value.ToList<IPAddress>();
                OnPropertyChanged("AvailableIPAdresses");
            }
        }

        public IPAddress SelectedIPAdress
        {
            get
            {
                return _selectedIPAdress;
            }
            set
            {
                _selectedIPAdress = value;
                OnPropertyChanged("SelectedIPAdress");
            }
        }

        public ObservableCollection<Raum> AvailableRooms
        {
            get => _availableRooms != null
                ? new ObservableCollection<Raum>(_availableRooms)
                : new ObservableCollection<Raum>();
            set
            {
                _availableRooms = value.ToList();
                OnPropertyChanged("AvailableRooms");
            }
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

        public HumidValue FeuchteMaxValue
        {
            get
            {
                return _humiMaxVal;
            }
            set
            {
                _humiMaxVal = value;
                OnPropertyChanged("FeuchteMaxValue");
            }
        }
        public HumidValue FeuchteAktuellValue
        {
            get
            {
                return _humiCurrentVal;
            }
            set
            {
                _humiCurrentVal = value;
                OnPropertyChanged("FeuchteAktuellValue");
            }
        }
        public HumidValue FeuchteMinValue
        {
            get
            {
                return _humiMinVal;
            }
            set
            {
                _humiMinVal = value;
                OnPropertyChanged("FeuchteMinValue");
            }
        }
        public TempValue TempMaxValue
        {
            get
            {
                return _tempMaxVal;
            }
            set
            {
                _tempMaxVal = value;
                OnPropertyChanged("TempMaxValue");
            }
        }
        public TempValue TempAktuellValue
        {
            get
            {
                return _tempCurrentVal;
            }
            set
            {
                _tempCurrentVal = value;
                OnPropertyChanged("TempAktuellValue");
            }
        }
        public TempValue TempMinValue
        {
            get
            {
                return _tempMinVal;
            }

            set
            {
                _tempMinVal = value;
                OnPropertyChanged("TempMinValue");
            }
        }
        public PressureValue PressMaxVal
        {
            get => _pressMaxVal;
            set
            {
                _pressMaxVal = value;
                OnPropertyChanged("PressMaxVal");
            }
        }
        public string ShortValuesString { get => _tempCurrentVal?.Value + ", " + _humiCurrentVal?.Value + ", " + _pressCurrentVal?.Value; }
        public PressureValue PressCurrentVal { get => _pressCurrentVal; set { _pressCurrentVal = value; OnPropertyChanged("PressCurrentVal"); } }
        public PressureValue PressMinVal { get => _pressMinVal; set { _pressMinVal = value; OnPropertyChanged("PressMinVal"); } }
        public string Status
        {
            get
            {
                return _status;
            }
            set
            {
                _status = value;
                OnPropertyChanged("Status");
            }
        }
        #endregion  

        public MainViewModel()
        {
            _portToListen = 11000;
            _avIPAdresses = null;
            _selectedIPAdress = null;
        }

        public ICommand WindowLoaded
        {
            get
            {
                if (_windowLoadedCommand == null)
                {
                    _windowLoadedCommand = new RelayCommand(c => ExecuteWindowLoadedCommand());
                }
                return _windowLoadedCommand;

            }
        }


        public ICommand LoadedCommand => WindowLoaded;

        public ICommand StartListeningCommand
        {
            get
            {
                if (_startListeningCommand == null)
                {
                    _startListeningCommand = new RelayCommand(c => StartListening(), c => !_isRunning);
                }
                return _startListeningCommand;

            }
        }

        public ICommand StopListeningCommand
        {
            get
            {
                if (_stopListeningCommand == null)
                {
                    _stopListeningCommand = new RelayCommand(c => StopListening(), c => _isRunning);
                }
                return _stopListeningCommand;

            }
        }

        private void StartListening()
        {
            try
            {
                // Wenn noch keine Server-Instanz existiert, anlegen und Events abonnieren.
                if (_serv == null)
                {
                    _serv = new ServerMockUp(); //zum Testen kann auch ein MockUp als Datenquelle verwendet werden.
                    _serv.StatusPropertyChanged += Serv_StatusPropertyChanged;
                    _serv.MessageReceived += _serv_MessageReceived;
                }

                // Start oder resume des Servers/Mockups
                _serv.Start(SelectedIPAdress, PortToListen);

                // Merken, dass wir laufen, damit Commands korrekt aktiv/disabled sind
                _isRunning = true;
                Status = "Listening...";

                // Command-Manager neu auswerten damit Buttons ihren Enabled-Status aktualisieren
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
                    // Pause handling further incoming messages on UI
                    _isRunning = false;

                    // Pause server but keep instance so Start can resume
                    _serv.Stop();

                    Status = "Paused listening.";

                    // Update command states
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

        private void ExecuteWindowLoadedCommand()
        {
            //try
            //{
            //    _dba.OpenConnectionToDBServer();
            //}
            //catch (Exception ex)
            //{
            //    Status = ex.Message;
            //}

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
                var rooms = _dba.GetRooms();
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

        private List<Raum> GetMockRooms()
        {
            return new List<Raum>
            {                 
                 new Raum { RaumID = 1, Name = "Wohnzimmer" },
                 new Raum { RaumID = 2, Name = "Küche" },
                 new Raum { RaumID = 3, Name = "Bad" },
                 new Raum { RaumID = 4, Name = "Kinderzimmer" },
                 new Raum { RaumID = 5, Name = "Terasse" },
            };
        }

        private (TempValue temp, HumidValue humid, PressureValue press) GetMockDataForRoom(int raumId)
        {
            DateTime now = DateTime.Now;
            switch (raumId)
            {
                case 1: return (new TempValue    { DateAndTime = now, Value = 21.5 },
                                new HumidValue   { DateAndTime = now, Value = 48.0 },
                                new PressureValue{ DateAndTime = now, Value = 1013.0 });
                case 2: return (new TempValue    { DateAndTime = now, Value = 23.0 },
                                new HumidValue   { DateAndTime = now, Value = 55.0 },
                                new PressureValue{ DateAndTime = now, Value = 1012.0 });
                case 3: return (new TempValue    { DateAndTime = now, Value = 22.0 },
                                new HumidValue   { DateAndTime = now, Value = 65.0 },
                                new PressureValue{ DateAndTime = now, Value = 1011.0 });
                case 4: return (new TempValue    { DateAndTime = now, Value = 20.0 },
                                new HumidValue   { DateAndTime = now, Value = 50.0 },
                                new PressureValue{ DateAndTime = now, Value = 1013.5 });
                case 5: return (new TempValue    { DateAndTime = now, Value = 18.5 },
                                new HumidValue   { DateAndTime = now, Value = 60.0 },
                                new PressureValue{ DateAndTime = now, Value = 1010.0 });
                default: return (new TempValue    { DateAndTime = now, Value = 0.0 },
                                 new HumidValue   { DateAndTime = now, Value = 0.0 },
                                 new PressureValue{ DateAndTime = now, Value = 0.0 });
            }
        }

        private void LoadRoomData(int sensorId)
        {
            if (!_isDbConnected)
            {
                Status = $"Keine DB-Verbindung – Mock-Daten für: {_selectedRaum.Name}";
                //return;
                var (mockTemp, mockHumid, mockPress) = GetMockDataForRoom(sensorId);
                TempAktuellValue  = mockTemp;
                TempMaxValue      = mockTemp;
                TempMinValue      = mockTemp;
                FeuchteAktuellValue = mockHumid;
                FeuchteMaxValue     = mockHumid;
                FeuchteMinValue     = mockHumid;
                PressCurrentVal   = mockPress;
                PressMaxVal       = mockPress;
                PressMinVal       = mockPress;
                return;
            }
            try
            {
                var (temp, humid, press) = _dba.GetLatestDataForRoom(sensorId);
                if (temp != null)
                {
                    TempAktuellValue = temp;
                    TempMaxValue = temp;
                    TempMinValue = temp;
                    FeuchteAktuellValue = humid;
                    FeuchteMaxValue = humid;
                    FeuchteMinValue = humid;
                    PressCurrentVal = press;
                    PressMaxVal = press;
                    PressMinVal = press;
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


        private void Serv_StatusPropertyChanged(string s)
        {
            Status = s;
        }


        private void _serv_MessageReceived(string msg)
        {
            // Handle incoming messages on a background thread to avoid blocking UI.
            var incoming = msg;
            Task.Run(() =>
            {
                try
                {
                    var parts = incoming.Split(';');
                    var tempStr = parts[0].Replace('.', ',');
                    var humStr = parts[1].Replace('.', ',');
                    var pressStr = parts[2].Replace('.', ',');
                    var ipAdr = parts.Length > 3 ? parts[3] : string.Empty;

                    double t = Convert.ToDouble(tempStr);
                    double luftfeuchte = Convert.ToDouble(humStr);
                    double druck = Convert.ToDouble(pressStr);

                    var newTemp = new TempValue() { DateAndTime = DateTime.Now, Value = t };
                    var newHum = new HumidValue() { DateAndTime = DateTime.Now, Value = luftfeuchte };
                    var newPress = new PressureValue() { DateAndTime = DateTime.Now, Value = druck };

                    // write to DB on background thread
                    try
                    {
                        _dba.InsertData(newTemp, newHum, newPress, DateTime.Now, ipAdr);
                    }
                    catch (Exception dbex)
                    {
                        // update status on UI
                        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => Status = dbex.Message));
                    }

                    // update UI-bound properties on UI thread
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        // If we've been stopped in the meantime, ignore updates.
                        if (!_isRunning)
                        {
                            return;
                        }

                        Status = DateTime.Now.ToShortTimeString() + ": " + incoming;

                        TempAktuellValue = newTemp;
                        FeuchteAktuellValue = newHum;
                        PressCurrentVal = newPress;

                        // detect wrong command
                        if (incoming.Contains("DROP"))
                        {
                            Status = "Fehler beim Interpretieren der Werte. " + incoming;
                            return;
                        }

                        // Max/Min logic
                        if (TempMaxValue == null || TempAktuellValue.Value > TempMaxValue.Value)
                            TempMaxValue = TempAktuellValue;
                        if (TempMinValue == null || TempAktuellValue.Value < TempMinValue.Value)
                            TempMinValue = TempAktuellValue;

                        if (FeuchteMaxValue == null || FeuchteAktuellValue.Value > FeuchteMaxValue.Value)
                            FeuchteMaxValue = FeuchteAktuellValue;
                        if (FeuchteMinValue == null || FeuchteAktuellValue.Value < FeuchteMinValue.Value)
                            FeuchteMinValue = FeuchteAktuellValue;

                        if (PressMaxVal == null || PressCurrentVal.Value > PressMaxVal.Value)
                            PressMaxVal = PressCurrentVal;
                        if (PressMinVal == null || PressCurrentVal.Value < PressMinVal.Value)
                            PressMinVal = PressCurrentVal;
                    }));
                }
                catch (Exception ex)
                {
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        Status = "Fehler beim Interpretieren der Werte. " + ex.Message + Environment.NewLine + incoming;
                    }));
                }
            });
        }

    }
}
