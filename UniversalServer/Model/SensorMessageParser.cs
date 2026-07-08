using System;
using System.Globalization;

namespace UniversalServer.Model
{
    public static class SensorMessageParser
    {
        // Expected format: "temp;humidity;pressure;ipAddress"
        public static SensorMessage Parse(string raw)
        {
            var parts = raw.Split(';');
            if (parts.Length < 3)
                throw new FormatException($"Ungültiges Nachrichtenformat: '{raw}'");

            double temperature = double.Parse(parts[0], CultureInfo.InvariantCulture);
            double humidity    = double.Parse(parts[1], CultureInfo.InvariantCulture);
            double pressure    = double.Parse(parts[2], CultureInfo.InvariantCulture);
            string ip          = parts.Length > 3 ? parts[3].Trim() : string.Empty;

            return new SensorMessage(temperature, humidity, pressure, ip, DateTime.Now);
        }
    }
}
