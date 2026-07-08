using System;
using System.Collections.Generic;

namespace UniversalServer.Model
{
    public interface ISensorRepository
    {
        List<Raum> GetRooms();
        (TempValue temp, HumidValue humid, PressureValue press) GetLatestDataForRoom(int sensorId);
        void InsertData(TempValue tv, HumidValue hv, PressureValue pv, DateTime dt, string ip);
    }
}
