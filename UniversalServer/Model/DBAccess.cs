using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;

namespace UniversalServer.Model
{
    public class DBAccess : ISensorRepository
    {
        private readonly string _connectionString =
            "SERVER=localhost;" +
            "DATABASE=SmartHomeDB2;" +
            "UID=root;" +
            "PASSWORD=;";

        private MySqlConnection OpenConnection()
        {
            var conn = new MySqlConnection(_connectionString);
            try
            {
                conn.Open();
                return conn;
            }
            catch (Exception ex)
            {
                conn.Dispose();
                throw new InvalidOperationException("Verbindung zur Datenbank fehlgeschlagen. Läuft der DB-Server?", ex);
            }
        }

        public List<Raum> GetRooms()
        {
            var rooms = new List<Raum>();
            using (var conn = OpenConnection())
            {
                const string query = "SELECT RaumID, Name FROM Raum ORDER BY Name";
                using (var cmd = new MySqlCommand(query, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        rooms.Add(new Raum
                        {
                            RaumID = reader.GetInt32("RaumID"),
                            Name   = reader.GetString("Name")
                        });
                    }
                }
            }
            return rooms;
        }

        public (TempValue temp, HumidValue humid, PressureValue press) GetLatestDataForRoom(int raumId)
        {
            using (var conn = OpenConnection())
            {
                const string query =
                    "SELECT m.Zeitpunkt, m.Temperatur, m.Luftfeuchtigkeit, m.Luftdruck " +
                    "FROM Messwerte m " +
                    "JOIN Sensor s ON s.SensorID = m.SensorID " +
                    "WHERE s.RaumID = @raumId ORDER BY m.Zeitpunkt DESC LIMIT 1";

                using (var cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@raumId", raumId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read())
                            return (null, null, null);

                        DateTime dt = reader.GetDateTime("Zeitpunkt");
                        return (
                            new TempValue     { DateAndTime = dt, Value = Convert.ToDouble(reader["Temperatur"]) },
                            new HumidValue    { DateAndTime = dt, Value = Convert.ToDouble(reader["Luftfeuchtigkeit"]) },
                            new PressureValue { DateAndTime = dt, Value = Convert.ToDouble(reader["Luftdruck"]) }
                        );
                    }
                }
            }
        }

        public void InsertData(TempValue tv, HumidValue hv, PressureValue pv, DateTime dt)
        {
            using (var conn = OpenConnection())
            {
                int sensorId = GetRandomSensorId(conn);

                const string insertQuery =
                    "INSERT INTO Messwerte (Zeitpunkt, Temperatur, Luftfeuchtigkeit, Luftdruck, SensorID) " +
                    "VALUES(@time, @temp, @hum, @press, @sID)";

                using (var cmd = new MySqlCommand(insertQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@time",  dt);
                    cmd.Parameters.AddWithValue("@temp",  tv.Value);
                    cmd.Parameters.AddWithValue("@hum",   hv.Value);
                    cmd.Parameters.AddWithValue("@press", pv.Value);
                    cmd.Parameters.AddWithValue("@sID",   sensorId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private int GetRandomSensorId(MySqlConnection conn)
        {
            const string query = "SELECT SensorID FROM Sensor ORDER BY RAND() LIMIT 1";
            using (var cmd = new MySqlCommand(query, conn))
            {
                object result = cmd.ExecuteScalar();
                if (result == null)
                    throw new InvalidOperationException("Es sind keine Sensoren in der Datenbank vorhanden.");
                return Convert.ToInt32(result);
            }
        }
    }
}
