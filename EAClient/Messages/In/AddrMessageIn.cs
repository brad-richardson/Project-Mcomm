using SSX3_Server.EAServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace SSX3_Server.EAClient.Messages
{
    public class AddrMessageIn : EAMessage
    {
        public override string MessageType { get { return "addr"; } }

        public string ADDR;
        public string PORT;

        public override void AssignValues()
        {
            ADDR = GetStringData("ADDR");
            PORT = GetStringData("PORT");
        }

        public override void AssignValuesToString()
        {
            AddStringData("ADDR", ADDR);
            AddStringData("PORT", PORT);
        }

        public override void ProcessCommand(EAClientManager client, EAServerRoom room = null)
        {
            // ONL4: fallback for a client that reports 0.0.0.0:0
            // (unknown local address). Substitute the observed TCP
            // endpoint, in the spirit of ChalMessageIn's LAN assist.
            // NOTE: the observed port is the lobby TCP port, not the
            // game's UDP port; +ses ADDR/FROM correctness for P2P is
            // revisited with the race capture.
            if (ADDR == "0.0.0.0" || string.IsNullOrEmpty(ADDR))
            {
                ADDR = client.IPAddress;
            }
            if (PORT == "0" || string.IsNullOrEmpty(PORT))
            {
                try
                {
                    var remote = client.MainClient?.Client?.RemoteEndPoint as IPEndPoint;
                    PORT = (remote != null) ? remote.Port.ToString() : "3658";
                }
                catch
                {
                    PORT = "3658";
                }
            }
            client.LocalIP = ADDR;
            client.GamePort = PORT;
            client.Broadcast(this);
        }
    }
}
