using System;
using System.Collections.Generic;
using System.Linq;
using VRageMath;
using Sandbox.ModAPI;
using Sandbox.Game;

using VRage.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace RadarBlock
{
	public class RadarProcess
	{
		private static List<RadarObjects> m_radarObjectList;
		private static bool m_init = false;
		private static bool m_LCDClear = false;
		public static bool passiveGPS;
        public static Random rnd = new Random();

        private static DateTime runTime;

		private static List<RadarOutputItem> m_radarOutputList = new List<RadarOutputItem>();
		internal static System.Timers.Timer clearTimer = new System.Timers.Timer();

		private static List<RadarSoundItem> m_radarSoundList = new List<RadarSoundItem>();

		static void ProcessServer(IMyEntity entity, string mode, RadarData item)
		{
            // Sanity check
            if (!(entity is IMyBeacon))
                return;

           // if (DateTime.Now - runTime <= TimeSpan.FromSeconds(RadarCore.radarSettings.CheckRadarSeconds)) return;
           // runTime = DateTime.Now;
            // This radar needs to be owned by the player or faction
            IMyBeacon beacon = (IMyBeacon)entity;
            if (!beacon.IsWorking || !beacon.IsFunctional || !beacon.Enabled) return;
            if (beacon.OwnerId == 0) return;
            List<IMyPlayer> owners = GetPlayerOwners(entity);

            if (owners.Count == 0) return;
            ProcessRadarItem(entity, mode, item, owners);
        }

		/// <summary>
		/// Process Radar Blocks
		/// </summary>
		public static void Process(IMyEntity entity, string mode, RadarData item)
		{
			if (MyAPIGateway.Session == null || RadarLogic.LastRadarUpdate == null)
				return;

			if (!m_init)
			{
				m_init = true;
				Initialize();
			}

			if (MyAPIGateway.Multiplayer.IsServer) ProcessServer(entity, mode, item);
		}
		
		public static void ProcessActiveRadars()
		{

            if (!m_init && RadarCore.isServer)
            {
                m_init = true;
                Initialize();
            }

            if (RadarCore.activeRadars.Count == 0)return;

			var copy = RadarCore.activeRadars.Keys.ToList();
			
			foreach(var rBlock in copy){

                IMyEntity blockEntity;
                MyAPIGateway.Entities.TryGetEntityById(rBlock.EntityId, out blockEntity);
                var radar = blockEntity as IMyBeacon;
				if(radar == null || blockEntity == null){

                   // RadarData data = RadarLogic.LoopRadarSync(rBlock.EntityId);
					RadarCore.activeRadars.Remove(rBlock);
					//RadarLogic.ChangeEmissive("Idle", radar, data);
					continue;
				}
				
				// Check if ship moves while active scanning (if settings approve)
				if(RadarCore.config.RequireStationaryWhileActiveScanning){
					
					var cubeBlock = blockEntity as IMyCubeBlock;
					if(cubeBlock == null)continue;
					var cubeGrid = cubeBlock.CubeGrid;
					if(cubeGrid == null)continue;

                    var vel = (Vector3D)cubeGrid.Physics.LinearVelocity;
                    var speed = vel.Length();
                    if (speed > 10)
                    {
                        RadarData data = RadarLogic.LoopRadarSync(rBlock.EntityId);
                        RadarLogic.ChangeEmissive("Idle", radar, data, true);
                        RadarCore.activeRadars.Remove(rBlock);

                        continue;
					}
				}
				
				// Need to stop processing active radar countdown if radius is set to passive
				if(radar.Radius <= RadarCore.radarSettings.modeSwitchRange){
					
					RadarCore.activeRadars.Remove(rBlock);

					continue;
				}
				
				// Check if radar is still enabled
				if(!radar.IsWorking){
					
					RadarCore.activeRadars.Remove(rBlock);
					continue;
				}
				
				if(RadarCore.activeRadars[rBlock] > 0){
					
					RadarCore.activeRadars[rBlock]--;
					RadarCore.detailInfo = RadarCore.GetStringBuilderText("ActiveCountdown", RadarCore.activeRadars[rBlock]);
					RadarCore.UpdateBlockDetails(radar as IMyTerminalBlock);
					//MyVisualScriptLogicProvider.ShowNotification("Time Left = " +RadarCore.activeRadars[rBlock].ToString(), 1000, "Green");
					
				}else{

                    if (rBlock.EmissiveMode != "Idle")
                    {
                        RadarLogic.ChangeEmissive("Idle", radar, rBlock, true);
                    }
                    List<IMyPlayer> ownerList = GetPlayerOwners(blockEntity);

                    if (RadarCore.isServer) ProcessRadarItem(blockEntity, "Active", rBlock, ownerList);
                    RadarCore.activeRadars.Remove(rBlock);
				}
			}
		}

        private static List<IMyPlayer> GetPlayerOwners(IMyEntity entity)
        {
            IMyBeacon beacon = (IMyBeacon)entity;
            if (beacon == null) return new List<IMyPlayer>();
            List<IMyPlayer> playerList = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(playerList);
            var beaconFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(beacon.OwnerId);
            List<IMyPlayer> owners = new List<IMyPlayer>();
            foreach (var player in playerList)
            {
                IMyFaction playerFaction = null;
                playerFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(player.IdentityId);

                if (playerFaction == null && beaconFaction == null && player.IdentityId == beacon.OwnerId)
                {
                    owners.Add(player);
                    break;
                }

                if (playerFaction != beaconFaction) continue;
                owners.Add(player);
            }

            return owners;
        }

		/*
		 * 100%-75% - Can detect massive objects
		 * 74%-55% - Can detect huge objects
		 * 54%-35% - Can detect large objects
		 * 34%-20% - Can detect moderate objects
		 * 19%-10% - Can detect small objects
		 * 0-9% - Can detect tiny objects
		 * Why do I care about min distance now that I think of it?  hmm.
		 */
		private static void Initialize()
		{
			runTime = DateTime.Now;

			m_radarObjectList = new List<RadarObjects>();
			/*
			AddScanItem("Massive", 0.75f, 1f, 500f);
			AddScanItem("Huge", 0.55f, 0.75f, 250f);
			AddScanItem("Large", 0.35f, 0.55f, 100f);
			AddScanItem("Medium", 0.20f, 0.35f, 50f);
			AddScanItem("Small", 0.10f, 0.20f, 25f);
			AddScanItem("Tiny", 0.01f, 0.10f, 0f);
			*/
			
			AddScanItem("Massive", 1f, 1f, 500f);
			AddScanItem("Huge", 1f, 1f, 250f);
			AddScanItem("Large", 1f, 1f, 100f);
			AddScanItem("Medium", 1f, 1f, 50f);
			AddScanItem("Small", 1f, 1f, 25f);
			AddScanItem("Tiny", 1f, 1f, 0f);

		}

		private static void AddScanItem(string name, float min, float max, float size)
		{
			RadarObjects p = new RadarObjects();
			p.SizeName = name;
			p.MinimumDistance = min;
			p.MaximumDistance = max;
			p.Size = size;
			m_radarObjectList.Add(p);
		}

        /// <summary>
        /// Process a radar block on a grid.  If it's the grid the player is flying, draw hud markers of entities the radar can spot
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        private static bool ProcessRadarItem(IMyEntity entity, string mode, RadarData item, List<IMyPlayer> ownerList)
		{

            IMyEntity parent = entity.GetTopMostParent();
			m_radarSoundList.RemoveAll(x => x.EntityId == parent.EntityId);

			if(item.ActiveRadar == true && mode == "Passive")
            {
				return false;
			}

            IMyBeacon beacon = (IMyBeacon)entity;
            Vector3D position = parent.GetPosition();
			double radius = (double)beacon.Radius;
            BoundingSphereD sphere = new BoundingSphereD(position, radius);
			List<IMyEntity> entities = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere);
			RadarFilter radarFilter = GetRadarEntityFilters(entity, item);
			string lcdoutput = "";
            

			foreach (IMyEntity foundEntity in entities)
            {
				// Projection or invalid object
				if (foundEntity.Physics == null)
					continue;

                // Waypoints and other things that are free of physics
                //if (!foundEntity.Physics.Enabled)
                //continue;
                

                // Ignore our own ship
                if (foundEntity == parent) continue;
                // if (player.Controller != null && player.Controller.ControlledEntity != null && player.Controller.ControlledEntity.Entity != null && foundEntity == player.Controller.ControlledEntity.Entity.GetTopMostParent())
                // continue;

                // Ignore our character entity
                // if (foundEntity.DisplayName == player.DisplayName)
                //continue;

                if (foundEntity is IMyVoxelMap ||
                    foundEntity is IMyCharacter ||
                    foundEntity is IMyCubeGrid)
                {
                    double distance = Vector3D.DistanceSquared(foundEntity.GetPosition(), position);
                    distance = Math.Sqrt(distance);

                    if (foundEntity is IMyVoxelMap)
                    {
                        lcdoutput = lcdoutput + AddRadarEntity(foundEntity, parent, radarFilter, distance, radius, mode, item, foundEntity.GetPosition(), beacon, ownerList);
                        continue;
                    }

                    IMyFaction entFaction = null;
                    var radarFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(beacon.OwnerId);
                    bool isFriendly = false;
                    IMyCubeGrid Grid = foundEntity as IMyCubeGrid;
                    if (Grid != null)
                    {
                        var owners = Grid.BigOwners;

                        foreach (var owner in owners)
                        {

                            if (owner == 0) continue;
                            entFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(owner);
                            break;
                        }

                        if (entFaction == radarFaction && entFaction != null && radarFaction != null)
                        {
                            isFriendly = true;
                        }
                    }

                    bool useOffSet = false;
                    int offSetDistanceMin = RadarCore.config.GpsOffSetDistanceMin;
                    int offSetDistanceMax = RadarCore.config.GpsOffSetDistanceMax;
                    if (offSetDistanceMax != 0 && foundEntity is IMyCubeGrid && !isFriendly) useOffSet = true;

                    if (useOffSet)
                    {

                        Vector3D offset = new Vector3D(0, 0, 0);
                        var randomDir = Vector3D.Normalize(MyUtils.GetRandomVector3D());
                        if (distance >= RadarCore.radarSettings.modeSwitchRange)
                        {
                            offset = randomDir * (double)rnd.Next(offSetDistanceMin, offSetDistanceMax) + foundEntity.GetPosition();
                        }
                        else
                        {
                            offset = randomDir * (double)rnd.Next(offSetDistanceMin, offSetDistanceMax) / 2 + foundEntity.GetPosition();
                        }

                        if (distance < radius)
                        {
                            double offsetDisance = RadarCore.MeasureDistance(offset, position);
                            lcdoutput = lcdoutput + AddRadarEntity(foundEntity, parent, radarFilter, offsetDisance, radius, mode, item, offset, beacon, ownerList);
                        }
                    }
                    else
                    {
                        if (distance < radius)
                        {
                            lcdoutput = lcdoutput + AddRadarEntity(foundEntity, parent, radarFilter, distance, radius, mode, item, foundEntity.GetPosition(), beacon, ownerList);
                        }
                    }
                }
                
			}

            if (radarFilter.OutputLCDName != null && !string.IsNullOrEmpty(radarFilter.OutputLCDName))
			{

				LCDManager.Add(parent, radarFilter.OutputLCDName, lcdoutput, false);

				RadarOutputItem outputItem = new RadarOutputItem();
				outputItem.Parent = parent;
				outputItem.Name = radarFilter.OutputLCDName;
				m_radarOutputList.Add(outputItem);

			}
			

			return true;
		}

		/*
		 *  RULES:
		 *  Objects > 66% of radar distance unknown object
		 *  Objects > 33% of radar distance size known (AABB?)
		 *  Objects < 33% of radar distance type known
		 */
		/// <summary>
		/// Add hud markers on a found entity
		/// </summary>
		/// <param name="entity"></param>
		/// <param name="distanceSquared"></param>
		/// <param name="radius"></param>
		private static string AddRadarEntity(IMyEntity entity, IMyEntity parent, RadarFilter radarFilter, double distance, double radius, string mode, RadarData item, Vector3D entPos, IMyBeacon radar, List<IMyPlayer> ownerList)
		{
			string output = "";

			try {

                if (entity == null || parent == null || radarFilter == null || item == null || radar == null) return "";
                bool showHud = true;
				
				bool powered = true; // start as true because we want to see asteroids if not specified otherise
				bool sonarmode = false; 
				bool functional = false;
				bool broadcasting = false;
                bool isFriendly = false;
				if (radarFilter.SonarMode)
					sonarmode=true; // TODO: increase detection range past transmission range, but only return noisy blocks in an atmosphere. (not my todo: Thraxus)

				RadarObjectTypes radarType = GetEntityObjectType(entity);
				RadarObjects radarItem = GetEntityObjectSize(entity);

                if (radarItem == null) return "";
				
				float maxdist = radarItem.MaximumDistance;
				if (sonarmode) // TODO: this will not work yet (not my todo: Thraxus)
					maxdist =maxdist * 100.0f;


				// Radar can't detect item of this size at this range
				if (distance > radius * maxdist)
					return "";

				if (radarFilter.MinimumDistance > 0f) {
					// The item is too close
					if (distance < radarFilter.MinimumDistance) {
						return "";
					}
				}
                
                var radarFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(radar.OwnerId);
                // Do not display asteroids
                if (radarType == RadarObjectTypes.Asteroid && radarFilter.NoAsteroids)
					return "";
				
				//if (radarType == RadarObjectTypes.Asteroid)
					//MyVisualScriptLogicProvider.ShowNotification("Roid Found", 3000, "Green");

				// Do not display ships
				//if (radarType == RadarObjectTypes.Ship && radarFilter.NoShips)
					//return "";

				// Do not display stations
				//if (radarType == RadarObjectTypes.Station && radarFilter.NoStations)
					//return "";

                // Do not display players
                /*if (radarType == RadarObjectTypes.Astronaut && radarFilter.NoCharacters)
					return "";*/
                
                if (radarType == RadarObjectTypes.Astronaut)
                {
                    var chara = entity as IMyPlayer;
                    if (chara == null) return "";
                    var entityFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(chara.IdentityId);
                    
                    if (radarFilter.NoFriendly)
                    {
                        if (entityFaction == radarFaction && entityFaction != null && radarFaction != null && radar.OwnerId != chara.IdentityId) return "";
                    }
                    
                }
                    
                 double objectsize = (entity.PositionComp.LocalAABB.Max - entity.PositionComp.LocalAABB.Min).Volume;
                
				// Do not display debris (but do display lifeforms regardless of size)
				if ((radarType != RadarObjectTypes.Astronaut) && (objectsize < Math.Abs(radarFilter.MinimumSize + 0.01)))
					return "";

				string description = "Unknown";
				
				broadcasting=false;
				
				if (radarType == RadarObjectTypes.Ship || radarType == RadarObjectTypes.Station)
				{
					
					// Get faction of scanned entity
					IMyFaction entFaction = null;
                    long entOwner = 0;
					IMyCubeGrid Grid = entity as IMyCubeGrid;
					if(Grid == null)return "";
					var owners = Grid.BigOwners;
					
					foreach(var owner in owners){
						
						if(owner == 0)continue;
						entFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(owner);
                        entOwner = owner;
						break;
					}

                    if(entFaction != null)
                    {
                        if (entFaction == radarFaction)
                        {
                            isFriendly = true;
                        }

                        // Do not display friendlies
                        if (radarFilter.NoFriendly)
                        {

                            if (isFriendly) return "";
                        }

                        // Do not get npc owned entities
                        if (radarFilter.NoNPC)
                        {

                            if (entFaction.IsEveryoneNpc())
                            {

                                return "";
                            }
                        }
                    }
                    else
                    {
                        if (entOwner == radar.OwnerId && radarFilter.NoFriendly) return "";
                    }
 
                    List<IMyTerminalBlock> Blocks = new List<IMyTerminalBlock>();
                    MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(Grid).GetBlocksOfType(Blocks, x => x.IsFunctional);
                    broadcasting = Blocks.Any(x => (x as IMyBeacon)?.IsWorking == true && x.BlockDefinition.SubtypeName.Contains("Radar") && RadarCore.config.AllEnabledRadarsVisible); //|| (x as IMyRadioAntenna)?.IsWorking == true);
                    //powered = Blocks.Any(x => (x as IMyReactor)?.IsWorking == true || (x as IMyBatteryBlock)?.CurrentStoredPower > 0f);

                    float totalPwr = 0;
                    if (RadarCore.config.UseMaxOutput)
                    {

                        foreach (var block in Blocks)
                        {

                            var bklPower = block as IMyPowerProducer;
                            if (bklPower == null) continue;

                            if (!block.IsWorking) continue;

                            totalPwr += bklPower.MaxOutput;
                        }
                       // MyVisualScriptLogicProvider.ShowNotification("Grid = " + Grid.CustomName + " Total Pwr = " + totalPwr.ToString(), 5000, "Red");

                        if (mode == "Active")
                        {

                            if (totalPwr < RadarCore.config.ActiveMAXScanGridsMWthreshold)
                            {
                                if (!broadcasting) return "";
                            }
                        }

                        if (mode == "Passive")
                        {

                            if (totalPwr < RadarCore.config.PassiveMAXScanGridsMWthreshold)
                            {
                                if (!broadcasting) return "";
                            }
                        }

                    }
                    else
                    {

                        foreach (var block in Blocks)
                        {

                            var bklPower = block as IMyPowerProducer;
                            if (bklPower == null) continue;

                            if (!block.IsWorking) continue;

                            totalPwr += bklPower.CurrentOutput;
                        }

                        if (mode == "Active")
                        {

                            if (totalPwr < RadarCore.config.ActiveCURRENTScanGridsMWthreshold)
                            {
                                if (!broadcasting) return "";
                            }
                        }

                        if (mode == "Passive")
                        {

                            if (totalPwr < RadarCore.config.PassiveCURRENTScanGridsMWthreshold)
                            {
                                if (!broadcasting) return "";
                            }
                        }
                    }
                }

                if (broadcasting)
					description=entity.DisplayName;
				else if (distance < radius * maxdist * 0.33)
				{
					if (radarType == RadarObjectTypes.Astronaut)
					{
						
						if (RadarCore.MeasureDistance(entity.GetPosition(), parent.GetPosition()) <= RadarCore.config.CharacterDetectionRange)
							if (entity.DisplayName.Length > 1)
								description = "Humanoid";
							else
								description = "Lifeform";
					}
					else
					{
						description = string.Format("{0} {1} {2}", radarItem.SizeName, radarType.ToString(), Math.Abs(entity.EntityId % 10000));
                        //MyVisualScriptLogicProvider.ShowNotification("Used .33 Scan", 5000, "Red");
                    }
				} else if (distance < radius * maxdist * 0.66) {
					description = string.Format("{0} unknown {1}", radarItem.SizeName, Math.Abs(entity.EntityId % 10000));
                   // MyVisualScriptLogicProvider.ShowNotification("Used .66 Scan", 5000, "Red");

                }
				else if (radarType == RadarObjectTypes.Asteroid && objectsize > 2022 && objectsize < 4098) // hardcoded boulder size. Do not show these, on planets it's spammy and in space it just brings up subvoxels
					description = "Boulder";

				Vector3D position = entity.GetPosition();
                
                foreach(var player in ownerList)
                {
                    // Don't show friendly players
                    if (entity.DisplayName == player.DisplayName)
                        continue;

                    // Player needs to be controlling something to see hud marks
                    if (player.Controller == null || player.Controller.ControlledEntity == null || player.Controller.ControlledEntity.Entity == null)
                        continue;

                    // Player needs to be controlling the ship that the radar is on to see hud marks (unless specified)
                    if ((parent != player.Controller.ControlledEntity.Entity.GetTopMostParent()))
                        continue;

                    // Show hud marks
                    if (mode == "Passive")
                    {
                        passiveGPS = true;
                    }
                    else
                    {
                        passiveGPS = false;
                    }
                    HudMarkManager.SendLocalGps(entPos, description, passiveGPS, player);
                }

				// output to LCD
				if (!string.IsNullOrEmpty(radarFilter.OutputLCDName)) {

					output = string.Format("GPS:{0}:{1}:{2}:{3}:Size {4}:Dist {5}:\n", description, Math.Round(entPos.X), Math.Round(entPos.Y), Math.Round(entPos.Z), description.StartsWith("Unk") ? (-1) : (Math.Round(objectsize)), Math.Round(distance));//entity.PositionComp.LocalAABB.Max - entity.PositionComp.LocalAABB.Min);
				}

				/*if (radarFilter.TriggerSoundName != null && radarFilter.TriggerSoundName != "") {
					if (m_radarSoundList.Where(x => x.EntityId == parent.EntityId).Count() < 1) {
						m_radarSoundList.Add(new RadarSoundItem() { EntityId = parent.EntityId });
						SoundBlockManager.PlaySound(parent, radarFilter.TriggerSoundName);
					}
				}*/
			} catch (Exception ex) {
				Logging.Instance.WriteLine(string.Format("AddRadarEntity Error: {0}", ex.ToString()));
				return string.Format("AddRadarEntity Error: {0}", ex.ToString());
			}
			return output;
		}

		/// <summary>
		/// Clear the LCD screen we are outputting to
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		static void ClearLCDTimer(object sender, System.Timers.ElapsedEventArgs e)
		{
			if (MyAPIGateway.Session == null)
				return;

			MyAPIGateway.Utilities.InvokeOnGameThread(new Action(() => {
			                                                     	m_LCDClear = false;
			                                                     	foreach (var item in m_radarOutputList) {
			                                                     		LCDManager.Clear(item.Parent, item.Name);
			                                                     		//LCDManager.Clear(m_LCDParent, m_LCDName);
			                                                     	}
			                                                     	m_radarOutputList.Clear();
			                                                     }));
		}

		/// <summary>
		/// Extract any text filters the player has put on the radar
		/// </summary>
		/// <param name="radarEntity"></param>
		/// <returns></returns>
		private static RadarFilter GetRadarEntityFilters(IMyEntity radarEntity, RadarData item)
		{
			RadarFilter filter = new RadarFilter();
			IMyBeacon radarBeacon = (IMyBeacon)radarEntity;

			filter.NoAsteroids = item.filterRoids;
			filter.NoNPC = item.filterNPC;
			filter.NoFriendly = item.filterFriendly;
			//filter.NoCharacters = true;
			filter.MinimumSize = 2f;
			filter.MinimumDistance = 1f;
            filter.OutputLCDName = item.SelectedLCD;
            filter.PassengerHud = true;
            /*String customdata = radarBeacon.CustomData;
			if (customdata.Length<2) // empty string, so let's get some defaults in there first
			{
				radarBeacon.CustomData="MinimumDistance:1\nMinimumSize:2\nYesShips\nYesStations\nYesHud\nYesUnpowered\nYesLifeforms\n\nOutputLCD:RadarLCD\n";
			}
			
			customdata = customdata.ToLowerInvariant();
			customdata.Replace("minsize:","minimumsize:");
			customdata.Replace("mindist:","minimumdistance:");
			customdata.Replace("mindistance:","minimumdistance:");
			customdata.Replace("output:","outputlcd:");

			if (customdata.Contains("noasteroids")||customdata.Contains("noroids"))
				filter.NoAsteroids = true;
			if (customdata.Contains("noships"))
				filter.NoShips = true;
			if (customdata.Contains("nostations"))
				filter.NoStations = true;
			if (customdata.Contains("nocharacters")||customdata.Contains("nolifeforms"))
				filter.NoCharacters = true;
			if (customdata.Contains("nohud"))
				filter.NoHud = true;
			if (customdata.Contains("passengerhud"))
				filter.PassengerHud = true;
			if (customdata.Contains("onlypowered")||customdata.Contains("nounpowered"))
				filter.OnlyPowered = true;
			if (customdata.Contains("sonarmode"))
				filter.SonarMode = true;

			// I should be using Regex, but don't really have time to profile.  I don't want to be stuck with a weird
			// processing issue as sometimes regex can be fickle
			if (customdata.Contains("minimumsize:")) {
				int pos = customdata.IndexOf("minimumsize:") + 12;
				string minString = customdata.Substring(pos, customdata.Length - pos);
				string corrected = "";
				for (int r = 0; r < minString.Length; r++) {
					int test = 0;
					string numTest = "";
					numTest += minString[r];
					if (!int.TryParse(numTest, out test))
						break;

					corrected += minString[r];
				}

				float minimumSize = 2f;
				filter.MinimumSize = float.TryParse(corrected, out minimumSize) ? minimumSize : 2f; // by default skip size 0~1 objects or we'll get all sorts of spam
			}
			else
				filter.MinimumSize = 2f; // by default skip size 0~1 objects or we'll get all sorts of spam
			if (customdata.Contains("minimumdistance:")) {
				int pos = customdata.IndexOf("minimumdistance:") + 16;
				string minString = customdata.Substring(pos, customdata.Length - pos);
				string corrected = "";
				for (int r = 0; r < minString.Length; r++) {
					int test = 0;
					string numTest = "";
					numTest += minString[r];
					if (!int.TryParse(numTest, out test))
						break;

					corrected += minString[r];
				}

				float minimumDistance = 1f; // default minimum distance to 1, so that we don't scan our own grid. But leave that as an option in case we need to see size.
				filter.MinimumDistance = float.TryParse(corrected, out minimumDistance) ? minimumDistance : 1f;
			}
			if (customdata.Contains("outputlcd:")) {
				int pos = customdata.IndexOf("outputlcd:") + 10;
				string minString = customdata.Substring(pos, customdata.Length - pos);
				string corrected = "";
				for (int r = 0; r < minString.Length; r++) {
					if (minString[r] == ',' || minString[r] == ':' || minString[r] == ')' || minString[r] == ']' || minString[r] == '\n')
						break;

					corrected += minString[r];
				}

				filter.OutputLCDName = corrected;
				
			}

			if (customdata.Contains("triggersoundname:")) {
				int pos = customdata.IndexOf("triggersoundname:") + 17;
				string minString = customdata.Substring(pos, customdata.Length - pos);
				string corrected = "";
				for (int r = 0; r < minString.Length; r++) {
					if (minString[r] == ',' || minString[r] == ':' || minString[r] == ')' || minString[r] == ']' || minString[r] == '\n')
						break;

					corrected += minString[r];
				}

				filter.TriggerSoundName = corrected;
				Logging.Instance.WriteLine(string.Format("Sound Name: {0}", filter.TriggerSoundName));*/

            return filter;
		}

		
		/// <summary>
		/// Get the entity type
		/// </summary>
		/// <param name="entity"></param>
		/// <returns></returns>
		private static RadarObjectTypes GetEntityObjectType(IMyEntity entity)
		{
			if (entity is IMyVoxelMap)
				return RadarObjectTypes.Asteroid;

			if (entity is IMyCharacter)
				return RadarObjectTypes.Astronaut;

			if (entity is IMyCubeGrid && ((IMyCubeGrid)entity).IsStatic)
				return RadarObjectTypes.Station;

			return RadarObjectTypes.Ship;
		}

		/// <summary>
		/// Get the entity size
		/// </summary>
		/// <param name="entity"></param>
		/// <returns></returns>
		private static RadarObjects GetEntityObjectSize(IMyEntity entity)
		{

			double entitySize = entity.PositionComp.WorldAABB.Size.AbsMax();

			foreach (RadarObjects item in m_radarObjectList) {
				if (entitySize >= item.Size) {
					return item;
				}
			}

			return null;
		}
		
		/*public static RadarData LoopClientRadarSync(long entityId){
			
			try{
	
				if(t_radarList.Count == 0)return null;
				foreach(var item in t_radarList){
				
					if(item.EntityId == entityId){
					
						return item;
					}
				}
			
				return null;
				
			}catch(Exception exc){

				return null;
			}
		}*/
	}

	public class RadarObjects
	{
		public float MinimumDistance { get; set; }
		public float MaximumDistance { get; set; }
		public string SizeName { get; set; }
		public float Size { get; set; }
	}

	public class RadarFilter
	{
		public long BlockId {get; set;}
		public bool NoNPC { get; set; }
		public bool NoFriendly {get; set;}
		public bool NoAsteroids { get; set; }
		public bool NoShips { get; set; }
		public bool NoStations { get; set; }
		public bool NoCharacters { get; set; }
		public bool NoHud { get; set; }
		public bool PassengerHud { get; set; }
		public bool OnlyPowered { get; set; }
		public bool SonarMode { get; set; }
		public float MinimumSize { get; set; }
		public float MinimumDistance { get; set; }
		public string OutputLCDName { get; set; }
		public string TriggerSoundName { get; set; }
	}

	public enum RadarObjectTypes
	{
		Astronaut,
		Asteroid,
		Station,
		Ship
	}

	public class RadarOutputItem
	{
		public IMyEntity Parent { get; set; }
		public string Name { get; set; }
	}

	public class RadarSoundItem
	{
		public long EntityId { get; set; }
	}

}