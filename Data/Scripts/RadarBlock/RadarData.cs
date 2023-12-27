using System;
using ProtoBuf;

namespace RadarBlock{

    [ProtoContract]
    public class RadarData{

        [ProtoMember(1)]
        public long EntityId = 0;

        [ProtoMember(2)]
        public string EmissiveMode = "";

        [ProtoMember(3)]
        public bool ActiveRadar;

        [ProtoMember(4)]
        public bool filterRoids;

        [ProtoMember(5)]
        public bool filterFriendly;

        [ProtoMember(6)]
        public bool filterNPC;

        [ProtoMember(7)]
        public string SelectedLCD = "";

        //[ProtoMember(8)]
        //public DateTime LastUpdate = DateTime.Now;

    }

    /*[ProtoContract]
    public struct RadarComms
    {
        [ProtoMember(1)]
        public bool myBoolFromServer;

        [ProtoMember(2)]
        public string serverResponse;

        [ProtoMember(3)]
        public string myRequestToServer;

        [ProtoMember(4)]
        public long PlayerId;

        [ProtoMember(5)]
        public long EntityId;

        public RadarComms(bool serverBool, string fromServerMessage, string toServerMessage, long playerID, long entityID)
        {
            myBoolFromServer = serverBool;
            serverResponse = fromServerMessage;
            myRequestToServer = toServerMessage;
            PlayerId = playerID;
            EntityId = entityID;
        }
    }*/

}