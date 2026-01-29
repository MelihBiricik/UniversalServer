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

        private ICommand _windowLoadedCommand;
        private ICommand _startListeningCommand;
        private IServerContract _serv;
        private string _status;

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


        public ICommand StartListeningCommand
        {
            get
            {
                if (_startListeningCommand == null)
                {
                    _startListeningCommand = new RelayCommand(c => StartListening());
                }
                return _startListeningCommand;

            }
        }

        private void StartListening()
        {
            //Nach den Laden des Windows, den Server starten.
            //_serv = new Server();
            _serv = new ServerMockUp(); //zum Testen kann auch ein MockUp als Datenquelle verwendet werden.

            _serv.StatusPropertyChanged += Serv_StatusPropertyChanged;
            _serv.MessageReceived += _serv_MessageReceived;
            _serv.Start(SelectedIPAdress, PortToListen);

        }

        private void ExecuteWindowLoadedCommand()
        {
            try
            {
                _dba.OpenConnectionToDBServer();
            }
            catch (Exception ex)
            {
                Status = ex.Message;
            }

            try
            {
                IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
                //IPAddress ipAddress = ipHostInfo.AddressList[6];
                var ipList = new ObservableCollection<IPAddress>(ipHostInfo.AddressList);
               
                ipList.Add(IPAddress.Loopback);
                AvailableIPAdresses = ipList;

                //SelectedIPAdress = ipHostInfo.AddressList[0];
                SelectedIPAdress = ipList.Where(adr => adr == IPAddress.Loopback).First();
            }
            catch (Exception ex)
            {
                Status = ex.Message;
            }
        }


        private void Serv_StatusPropertyChanged(string s)
        {
            Status = s;
        }


        private void _serv_MessageReceived(string msg)
        {
            Status = DateTime.Now.ToShortTimeString() + ": " + msg;

            //Message auf analysieren und auf die Eigenschaften verteilen.
            //Protokoll-Format: Temperatur;Luftfeuchte;Luftdruck;IP des Boards
            try
            {
                string temp = msg.Split(';')[0].Replace('.', ',');

                double t = Convert.ToDouble(temp);

                TempAktuellValue = new TempValue() { DateAndTime = DateTime.Now, Value = t };

                double luftfeuchte = Convert.ToDouble(msg.Split(';')[1].Replace('.', ','));
                FeuchteAktuellValue = new HumidValue() { DateAndTime = DateTime.Now, Value = luftfeuchte };

                string d = msg.Split(';')[2].Replace('.', ',');
                double druck = Convert.ToDouble(d);

                PressCurrentVal = new PressureValue() { DateAndTime = DateTime.Now, Value = druck };

                string ipAdr = msg.Split(';')[3];

                bool wrongCommandDetected = msg.Contains("DROP");
                if (wrongCommandDetected)
                    throw new Exception(msg);


                //Daten in die Datenbank schreiben.
                try
                {
                    _dba.InsertData(TempAktuellValue, FeuchteAktuellValue, PressCurrentVal, DateTime.Now, ipAdr);
                }
                catch (Exception ex)
                {
                    Status = ex.Message;
                }

                //Max und Min-Werte feststellen und spiechern.
                if (TempMaxValue != null)
                {
                    if (TempAktuellValue.Value > TempMaxValue.Value)
                    {
                        TempMaxValue = TempAktuellValue;
                    }
                }
                else
                { TempMaxValue = TempAktuellValue; }

                if (TempMinValue != null)
                {
                    if (TempAktuellValue.Value < TempMinValue.Value)
                    {
                        TempMinValue = TempAktuellValue;
                    }
                }
                else
                {
                    TempMinValue = TempAktuellValue;
                }

                //Feuchte
                if (FeuchteMaxValue != null)
                {
                    if (FeuchteAktuellValue.Value > FeuchteMaxValue.Value)
                    {
                        FeuchteMaxValue = FeuchteAktuellValue;
                    }
                }
                else
                {
                    FeuchteMaxValue = FeuchteAktuellValue;
                }

                if (FeuchteMinValue != null)
                {
                    if (FeuchteAktuellValue.Value < FeuchteMinValue.Value)
                    {
                        FeuchteMinValue = FeuchteAktuellValue;
                    }
                }
                else
                {
                    FeuchteMinValue = FeuchteAktuellValue;
                }

                //Luftdruck
                if (PressMaxVal != null)
                {
                    if (PressCurrentVal.Value > PressMaxVal.Value)
                    {
                        PressMaxVal = PressCurrentVal;
                    }
                }
                else
                {
                    PressMaxVal = PressCurrentVal;
                }

                if (PressMinVal != null)
                {
                    if (PressCurrentVal.Value < PressMinVal.Value)
                    {
                        PressMinVal = PressCurrentVal;
                    }
                }
                else
                {
                    PressMinVal = PressCurrentVal;
                }
            }
            catch (Exception ex)
            {
                Status = "Fehler beim Interpretieren der Werte. " + ex.Message + Environment.NewLine + msg;

            }
        }

    }
}
