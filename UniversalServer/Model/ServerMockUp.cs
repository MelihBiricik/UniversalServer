using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UniversalServer.Model
{
    /// <summary>
    /// Dies ist ein Simulator, der die eingehenden Daten (Temperaturwerte, Feuchtigkeit usw.) 
    /// simuliert, die ansonsten vom ESP8266 kommen.
    /// </summary>
    public class ServerMockUp : IServerContract
    {

        public event StatusChangedEventHandler StatusPropertyChanged;
        public event MessageReceivedEventHandler MessageReceived;

        Timer _tmr;
        private int _isRunningTick;



        public void Start(IPAddress ipadr, int port)
        {
            //Parameter werden beim MockUp nicht benötig... Das Interface gibt sie aber vor...

            StatusPropertyChanged("Starting Server...");
            Thread.Sleep(500); //Verzögerung simulieren, wenn wir später auf echte Sockets gehen.

            //Timer starten oder fortsetzen, der uns zyklisch Werte liefert.
            if (_tmr == null)
            {
                _tmr = new Timer(new TimerCallback(TimerProc));
                _tmr.Change(1000, 2000);
            }
            else
            {
                // resume timer
                try { _tmr.Change(1000, 2000); } catch { }
            }

            StatusPropertyChanged("Waiting for Connection...");
        }

        private void TimerProc(object state)
        {
            // Verhindert überlappende Ausführungen, falls der Timer (z.B. durch Debugging-Pausen)
            // bereits fällige Ticks aufgestaut hat und mehrere Callbacks gleichzeitig anlaufen.
            if (Interlocked.Exchange(ref _isRunningTick, 1) == 1)
                return;

            try
            {
                // Protokoll to simulate: Temperatur;Luftfeuchte;Luftdruck;IP
                Random rndm = new Random();

                double temp = 22 + rndm.NextDouble() - rndm.NextDouble();
                double hum = 50 + rndm.Next(-5, 5);
                int press = 1024 + rndm.Next(-20, 20);
                string ip = new string[] {
                    "192.168.1.145",
                    "192.168.1.99",
                    "192.168.1.32",
                    "192.168.1.234",
                    "192.168.1.10",
                    "192.168.1.77",
                }[rndm.Next(0, 5)];

                string data =
                    String.Format(CultureInfo.InvariantCulture, "{0:0.00}", temp) + ";" +
                    String.Format(CultureInfo.InvariantCulture, "{0:00.00}", hum) + ";" +
                    String.Format(CultureInfo.InvariantCulture, "{0:0000}", press) + ";" +
                    ip;

                MessageReceived(data);
            }
            finally
            {
                Interlocked.Exchange(ref _isRunningTick, 0);
            }
        }

        public void Stop()
        {
            try
            {
                if (_tmr != null)
                {
                    // pause timer callbacks but keep timer instance so we can resume later
                    try { _tmr.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite); } catch { }
                }
                StatusPropertyChanged("Mockup paused.");
            }
            catch (Exception ex)
            {
                StatusPropertyChanged("Error pausing mockup: " + ex.Message);
            }
        }

    }
}
