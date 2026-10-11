using SSX3_Server.EAServer;

namespace SSX3_Server.EAClient.Messages
{
    public class ChalMessageIn : EAMessage
    {
        public override string MessageType { get { return "chal"; } }

        public string PERS = "";
        public string HOST = "";
        public string MODE = "";

        public string FromPlayer;

        public static List<ChalMessageIn> chalMessageIns = new List<ChalMessageIn>();

        public override void AssignValues()
        {
            PERS = GetStringData("PERS");
            HOST = GetStringData("HOST");
        }

        public override void AssignValuesToString()
        {
            AddStringData("MODE", MODE);
        }

        public override void ProcessCommand(EAClientManager client, EAServerRoom room = null)
        {
            //Add to list
            FromPlayer = client.LoadedPersona.Name;
            lock (chalMessageIns)
            {
                if (PERS == "*" || PERS == "")
                {
                    for (int i = 0; i < chalMessageIns.Count; i++)
                    {
                        if (chalMessageIns[i].FromPlayer == FromPlayer)
                        {
                            chalMessageIns.RemoveAt(i);

                            break;
                        }
                    }
                    DQUEMessageout dQUEMessageout = new DQUEMessageout();

                    client.Broadcast(this);
                    return;
                }
                else
                {
                    chalMessageIns.Add(this);

                    bool Host = false;
                    bool Oppo = false;
                    ChalMessageIn HostEntry = new ChalMessageIn();
                    ChalMessageIn OppoEntry = new ChalMessageIn();

                    ChalMessageIn chalMessageIn = new ChalMessageIn();

                    chalMessageIn.MODE = "chal";

                    client.Broadcast(chalMessageIn);

                   //Check to see if theres a host command and a receive
                   for (int i = 0; i < chalMessageIns.Count; i++)
                   {
                       if (chalMessageIns[i].HOST == "1" && (chalMessageIns[i].FromPlayer == PERS || chalMessageIns[i].PERS == FromPlayer))
                       {
                           Host = true;
                           HostEntry = chalMessageIns[i];
                           break;
                       }
                   }

                   //if so get the host command and the receive seperated
                   for (int i = 0; i < chalMessageIns.Count; i++)
                   {
                       if (chalMessageIns[i].HOST == "0" && chalMessageIns[i].FromPlayer == HostEntry.PERS)
                       {
                           Oppo = true;
                           OppoEntry = chalMessageIns[i];
                           break;
                       }
                   }

                    //Generate host and send to player
                    if (Host && Oppo)
                    {
                        var HostClient = EAServerManager.Instance.GetUserPersona(HostEntry.FromPlayer);
                        var OtherUser = EAServerManager.Instance.GetUserPersona(OppoEntry.FromPlayer);

                        CreateSession(HostClient, OtherUser,
                            HostClient.challange.TrackID.ToString(),
                            HostClient.challange.Gamemode2,
                            HostClient.challange.Ranked);

                        chalMessageIns.Remove(HostEntry);
                        chalMessageIns.Remove(OppoEntry);
                    }
                }
            }
        }

        // Shared +ses construction for challenge matches and quick-play pairs.
        // trackId/gamemode2/ranked come from the mesg challenge text on the chal
        // path and from the agreed quik flags on the quick-play path.
        public static void CreateSession(EAClientManager HostClient, EAClientManager OtherUser,
            string trackId, string gamemode2, string ranked)
        {
            string Seed = (new Random()).Next().ToString();

            string HostIP = HostClient.IPAddress;
            string OtherIP = OtherUser.IPAddress;

            bool IPTest = false;
            if (HostIP == OtherIP)
            {
                IPTest = true;
                //Assume Same Network so try local connection
                HostIP = HostClient.LocalIP;
                OtherIP = OtherUser.LocalIP;
            }

            if (HostClient.OverrideIP != "")
            {
                HostIP = HostClient.OverrideIP;
            }

            if (OtherUser.OverrideIP != "")
            {
                OtherIP = OtherUser.OverrideIP;
            }

            PlusSesMessageOut plusSesMessageOut = new PlusSesMessageOut();

            Guid g = Guid.NewGuid();
            plusSesMessageOut.NAME = g.ToString();
            plusSesMessageOut.SELF = OtherUser.LoadedPersona.Name;//HostClient.LoadedPersona.Name;
            plusSesMessageOut.HOST = HostClient.LoadedPersona.Name;
            plusSesMessageOut.FROM = HostIP;

            plusSesMessageOut.OPPO = OtherUser.LoadedPersona.Name;
            plusSesMessageOut.ADDR = OtherIP;

            plusSesMessageOut.P1 = trackId;
            plusSesMessageOut.P2 = gamemode2;
            plusSesMessageOut.P3 = "0"; //Unknown
            plusSesMessageOut.P4 = "0"; //Unknown
            plusSesMessageOut.AUTH = ranked;
            plusSesMessageOut.SEED = Seed;
            plusSesMessageOut.WHEN = DateTime.Now.ToString("yyyy.MM.dd hh:mm:ss");

            if (gamemode2 == 4.ToString())
            {
                plusSesMessageOut.P2 = 0.ToString();
            }
            if (gamemode2 == 5.ToString())
            {
                plusSesMessageOut.P2 = 1.ToString();
            }

            if (HostClient.ForceGamemodeID != -1)
            {
                plusSesMessageOut.P2 = HostClient.ForceGamemodeID.ToString();
            }

            if (HostClient.ForceTrackID != -1)
            {
                plusSesMessageOut.P1 = HostClient.ForceTrackID.ToString();
            }

            HostClient.EnteringChalAt = DateTime.Now;
            OtherUser.EnteringChalAt = DateTime.Now;

            SessionDatabse.SessionData sessionData = new SessionDatabse.SessionData();

            sessionData.GUID = plusSesMessageOut.NAME;
            sessionData.Player0 = HostClient.LoadedPersona.Name;
            sessionData.Player1 = OtherUser.LoadedPersona.Name;
            sessionData.Ranked = (ranked == "1");
            sessionData.When = DateTime.Now.ToString("yyyy.M.d h:mm:ss");
            sessionData.Auth = plusSesMessageOut.AUTH;
            sessionData.Valid = false;

            EAServerManager.Instance.sessionDatabse.sessionDatas.Insert(0, sessionData);

            EAServerManager.Instance.sessionDatabse.CreateJson(AppContext.BaseDirectory + "\\Session.json");


            OtherUser.Broadcast(plusSesMessageOut);

            //Double Check This Data Something might be wrong to cause a abort
            plusSesMessageOut.SELF = HostClient.LoadedPersona.Name;//OtherUser.LoadedPersona.Name;
            //plusSesMessageOut.OPPO = HostClient.LoadedPersona.Name;
            //plusSesMessageOut.ADDR = HostIP;
            //plusSesMessageOut.FROM = HostIP;

            HostClient.Broadcast(plusSesMessageOut);

            OtherUser.TimeoutSeconds = 60 * 10;
            HostClient.TimeoutSeconds = 60 * 10;
            HostClient.PingTimeout = 60 * 10;
            OtherUser.PingTimeout = 60 * 10;

            ConsoleManager.WriteLine(HostClient.LoadedPersona.Name + " Started Session with " + OtherUser.LoadedPersona.Name);
            if (IPTest)
            {
                ConsoleManager.WriteLine(HostClient.LoadedPersona.Name + " and " + OtherUser.LoadedPersona.Name + " Have Same Global IP Sent Local IP instead");
            }
        }

        public static void RemoveChallange(EAClientManager client, MesgMessageIn mesgMessageIn = null)
        {
            //Clear Out Challange
            for (global::System.Int32 i = 0; i < ChalMessageIn.chalMessageIns.Count; i++)
            {
                var TempChal = ChalMessageIn.chalMessageIns[i];

                if (TempChal.FromPlayer == client.LoadedPersona.Name)
                {
                    ChalMessageIn.chalMessageIns.Remove(TempChal);
                }
            }
            if (mesgMessageIn != null)
            {
                for (global::System.Int32 i = 0; i < ChalMessageIn.chalMessageIns.Count; i++)
                {
                    var TempChal = ChalMessageIn.chalMessageIns[i];

                    if (TempChal.FromPlayer == mesgMessageIn.PRIV)
                    {
                        ChalMessageIn.chalMessageIns.Remove(TempChal);
                    }
                }
            }
        }
    }
}
