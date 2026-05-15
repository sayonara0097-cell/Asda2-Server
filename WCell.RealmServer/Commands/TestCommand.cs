using Castle.ActiveRecord;
using Cell.Core;
using NHibernate.Engine;
using NHibernate.Linq;
using NHibernate.Mapping;
using NLog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Security.Cryptography;
using System.ServiceModel.Channels;
using WCell.Constants;
using WCell.Constants.Factions;
using WCell.Constants.Items;
using WCell.Constants.NPCs;
using WCell.Constants.Skills;
using WCell.Constants.Spells;
using WCell.Core;
using WCell.Core.Network;
using WCell.Intercommunication.DataTypes;
using WCell.RealmServer.Asda2_Items;
using WCell.RealmServer.Asda2BattleGround;
using WCell.RealmServer.Asda2Looting;
using WCell.RealmServer.Asda2PetSystem;
using WCell.RealmServer.Auth.Accounts;
using WCell.RealmServer.Chat;
using WCell.RealmServer.Database;
using WCell.RealmServer.Entities;
using WCell.RealmServer.Factions;
using WCell.RealmServer.Global;
using WCell.RealmServer.Guilds;
using WCell.RealmServer.Handlers;
using WCell.RealmServer.Items;
using WCell.RealmServer.Misc;
using WCell.RealmServer.Network;
using WCell.RealmServer.NPCs;
using WCell.RealmServer.NPCs.Spawns;
using WCell.RealmServer.Social;
using WCell.RealmServer.Spells;
using WCell.RealmServer.Spells.Auras;
using WCell.Util;
using WCell.Util.Commands;
using WCell.Util.Graphics;
using WCell.Util.Threading;

namespace WCell.RealmServer.Commands
{
    public class TestCommand : RealmServerCommand
    {
        protected TestCommand()
        {
        }

        public override RoleStatus RequiredStatusDefault
        {
            get { return RoleStatus.EventManager; }
        }

        protected override void Initialize()
        {
            Init("test");
            EnglishParamInfo = "";
            EnglishDescription = "";
        }

        public override void Process(CmdTrigger<RealmServerCmdArgs> trigger)
        {

            Character chr = (Character)trigger.Args.Target;
            var param0 = trigger.Text.NextInt(0);
            var param1 = trigger.Text.NextInt(1);
            var param2 = trigger.Text.NextInt(0);

            //using (RealmPacketOut packet = new RealmPacketOut((RealmServerOpCode)4022))
            //{
            //    packet.WriteByte((byte)1);
            //    Asda2InventoryHandler.WriteItemInfoToPacket(packet, chr.Asda2Inventory.Equipment[10]);
            //    chr.Client.Send(packet);
            //}

            //var guild = chr.Guild;
            
            var item1 = chr.Asda2Inventory.ShopItems[20];
            var item2 = chr.Asda2Inventory.ShopItems[21];
            var item3 = chr.Asda2Inventory.ShopItems[22];
            var item4 = chr.Asda2Inventory.ShopItems[23];
            var item5 = chr.Asda2Inventory.ShopItems[24];

            //Create new list
            List<Asda2Item> items = new List<Asda2Item> { item1, item2, item3, item4, item5 };
            var client = chr.Client;
            foreach (var item in items)
            {
                Asda2Item scroll = client.ActiveCharacter.Asda2Inventory.GetShopItemById(268);

                if (scroll != null && scroll.Amount >= 1)
                {
                    scroll.Amount -= 1;
                    item.Record.IsSoulBound = false;
                    item.Record.SealCount++;
                    item.Record.Save();
                    Asda2InventoryHandler.UpdateItemInventoryInfo(client, scroll);
                    Asda2InventoryHandler.UpdateItemInventoryInfo(client, item);
                }

                Asda2InventoryHandler.SealItemResponse(client, 1, item, scroll);
            }

            //using (RealmPacketOut packet = new RealmPacketOut((RealmServerOpCode)6093))
            //{
            //    packet.WriteByte(1); // Always 1
            //    packet.WriteInt16(chr.SessionId); // Weight
            //    packet.WriteInt16(chr.SessionId); // Weight
            //    packet.WriteByte(0); // Weight
            //    packet.WriteInt16(chr.SessionId); // Weight
            //    packet.WriteByte(param0); // Weight
            //    chr.Send(packet);
            //}

            //using (RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.SelfDeath))
            //{
            //    packet.WriteInt16(chr.SessionId);
            //    chr.SendPacketToArea(packet, true, true, Locale.Any, new float?());
            //}

            //using (RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.SoulmateSummoningYou))
            //{
            //    packet.WriteInt32(chr.AccId);
            //    packet.WriteInt16(chr.SessionId);
            //    packet.WriteByte(0);
            //    packet.WriteInt16((byte)chr.MapId);
            //    packet.WriteByte(1);
            //    if (chr.Client.AddrTemp.Contains("192.168."))
            //        packet.WriteFixedAsciiString(RealmServerConfiguration.ExternalAddress, 16, Locale.Start);
            //    else
            //        packet.WriteFixedAsciiString(RealmServerConfiguration.RealExternalAddress, 16, Locale.Start);
            //    packet.WriteInt16(ServerApp<RealmServer>.Instance.Port);
            //    packet.WriteInt16((short)chr.Asda2X);
            //    packet.WriteInt16((short)chr.Asda2Y);
            //    packet.WriteInt32(chr.AccId);
            //    packet.WriteInt16(chr.SessionId);
            //    packet.WriteByte(0);
            //    packet.WriteInt16((byte)chr.MapId);
            //    packet.WriteByte(1);
            //    chr.Send(packet, false);
            //}

            //اداة الاحتفالية تم استلامها

            //using (RealmPacketOut packet = new RealmPacketOut((RealmServerOpCode)6583))
            //{
            //    packet.WriteByte(1); // Always 1
            //    packet.WriteInt32(chr.Asda2Inventory.Weight); // Weight
            //    packet.WriteInt32(33800); // ItemID
            //    chr.Send(packet);
            //}

            ////////////////////////////

            //الطقس

            //using (RealmPacketOut packet = new RealmPacketOut((RealmServerOpCode)6759))
            //{
            //    int hour = DateTime.Now.Hour;
            //    int minute = DateTime.Now.Minute;
            //    int val1 = 0;
            //    int val2;
            //    if (hour < 6)
            //    {
            //        val2 = hour * 10 + minute / 6;
            //    }
            //    else
            //    {
            //        val1 = hour / 6;
            //        val2 = (hour - val1 * 6) * 10 + minute / 6;
            //    }
            //    packet.WriteByte(val1); //الزمن
            //    packet.WriteByte(val2); //الوقت
            //    packet.WriteByte(2);
            //    packet.WriteByte(3); // IDK just 3
            //    packet.WriteByte(0); // 0- Stop | 1- ?? |  2 - rain
            //    chr.Send(packet);
            //}

            /////////////////////////////////////

            //اداة الفعالية

            //using (RealmPacketOut packet = new RealmPacketOut((RealmServerOpCode)6087))
            //{
            //    packet.WriteByte(1);
            //    chr.Send(packet);
            //}

            ////////////////////////////////////

            //العداد

            //using (RealmPacketOut packet = new RealmPacketOut((RealmServerOpCode)6145))
            //{
            //    packet.WriteInt32(chr.AccId);
            //    packet.WriteInt32(param0); // الرقم
            //    packet.WriteByte(1); // النوع ؟؟؟؟
            //    chr.Send(packet);
            //}

            ////////////////////////////////////

            //شبه التخصص

            //using (RealmPacketOut packet = new RealmPacketOut((RealmServerOpCode)6231))
            //{
            //    packet.WriteByte(1);
            //    chr.Send(packet);
            //}

            //chr.LearnedTutorials.Clear();

            //using (RealmPacketOut packet = new RealmPacketOut((RealmServerOpCode)6185))
            //{
            //    chr.LearnedTutorials.WriteToAsda2Packet(packet);
            //    chr.Send(packet);
            //}

            //chr.TutorialFlags.FlagData.ForEach((f) => chr.SendErrorMsg($"{f}"));
            //using (RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.SelfDeath))
            //{
            //    packet.WriteInt16(chr.SessionId);
            //    chr.SendPacketToArea(packet, true, true, Locale.Any, new float?());
            //}



            //using (RealmPacketOut packet = new RealmPacketOut((RealmServerOpCode)6233))
            //{
            //    var group = chr.Group;
            //    if (group == null)
            //        return;
            //    int[] numArray = new int[6];
            //    string[] strArray = new string[6];
            //    int index1 = 0;
            //    for (int index2 = 0; index2 < 6; ++index2)
            //        numArray[index2] = -1;
            //    List<Character> characterList = new List<Character>();
            //    if (group.Leader.Character == null)
            //        return;
            //    characterList.Add(group.Leader.Character);
            //    characterList.AddRange(group
            //      .Where(member => member.Character != group.Leader.Character)
            //      .Select(member => member.Character));
            //    foreach (Character character in characterList)
            //    {
            //        numArray[index1] = (int)character.AccId;
            //        strArray[index1] = character.Name;
            //        ++index1;
            //    }

            //    for (int index2 = 0; index2 < 6; ++index2)
            //        packet.WriteInt32(numArray[index2]);
            //    for (int index2 = 0; index2 < 6; ++index2)
            //        packet.WriteFixedAsciiString(strArray[index2] ?? "", 20, Locale.Start);
            //    chr.Send(packet, true);
            //}

            //using (RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.ItemReplaced))
            //{
            //    packet.WriteByte((byte)1);
            //    packet.WriteByte(1);
            //    packet.WriteInt16(10);
            //    packet.WriteInt16(0);
            //    packet.WriteByte(1);
            //    packet.WriteInt32(9);
            //    packet.WriteInt16(12);
            //    packet.WriteInt16(10);
            //    packet.WriteInt16(0);
            //    packet.WriteByte(3);
            //    packet.WriteInt32(9);
            //    packet.WriteInt16(12);
            //    chr.Client.Send(packet, true);
            //}


            //NPCSpawnPoint point = new NPCSpawnPoint().;
            //(chr.Target as NPC).Brain.State = AI.BrainState.PatrolCircle;
        }
    }
}