using Sandbox.Game;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.Components;
using VRage.Game.ModAPI;

namespace NpcFactionRep
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class Session : MySessionComponentBase
    {
        // Start Editable Variables
        private const string ChatCommand = "/factionrepreset";
        private string[] NpcFactionTags = { "BTFN", "MONK", "TEST" };
        private int AllowPeaceAmt = 500;
        private int DeclareWarAmt = -500;
        // End Editable Variables


        private Dictionary<string, long> NpcFactionPairs = new Dictionary<string, long>();
        private Dictionary<long, IMyFaction> allFactions = new Dictionary<long, IMyFaction>();
        private bool isServer;
        private int ticks;

        public override void BeforeStart()
        {
            isServer = MyAPIGateway.Session.IsServer;
            MyAPIGateway.Multiplayer.RegisterMessageHandler(2210, MessageHandler);
            MyAPIGateway.Utilities.MessageEntered += ChatCommands;
            if (isServer)
            {
                MyAPIGateway.Session.Factions.FactionStateChanged += FactionChange;
                var factions = MyAPIGateway.Session.Factions.Factions;
                foreach (var faction in factions.Values)
                {
                    if (NpcFactionTags.Contains(faction.Tag))
                    {
                        NpcFactionPairs.Add(faction.Tag, faction.FactionId);
                    }
                }
            }
        }

        public override void UpdateBeforeSimulation()
        {
            if (!isServer) return;
            ticks++;

            if (ticks % 60 != 0) return;
            ticks = 0;

            CheckReputation();
            //CheckAllMembers();
        }

        private void CheckAllMembers()
        {
            IMyFaction faction = MyAPIGateway.Session.Factions.TryGetFactionByTag("CCC");
            IMyFaction npc = MyAPIGateway.Session.Factions.TryGetFactionByTag("TEST");
            if (faction == null || npc == null) return;
            var members = faction.Members;

            foreach (var member in members.Keys)
            {
                MyAPIGateway.Utilities.ShowNotification($"Rep = {MyAPIGateway.Session.Factions.GetReputationBetweenPlayerAndFaction(member, npc.FactionId)}", 500);
            }
        }

        private void CheckReputation()
        {
            allFactions.Clear();
            allFactions = MyAPIGateway.Session.Factions.Factions;

            foreach(var faction in allFactions.Values)
            {
                if (faction.IsEveryoneNpc() || faction.Tag.Length > 3) continue;
                foreach(var npcId in NpcFactionPairs.Values)
                {
                    if (MyAPIGateway.Session.Factions.GetRelationBetweenFactions(faction.FactionId, npcId) != VRage.Game.MyRelationsBetweenFactions.Enemies)
                    {
                        var factionMembers = faction.Members;
                        foreach(var member in factionMembers.Keys)
                        {
                            if (MyAPIGateway.Session.Factions.GetReputationBetweenPlayerAndFaction(member, npcId) < DeclareWarAmt)
                            {
                                MyAPIGateway.Session.Factions.DeclareWar(npcId, faction.FactionId);
                            }
                        }
                    }
                }
            }
        }

        public void ChatCommands(string messageText, ref bool sendToOthers)
        {
            if (messageText.StartsWith(ChatCommand))
            {
                long clientId = MyAPIGateway.Session.LocalHumanPlayer.IdentityId;
                messageText += "\n" + clientId.ToString();
                sendToOthers = false;
                SendChatToServer(messageText);
            }
        }

        public void FactionChange(MyFactionStateChange type, long fromFaction, long toFaction, long playerId, long senderId)
        {
            if (type == MyFactionStateChange.SendPeaceRequest)
            {
                IMyFaction faction = MyAPIGateway.Session.Factions.TryGetFactionById(toFaction);
                if (faction == null || !faction.IsEveryoneNpc() || faction.Tag.Length <= 3) return;

                foreach (var npcId in NpcFactionPairs.Values)
                {
                    if (MyAPIGateway.Session.Factions.GetRelationBetweenFactions(fromFaction, toFaction) != VRage.Game.MyRelationsBetweenFactions.Enemies)
                        return;
                }

                foreach (var npcId in NpcFactionPairs.Values)
                {
                    if (npcId == toFaction)
                    {
                        if (MyAPIGateway.Session.Factions.GetReputationBetweenPlayerAndFaction(senderId, npcId) >= AllowPeaceAmt)
                        {
                            MyAPIGateway.Session.Factions.AcceptPeace(fromFaction, toFaction);
                            break;
                        }
                    }
                }
            }
        }

        private void SendChatToServer(string message)
        {
            try
            {
                var sendData = MyAPIGateway.Utilities.SerializeToBinary(message);
                MyAPIGateway.Multiplayer.SendMessageToServer(2210, sendData);
            }
            catch (Exception exc)
            {

            }
        }

        private void MessageHandler(byte[] data)
        {
            try
            {
                var receivedData = MyAPIGateway.Utilities.SerializeFromBinary<string>(data);
                if (receivedData.Contains(ChatCommand))
                {
                    var factions = MyAPIGateway.Session.Factions.Factions;
                    foreach (var faction in factions.Values)
                    {
                        if (faction.IsEveryoneNpc() || faction.Tag.Length > 3) continue;
                        var members = faction.Members;

                        foreach (var npcFaction in NpcFactionPairs.Values)
                        {
                            MyAPIGateway.Session.Factions.DeclareWar(faction.FactionId, npcFaction);
                            foreach (var member in members.Keys)
                            {
                                MyAPIGateway.Session.Factions.SetReputationBetweenPlayerAndFaction(member, npcFaction, -1000);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {

            }
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Multiplayer.UnregisterMessageHandler(2210, MessageHandler);
            MyAPIGateway.Utilities.MessageEntered -= ChatCommands;
            if (MyAPIGateway.Session.IsServer)
            {
                MyAPIGateway.Session.Factions.FactionStateChanged -= FactionChange;
            }
        }
    }
}
            

