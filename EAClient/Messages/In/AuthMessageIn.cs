using SSX3_Server.EAServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SSX3_Server.EAClient.Messages
{
    public class AuthMessageIn : EAMessage
    {
        public override string MessageType { get { return "auth"; } }

        public string NAME;
        public string PASS;
        public string TOS;
        public string MID;
        public string PID;
        public string HWFLAG;
        public string HWMASK;
        public string PROD;
        public string VERS;
        public string LANG;
        public string SLUS;

        public override void AssignValues()
        {
            NAME = GetStringData("NAME");
            PASS = GetStringData("PASS");
            TOS = GetStringData("TOS");
            MID = GetStringData("MID");
            PID = GetStringData("PID");
            HWFLAG = GetStringData("HWFLAG");
            HWMASK = GetStringData("HWMASK");
            PROD = GetStringData("PROD");
            VERS = GetStringData("VERS");
            LANG = GetStringData("LANG");
            SLUS = GetStringData("SLUS");
        }

        public override void AssignValuesToString()
        {
            AddStringData("NAME", NAME);
            AddStringData("PASS", PASS);
            AddStringData("TOS", TOS);
            AddStringData("MID", MID);
            AddStringData("PID", PID);
            AddStringData("HWFLAG", HWFLAG);
            AddStringData("HWMASK", HWMASK);
            AddStringData("PROD", PROD);
            AddStringData("VERS", VERS);
            AddStringData("LANG", LANG);
            AddStringData("SLUS", SLUS);
        }

        public override void ProcessCommand(EAClientManager client, EAServerRoom room = null)
        {
            //Apply AUTH Data
            client.VERS = VERS.Replace("\"", "");


            //Confirm Auth Data with saves
            var UserData = EAClientManager.GetUserData(NAME);
            // ONL4-P2 anonymous quick play: accept any non-banned,
            // non-empty name, auto-creating the user and a default
            // persona (rank/highscore storage stays default/ignored).
            // The existing login-ok path below then runs unchanged.
            if (UserData == null && !string.IsNullOrEmpty(NAME) &&
                !EAServerManager.Instance.BannedNames.Contains(NAME))
            {
                string now = DateTime.Now.ToString("yyyy.MM.dd hh:mm:ss");
                var nu = new EAUserData();
                nu.Name = NAME;
                nu.Pass = "";
                nu.Spam = "0";
                nu.Mail = "";
                nu.Gend = "m";
                nu.Born = "1990.01.01";
                nu.Defper = NAME;
                nu.Alts = "";
                nu.Minage = "1";
                nu.Lang = LANG;
                nu.Prod = PROD;
                nu.Vers = VERS;
                nu.GameReg = SLUS;
                nu.PersonaList = new List<string>();
                nu.Since = now;
                nu.Last = now;
                nu.Bypass = true;
                nu.IPApproved.Add(ByteUtil.CreateSHA256(client.IPAddress));
                var np = new EAUserPersona();
                np.Owner = NAME;
                np.Name = NAME;
                np.Since = now;
                np.Last = now;
                np.CreateJson(AppContext.BaseDirectory + "\\Personas\\" + NAME.ToLower() + ".json");
                nu.PersonaList.Add(np.Name);
                nu.CreateJson(AppContext.BaseDirectory + "\\Users\\" + NAME.ToLower() + ".json");
                ConsoleManager.WriteLine(client.IPAddress + " auto-created anonymous account " + NAME);
                UserData = nu;
            }
            if (UserData != null)
            {
                AuthMessageOut msg2 = new AuthMessageOut();

                var UserTest = EAServerManager.Instance.GetUser(NAME);
                if (UserTest != null)
                {
                    if(client.IPAddress== UserTest.IPAddress)
                    {
                        UserTest.DestroyClient();
                        Thread.Sleep(1000);
                    }
                    else
                    {
                        msg2.SubMessage = "logn";
                        client.Broadcast(msg2);
                        Thread.Sleep(10000);
                        client.DestroyClient();
                        return;
                    }
                }


                client.userData = UserData;

                //ConsoleManager.WriteLine(ByteUtil.Decrypt(PASS.Replace("\"", ""), client.MASK));

                if (((UserData.Name == NAME /*&& UserData.Pass == ByteUtil.CreateMD5(PASS)*/) || UserData.Bypass == true) && UserData.Banned == false)
                {
                    client.userData.Last = DateTime.Now.ToString("yyyy.MM.dd hh:mm:ss");

                    client.SaveEAUserData();

                    msg2.TOS = "1";
                    msg2.MAIL = UserData.Mail;
                    msg2.PERSONAS = client.GetPersonaList();
                    msg2.BORN = UserData.Born;
                    msg2.GEND = UserData.Gend;
                    msg2.FROM = "US";
                    msg2.LANG = "en";
                    msg2.SPAM = UserData.Spam;
                    msg2.SINCE = UserData.Since;

                    client.LoggedIn = true;

                    ConsoleManager.WriteLine(NAME + " Logged in from " + client.IPAddress);

                    client.Broadcast(msg2);

                    EAServerManager.Instance.SendRooms(client);
                }
                else
                {
                    ConsoleManager.WriteLine(client.IPAddress + " Sent a Invalid Login");
                    msg2.SubMessage = "imst";
                    client.Broadcast(msg2);
                }

            }
            else
            {
                AuthMessageOut msg2 = new AuthMessageOut();
                msg2.SubMessage = "imst";
                client.Broadcast(msg2);
            }
        }
    }
}
