using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using Sandbox.Game;
using VRage.Game.ModAPI;

namespace RadarBlock
{
    public static class Comms
    {
        public static void SendMessageToPlayer(string message, IMyPlayer player)
        {
            var sendData = MyAPIGateway.Utilities.SerializeToBinary(message);
            MyAPIGateway.Multiplayer.SendMessageTo(4110, sendData, player.SteamUserId);
        }

        public static void SendSettingsToPlayer(Settings config, IMyPlayer player)
        {
            var sendData = MyAPIGateway.Utilities.SerializeToBinary(config);
            MyAPIGateway.Multiplayer.SendMessageTo(4111, sendData, player.SteamUserId);
        }

        public static void SendToOtherPlayers(string message)
        {

            List<IMyPlayer> playerList = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(playerList);

            foreach (var player in playerList)
            {
                var sendData = MyAPIGateway.Utilities.SerializeToBinary(message);
                MyAPIGateway.Multiplayer.SendMessageTo(4110, sendData, player.SteamUserId);
            }

        }

        public static void SendToOtherPlayers(RadarData radarData)
        {

            List<IMyPlayer> playerList = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(playerList);

            foreach (var player in playerList)
            {
                var sendData = MyAPIGateway.Utilities.SerializeToBinary(radarData);
                MyAPIGateway.Multiplayer.SendMessageTo(4111, sendData, player.SteamUserId);
            }

        }

        public static void SendRadarDataToServer(RadarData data)
        {
            var sendData = MyAPIGateway.Utilities.SerializeToBinary(data);
            MyAPIGateway.Multiplayer.SendMessageToServer(4111, sendData);
        }

        public static void SendMessageToServer(string message)
        {

            var sendData = MyAPIGateway.Utilities.SerializeToBinary(message);
            MyAPIGateway.Multiplayer.SendMessageToServer(4110, sendData);
        }

        public static void SendMessageToServer(string message, long playerId)
        {
            string newMessage = "";
            if (message == "SyncConfig")
            {
                newMessage = message + "\n" + playerId.ToString();
            }

            var sendData = MyAPIGateway.Utilities.SerializeToBinary(newMessage);
            MyAPIGateway.Multiplayer.SendMessageToServer(4110, sendData);
        }

        public static void ObjectMessageHandler(byte[] data)
        {

            try
            {
                var radarData = MyAPIGateway.Utilities.SerializeFromBinary<RadarData>(data);

                if (radarData != null)
                {
                    if (RadarCore.isServer)
                    {
                        RadarCore.SyncLCD(radarData);
                    }
                    else
                    {
                        RadarLogic.AddRadarToClient(radarData);
                    }
                    return;
                }
            }
            catch (Exception exc)
            {

            }

            try
            {
                var config = MyAPIGateway.Utilities.SerializeFromBinary<Settings>(data);

                if (config != null)
                {
                    if (RadarCore.isServer) return;
                    else RadarCore.SyncConfig(config);
                    return;
                }
            }
            catch (Exception exc)
            {

            } 
        }

        public static void StringMessageHandler(byte[] data)
        {
            try
            {
                var receivedData = MyAPIGateway.Utilities.SerializeFromBinary<string>(data);

                if (receivedData != null)
                {
                    if (receivedData.Contains("UpdateEmissive"))
                    {
                        RadarCore.SyncEmissives(receivedData);
                    }

                    if (receivedData.Contains("SendLocalGPS"))
                    {
                        HudMarkManager.AddLocalGps(receivedData);
                    }

                    if (receivedData.Contains("AddActive"))
                    {
                        RadarCore.SyncActiveRadar(receivedData);
                    }

                    if (RadarCore.isServer)
                    {
                        if (receivedData.Contains("FilterToServer"))
                        {
                            RadarCore.SyncFilters(receivedData);
                        }

                        /*if (receivedData.Contains("SetSelectedLCD"))
                        {
                            RadarCore.SyncLCD(receivedData);
                        }*/

                        if (receivedData.Contains("SyncConfig"))
                        {
                            RadarCore.ServerGetConfig(receivedData);
                        }
                    }

                    return;
                }
            }
            catch (Exception exc)
            {

            }   
        }
    }
}
