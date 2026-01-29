using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace UniversalServer.Model
{
    public interface IServerContract
    {
        event StatusChangedEventHandler StatusPropertyChanged;
        event MessageReceivedEventHandler MessageReceived;
        void Start(IPAddress ipadr, int port);
    }
}
