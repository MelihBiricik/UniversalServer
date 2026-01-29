using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;

namespace UniversalServer.Model
{
    public class ConnectionData
    {
        public int Port { get; set; }
        public IPAddress[] IPAdressList { get; set; }
        public IPAddress SelectedIPAdress { get; set; }
    }
}
