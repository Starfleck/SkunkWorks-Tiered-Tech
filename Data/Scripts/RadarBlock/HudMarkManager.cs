using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using Sandbox.Game;

using VRageMath;
using VRage;
using VRage.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace RadarBlock
{
	public class HudMarkManager
	{
		private static List<MyTuple<IMyGps, DateTime>> m_hudMarkList = new List<MyTuple<IMyGps, DateTime>>();
		private static DateTime m_lastUpdate = DateTime.Now;
        private static bool m_initialized = false;
       // public static Random rnd = new Random();

		/// <summary>
		/// Add a hud mark to the player's screen.  Using GPS here causes issue (sometimes they save??).  I need
		/// to add unsaved hudmarkers to the game's source.  Would allow for colours too if I do that.
		/// </summary>
		/// <param name="position"></param>
		/// <param name="description"></param>
		public static void AddLocalGps(string message)
		{
            // Does name need to be unique?  Let's assume yes.  ED: no name is what shows up on hud
            var split = message.Split('\n');
            Vector3D pos = new Vector3D(0, 0, 0);
            string description = split[2];
            bool passiveGps = false;

            Vector3D.TryParse(split[1], out pos);
            bool.TryParse(split[3], out passiveGps);
            if (pos == new Vector3D(0, 0, 0)) return;

            RadarProcess.passiveGPS = passiveGps;
            var gps = MyAPIGateway.Session.GPS.Create(description, "BLIP: " + description, pos, true, true);
            m_hudMarkList.Add(new MyTuple<IMyGps, DateTime>(gps, DateTime.Now));
			MyAPIGateway.Session.GPS.AddLocalGps(gps);

        }

        public static void SendLocalGps(Vector3D position, string description, bool passiveGPS, IMyPlayer player)
        {
            string message = "SendLocalGPS" + "\n";
            message += position.ToString() + "\n";
            message += description + "\n";
            message += passiveGPS.ToString() + "\n";

            Comms.SendMessageToPlayer(message, player);
        }

		/// <summary>
		/// Process our hud markers.  Remove them after a certian time. (5 seconds right now)
		/// </summary>
		public static void Process(bool force)
		{

            if (MyAPIGateway.Session == null)
                return;

            if (!m_initialized && !(MyAPIGateway.Utilities.IsDedicated && MyAPIGateway.Multiplayer.IsServer) && MyAPIGateway.Session.Player != null)
            {
                m_initialized = true;
                RemoveSavedGPS();               
            }

            if (m_hudMarkList.Count == 0) return;
			
			if(force){
				
				for (int r = m_hudMarkList.Count - 1; r > -1; r--)
				{
					MyTuple<IMyGps, DateTime> items = m_hudMarkList[r];
					MyAPIGateway.Session.GPS.RemoveLocalGps(items.Item1);
					m_hudMarkList.RemoveAt(r);
				}
				return;
			}

			for (int r = m_hudMarkList.Count - 1; r > -1; r--)
			{
				MyTuple<IMyGps, DateTime> items = m_hudMarkList[r];
				if (DateTime.Now - items.Item2 > TimeSpan.FromSeconds(RadarCore.radarSettings.ClearHUDListEverySeconds))
				{
					MyAPIGateway.Session.GPS.RemoveLocalGps(items.Item1);
					m_hudMarkList.RemoveAt(r);
                   // MyVisualScriptLogicProvider.ShowNotification("GPS Removed", 5000, "Green");
                }
			}
        }

        private static void RemoveSavedGPS()
        {
            if (MyAPIGateway.Session != null)
                return;

            //if (MyAPIGateway.Utilities.IsDedicated && MyAPIGateway.Multiplayer.IsServer)
                //return;

            try
            {
                List<IMyGps> list = new List<IMyGps>();
                MyAPIGateway.Session.GPS.GetGpsList(MyAPIGateway.Session.Player.IdentityId, list);

                foreach (var item in list)
                {
                    if (item.Description.StartsWith("BLIP:"))
                    {
                        MyAPIGateway.Session.GPS.RemoveGps(MyAPIGateway.Session.Player.IdentityId, item);
                    }
                }
            }
            catch(Exception ex)
            {
                Logging.Instance.WriteLine(string.Format("RemoveSavedGPS(): {0}", ex.ToString()));
            }
        }
    }
}
