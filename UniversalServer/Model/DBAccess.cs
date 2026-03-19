using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace UniversalServer.Model
{
    public class DBAccess
    {
        private MySqlConnection _myConnection;
        public DBAccess()
        {
            // 
            _myConnection =
                     new MySqlConnection(
                         "SERVER=localhost;" +
                         "DATABASE=SmartHomeDB2;" +
                         "UID = root;" +
                         "PASSWORD=;");
        }
        public void OpenConnectionToDBServer()
        {
            try
            {
                _myConnection.Open();
            }
            catch (Exception ex)
            {
                Exception ex2 = new Exception($"Open zur DB hat nicht geklappt!!! Läuft die DB???" + Environment.NewLine + ex.Message);
                throw ex2;
            }
        }
        public void InsertData(TempValue tv, HumidValue hv, PressureValue pv, DateTime dt, string ip)
        {
            try
            {
                OpenConnectionToDBServer(); // Verbindung zur MySQL Workbench öffnen

                // SCHRITT 1: Die SensorID für den Namen (z.B. "Wohnzimmer") herausfinden
                string getSensorIdQuery = "SELECT SensorID FROM Sensor WHERE Typ = @name LIMIT 1";
                int sID = -1;

                MySqlCommand getLog = new MySqlCommand(getSensorIdQuery, _myConnection);

                getLog.Parameters.AddWithValue("@name", ip);

                // Wir führen den Befehl aus und holen uns die ID
                object result = getLog.ExecuteScalar();

                if (result != null)
                {
                    sID = Convert.ToInt32(result);
                }
                if (sID != -1) 
                {  
                // SCHRITT 2: Den Messwert mit der gefundenen SensorID speichern
                    string insertQuery = "INSERT INTO Messwerte (Zeitpunkt, Temperatur, Luftfeuchtigkeit, Luftdruck, SensorID)" + 
                                         "VALUES(@time, @temp, @hum, @press, @sID)";


                    using (MySqlCommand cmd = new MySqlCommand(insertQuery, _myConnection))
                    {
                        // Parametreleri şemadaki DECIMAL(5,2) yapısına uygun gönderiyoruz
                        cmd.Parameters.AddWithValue("@time", dt);
                        cmd.Parameters.AddWithValue("@temp", tv.Value);
                        cmd.Parameters.AddWithValue("@hum", hv.Value);
                        cmd.Parameters.AddWithValue("@press", pv.Value);
                        cmd.Parameters.AddWithValue("@sID", sID);

                        cmd.ExecuteNonQuery(); // Daten in die Datenbank schreiben
                    }
                }
                else
                {
                    MessageBox.Show("Sensor '" + ip + "' wurde nicht in der Datenbank gefunden!");
                }
                _myConnection.Close(); // Tür wieder schließen
            }
            catch (Exception ex)
            {
                MessageBox.Show("Fehler beim Speichern: " + ex.Message);
            }
        }
    }
}