using System;

namespace UniversalServer.Model
{
    public class SensorMessage
    {
        public double Temperature { get; }
        public double Humidity { get; }
        public double Pressure { get; }
        public string IpAddress { get; }
        public DateTime ReceivedAt { get; }

        public SensorMessage(double temperature, double humidity, double pressure, string ipAddress, DateTime receivedAt)
        {
            Temperature = temperature;
            Humidity = humidity;
            Pressure = pressure;
            IpAddress = ipAddress;
            ReceivedAt = receivedAt;
        }

        public TempValue ToTempValue()     => new TempValue     { Value = Temperature, DateAndTime = ReceivedAt };
        public HumidValue ToHumidValue()   => new HumidValue    { Value = Humidity,    DateAndTime = ReceivedAt };
        public PressureValue ToPressValue() => new PressureValue { Value = Pressure,   DateAndTime = ReceivedAt };
    }
}
