using SSX3_Server.EAServer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace SSX3_Server.EAClient.Messages
{
    public class QuikMessageIn : EAMessage
    {
        public override string MessageType { get { return "quik"; } }

        public string KIND;
        public bool Ranked;
        public bool Race;
        public bool BigAir;
        public bool SuperPipe;
        public bool Slopestyle;//

        public string FromPlayer;
        public DateTime QueuedAt = DateTime.Now;

        public static List<QuikMessageIn> quikMessageIn = new List<QuikMessageIn>();
        private static readonly object _quikLock = new object();
        private static Thread _sweeper = null;

        // Event pools for quick-play: (quik flag, +ses P2, track IDs).
        // Tracks per event from HighscoreDatabase.RankToHighscore (track,event
        // pairs the server scores). P2 0=Race / 1=SlopeStyle are certain; 2/3
        // follow RankToHighscore (SuperPipe=2, BigAir=3), which contradicts
        // EventIDToName — unverified against a real client, see lane report.
        private static readonly (string Flag, string P2, string[] Tracks)[] _events =
        {
            ("Race", "0", new[] { "0", "1", "2", "3", "4", "14", "15", "16" }),
            ("Slopestyle", "1", new[] { "5", "6", "7", "14", "15", "16" }),
            ("SuperPipe", "2", new[] { "11", "12", "13" }),
            ("BigAir", "3", new[] { "8", "9", "10" }),
        };

        public override void AssignValues()
        {
            KIND = stringDatas[0].Value;

            if (stringDatas.Count > 1)
            {
                string[] LineSplit = stringDatas[1].Value.Split(",");

                if (LineSplit[0] == "1")
                {
                    Ranked = true;
                }

                if (LineSplit[1] == "1")
                {
                    Race = true;
                }

                if (LineSplit[2] == "1")
                {
                    BigAir = true;
                }

                if (LineSplit[3] == "1")
                {
                    SuperPipe = true;
                }

                if (LineSplit[4] == "1")
                {
                    Slopestyle = true;
                }

            }
        }

        public override void AssignValuesToString()
        {
            AddStringData("KIND", KIND);

            if (KIND != "*")
            {
                string TempString = "";

                if (Ranked)
                {
                    TempString += "1";
                }

                TempString += ",";

                if (Race)
                {
                    TempString += "1";
                }

                TempString += ",";

                if (BigAir)
                {
                    TempString += "1";
                }

                TempString += ",";

                if (SuperPipe)
                {
                    TempString += "1";
                }

                TempString += ",";

                if (Slopestyle)
                {
                    TempString += "1";
                }


                AddStringData("QMFP", TempString);
            }

        }

        public override void ProcessCommand(EAClientManager client, EAServerRoom room = null)
        {
            FromPlayer = client.LoadedPersona.Name;
            client.Broadcast(this);
            if (KIND == "DeathRace")
            {
                lock (_quikLock)
                {
                    EnsureSweeper();
                    // Re-queue replaces any older entry from the same player.
                    for (int i = quikMessageIn.Count - 1; i >= 0; i--)
                    {
                        if (quikMessageIn[i].FromPlayer == FromPlayer)
                        {
                            quikMessageIn.RemoveAt(i);
                        }
                    }
                    QueuedAt = DateTime.Now;
                    quikMessageIn.Add(this);
                    TryPair(client, this);
                }
            }
            else if (KIND == "*")
            {
                lock (_quikLock)
                {
                    for (int i = 0; i < quikMessageIn.Count; i++)
                    {
                        if (quikMessageIn[i].FromPlayer == FromPlayer)
                        {
                            quikMessageIn.RemoveAt(i);

                            break;
                        }
                    }
                    //stop quick match search
                }
            }
        }

        private bool FlagFor(string flag)
        {
            return flag switch
            {
                "Race" => Race,
                "Slopestyle" => Slopestyle,
                "SuperPipe" => SuperPipe,
                "BigAir" => BigAir,
                _ => false,
            };
        }

        // Pair two queued players with the same ranked flag, same client build
        // and at least one shared event. First shared event (canonical order)
        // wins; the track is random within that event's pool. Earlier queue
        // entry hosts. Caller must hold _quikLock.
        private static void TryPair(EAClientManager client, QuikMessageIn mine)
        {
            for (int i = 0; i < quikMessageIn.Count; i++)
            {
                var other = quikMessageIn[i];
                if (other == mine || other.FromPlayer == mine.FromPlayer)
                {
                    continue;
                }
                if (other.Ranked != mine.Ranked)
                {
                    continue;
                }
                var otherClient = EAServerManager.Instance.GetUserPersona(other.FromPlayer);
                if (otherClient == null)
                {
                    continue; // stale entry; the sweeper prunes it
                }
                if (otherClient.VERS != client.VERS)
                {
                    continue; // never mix builds (60/120 would desync)
                }
                string eventP2 = null;
                string[] pool = null;
                foreach (var ev in _events)
                {
                    if (mine.FlagFor(ev.Flag) && other.FlagFor(ev.Flag))
                    {
                        eventP2 = ev.P2;
                        pool = ev.Tracks;
                        break;
                    }
                }
                if (pool == null)
                {
                    continue; // no shared event
                }
                string track = pool[(new Random()).Next(pool.Length)];
                // Earlier entry hosts.
                var hostEntry = other.QueuedAt <= mine.QueuedAt ? other : mine;
                var hostClient = hostEntry == other ? otherClient : client;
                var oppoClient = hostEntry == other ? client : otherClient;
                quikMessageIn.Remove(other);
                quikMessageIn.Remove(mine);
                ConsoleManager.WriteLine("QuickPlay: pairing " + hostClient.LoadedPersona.Name
                    + " with " + oppoClient.LoadedPersona.Name
                    + " on track " + track + " event " + eventP2);
                ChalMessageIn.CreateSession(hostClient, oppoClient, track, eventP2,
                    mine.Ranked ? "1" : "0");
                return;
            }
        }

        private static void EnsureSweeper()
        {
            if (_sweeper == null)
            {
                _sweeper = new Thread(SweepLoop);
                _sweeper.IsBackground = true;
                _sweeper.Start();
            }
        }

        // Dequeue waiters past QuickPlayWaitSeconds and fire the no-opponent
        // hook (stub: log line + config value; the bot path consumes it later).
        // Also prunes entries whose player disconnected.
        private static void SweepLoop()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(5000);
                    int wait = EAServerManager.Instance.config.QuickPlayWaitSeconds;
                    string hook = EAServerManager.Instance.config.QuickPlayNoOpponentHook;
                    lock (_quikLock)
                    {
                        for (int i = quikMessageIn.Count - 1; i >= 0; i--)
                        {
                            var entry = quikMessageIn[i];
                            var entryClient = EAServerManager.Instance.GetUserPersona(entry.FromPlayer);
                            if (entryClient == null)
                            {
                                quikMessageIn.RemoveAt(i);
                                continue;
                            }
                            if ((DateTime.Now - entry.QueuedAt).TotalSeconds >= wait)
                            {
                                quikMessageIn.RemoveAt(i);
                                ConsoleManager.WriteLine("QuickPlay: no opponent for "
                                    + entry.FromPlayer + " after " + wait
                                    + "s (hook=" + hook + ")");
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    ConsoleManager.WriteLine("QuickPlay sweeper: " + e.Message);
                }
            }
        }
    }
}
