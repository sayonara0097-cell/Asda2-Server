using Cell.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using WCell.Constants;
using WCell.Constants.Achievements;
using WCell.Constants.World;
using WCell.Core;
using WCell.Core.Network;
using WCell.RealmServer.Achievements;
using WCell.RealmServer.Battlegrounds;
using WCell.RealmServer.Chat;
using WCell.RealmServer.Commands;
using WCell.RealmServer.Database;
using WCell.RealmServer.Entities;
using WCell.RealmServer.Global;
using WCell.RealmServer.Guilds;
using WCell.RealmServer.Handlers;
using WCell.RealmServer.Misc;
using WCell.RealmServer.Network;
using WCell.Util.Graphics;

namespace WCell.RealmServer.Asda2BattleGround
{
    public static class Asda2BattlegroundHandler
    {
        private static readonly byte[] unk80 = new byte[107]
        {
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            byte.MaxValue,
            byte.MaxValue,
            byte.MaxValue,
            (byte) 250,
            (byte) 20,
            (byte) 124,
            (byte) 80,
            (byte) 0,
            (byte) 0
        };

        private static readonly byte[] stab9 = new byte[8]
        {
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0
        };

        private static readonly byte[] stab35 = new byte[3]
        {
            (byte) 0,
            byte.MaxValue,
            byte.MaxValue
        };

        private static readonly byte[] stab46 = new byte[16]
        {
            (byte) 0,
            (byte) 0,
            (byte) 0,
            (byte) 0,
            byte.MaxValue,
            byte.MaxValue,
            byte.MaxValue,
            byte.MaxValue,
            byte.MaxValue,
            byte.MaxValue,
            byte.MaxValue,
            byte.MaxValue,
            byte.MaxValue,
            byte.MaxValue,
            byte.MaxValue,
            byte.MaxValue
        };

        private static readonly byte[] guildCrest = new byte[40];
        private static readonly byte[] warTaxGuildCrest = "00000000A8FFFF00000000".AsBytes();

        public static void SendMessageServerAboutWarStartsResponse(byte mins)
        {
            int num1 = (int)mins / 10;
            int num2 = (int)mins % 10;
            using (RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.MessageServerAboutWarStarts))
            {
                packet.WriteInt16(0);
                packet.WriteInt16(100);
                packet.WriteByte(48 + num1);
                packet.WriteByte(48 + num2);
                packet.WriteSkip(Asda2BattlegroundHandler.unk80);
                WCell.RealmServer.Global.World.Broadcast(packet, true, Locale.Any);
            }
        }

        [PacketHandler(RealmServerOpCode.RequestUpdateWarScreenManagerData)]
        public static void RequestUpdateWarScreenManagerDataRequest(IRealmClient client, RealmPacketIn packet)
        {
            int num;
            if (client.ActiveCharacter.MapId == MapId.Alpia)
                num = 0;
            else if (client.ActiveCharacter.MapId == MapId.Silaris)
                num = 1;
            else if (client.ActiveCharacter.MapId == MapId.Flamio)
            {
                num = 2;
            }
            else
            {
                if (client.ActiveCharacter.MapId != MapId.Aquaton)
                    return;
                num = 3;
            }

            Asda2BattlegroundHandler.SendUpdateWarManagerScreenDataResponse(client,
                Asda2BattlegroundMgr.AllBattleGrounds[(Asda2BattlegroundTown)num][0]);
            Asda2BattlegroundHandler.SendWarTaxInfoResponse(client);
        }



        public static void SendUpdateWarManagerScreenDataResponse(IRealmClient client, Asda2Battleground btlgrnd)
        {
            using (RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.UpdateWarManagerScreenData))
            {
                packet.WriteByte(1);
                packet.WriteByte(0);
                packet.WriteInt16(btlgrnd.StartTime.Hour);
                packet.WriteInt16(btlgrnd.StartTime.Minute);
                packet.WriteInt16(btlgrnd.EndTime.Hour);
                packet.WriteInt16(btlgrnd.EndTime.Minute);
                packet.WriteInt16(btlgrnd.AmountOfBattleGroundsInList);
                packet.WriteInt16(btlgrnd.LightTeam.Count);
                packet.WriteInt16(btlgrnd.DarkTeam.Count);
                packet.WriteByte(0);
                packet.WriteInt32(0);
                packet.WriteInt16(btlgrnd.LightWins);
                packet.WriteInt16(btlgrnd.DarkWins);
                packet.WriteInt16(btlgrnd.LightLooses);
                packet.WriteInt16(btlgrnd.DarkLooses);
                packet.WriteInt16((int)btlgrnd.LightWins + (int)btlgrnd.LightLooses);
                packet.WriteByte(0);
                packet.WriteInt16(btlgrnd.MinEntryLevel);
                packet.WriteInt16(btlgrnd.MaxEntryLevel);
                packet.WriteByte((byte)btlgrnd.WarType);
                client.Send(packet, true);
            }
        }

        [PacketHandler(RealmServerOpCode.LeaveBattleGround)]
        public static void LeaveBattleGroundRequest(IRealmClient client, RealmPacketIn packet)
        {
            if (client.ActiveCharacter.CurrentBattleGround == null)
                return;
            client.ActiveCharacter.CurrentBattleGround.Leave(client.ActiveCharacter);
        }

        [PacketHandler(RealmServerOpCode.RegisterToWar)]
        public static void RegisterToWarRequest(IRealmClient client, RealmPacketIn packet)
        {
            Asda2Battleground asda2Battleground = Asda2BattlegroundMgr.AllBattleGrounds[
                client.ActiveCharacter.MapId == MapId.Alpia
                    ? Asda2BattlegroundTown.Alpia
                    : (client.ActiveCharacter.MapId == MapId.Silaris
                        ? Asda2BattlegroundTown.Silaris
                        : (client.ActiveCharacter.MapId == MapId.Aquaton
                            ? Asda2BattlegroundTown.Aquaton
                            : Asda2BattlegroundTown.Flamio))][0];

            if (client.ActiveCharacter.Level < (int)asda2Battleground.MinEntryLevel ||
                client.ActiveCharacter.Level > (int)asda2Battleground.MaxEntryLevel)
                Asda2BattlegroundHandler.SendRegisteredToWarResponse(client, RegisterToBattlegroundStatus.WrongLevel);
            else if (asda2Battleground.WaitingList.ContainsValue(client.ActiveCharacter))
                Asda2BattlegroundHandler.SendRegisteredToWarResponse(client,
                    RegisterToBattlegroundStatus.YouHaveAlreadyRegistered, asda2Battleground.WaitingList.First((c) => c.Value == client.ActiveCharacter).Key);
            else if (asda2Battleground.DissmisedCharacterNames.Contains(client.ActiveCharacter.Name))
                Asda2BattlegroundHandler.SendRegisteredToWarResponse(client,
                    RegisterToBattlegroundStatus.YouCantEnterCauseYouHaveBeenDissmised);
            else if (client.ActiveCharacter.Asda2FactionId < (short)0 ||
                     client.ActiveCharacter.Asda2FactionId > (short)1)
                Asda2BattlegroundHandler.SendRegisteredToWarResponse(client,
                    RegisterToBattlegroundStatus.BattleGroupInfoIsInvalid);
            else if (asda2Battleground.JoinList(client.ActiveCharacter, out int key, out int freeId) != -1)
            {
                Asda2BattlegroundHandler.SendRegisteredToWarResponse(client, RegisterToBattlegroundStatus.Ok, (byte)freeId);
            }
            else
            {
                Asda2BattlegroundHandler.SendRegisteredToWarResponse(client, RegisterToBattlegroundStatus.Fail);
                client.ActiveCharacter.SendInfoMsg("Sry no more free war places. Try again later.");
            }
        }

        public static void SendRegisteredToWarResponse(IRealmClient client, RegisterToBattlegroundStatus status, byte num = 99)
        {
            using (RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.RegisteredToWar))
            {
                packet.WriteByte((byte)status);
                packet.WriteInt16(client.ActiveCharacter.Asda2FactionId);
                packet.WriteInt16(0);
                packet.WriteInt16(num);
                client.Send(packet, false);
            }
        }

        public static void SendWarHasBeenCanceledResponse(IRealmClient client,
            Asda2BattlegroundWarCanceledReason status)
        {
            using (RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.WarHasBeenCanceled))
            {
                packet.WriteInt16((byte)status);
                client.Send(packet, false);
            }
        }

        public static void SendWiningFactionInfoResponse(Asda2BattlegroundTown townId, int factionId, string mvpName)
        {
            using (RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.WiningFactionInfo))
            {
                packet.WriteInt32((int)townId);
                packet.WriteInt32(factionId);
                packet.WriteFixedAsciiString(mvpName, 20, Locale.Start);
                WCell.RealmServer.Global.World.Broadcast(packet, true, Locale.Any);
            }
        }

        public static void SendYouCanEnterWarAfterResponse(IRealmClient client, byte key = 0)
        {
            using (RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.YouCanEnterWarAfter))
            {
                packet.WriteByte(client.ActiveCharacter.Asda2FactionId);
                packet.WriteInt16(client.ActiveCharacter.SessionId);
                packet.WriteInt16(key);
                client.Send(packet, true);
            }
        }

        public static void SendYouCanEnterWarResponse(IRealmClient client)
        {
            using (RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.YouCanEnterWar))
            {
                packet.WriteByte(0);
                packet.WriteInt16(client.ActiveCharacter.Asda2FactionId);
                packet.WriteInt16(0);
                client.Send(packet, false);
            }
        }

        [PacketHandler((RealmServerOpCode)6734)] //War lead transfer
        public static void TransferWarLeadership(IRealmClient client, RealmPacketIn packet)
        {
            packet.Position += 8;  // Assume this is necessary for packet handling
            byte v = packet.ReadByte();

            Asda2Battleground battleground = client.ActiveCharacter.CurrentBattleGround;
            Character chr = battleground.GetCharacter(client.ActiveCharacter.Asda2FactionId, v);

            if (chr != null)
            {
                byte swappedId = chr.CurrentBattleGroundId;
                var team = client.ActiveCharacter.Asda2FactionId == 0 ? battleground.LightTeam : battleground.DarkTeam;
                client.ActiveCharacter.SendErrorMsg("" + swappedId);

                var temp = team[client.ActiveCharacter.CurrentBattleGroundId];
                team[client.ActiveCharacter.CurrentBattleGroundId] = chr;
                team[swappedId] = temp;

                var tempId = client.ActiveCharacter.CurrentBattleGroundId;
                client.ActiveCharacter.CurrentBattleGroundId = swappedId;
                chr.CurrentBattleGroundId = tempId;


                SendTransferWarLeadershipResponse(chr.Client, 1, client.ActiveCharacter);
                SendTransferWarLeadershipResponse(client, 1, chr);
                SendWarTeamListResponse(client.ActiveCharacter);
                SendWarTeamListResponse(chr);
            }
        }

        public static void SendTransferWarLeadershipResponse(IRealmClient client, byte status, Character chr)
        {
            using (RealmPacketOut packet = new RealmPacketOut((RealmServerOpCode)6735))
            {
                packet.WriteByte(status);
                packet.WriteInt16(client.ActiveCharacter.SessionId);
                packet.WriteInt16(chr.SessionId);
                client.Send(packet, false);
            }
        }

        [PacketHandler(RealmServerOpCode.EnterBatlefield)]
        public static void EnterBatlefieldRequest(IRealmClient client, RealmPacketIn packet)
        {
            Asda2Battleground asda2Battleground = client.ActiveCharacter.CurrentBattleGround ??
                Asda2BattlegroundMgr.AllBattleGrounds[
                    client.ActiveCharacter.MapId == MapId.Alpia
                        ? Asda2BattlegroundTown.Alpia
                        : (client.ActiveCharacter.MapId == MapId.Silaris
                            ? Asda2BattlegroundTown.Silaris
                            : (client.ActiveCharacter.MapId == MapId.Aquaton
                                ? Asda2BattlegroundTown.Aquaton
                                : Asda2BattlegroundTown.Flamio))][0];
            asda2Battleground.Join(client.ActiveCharacter);
            if (client.ActiveCharacter.CurrentBattleGround == null)
                client.ActiveCharacter.SendWarMsg("You are not registered to faction war.");
            else if (!client.ActiveCharacter.CurrentBattleGround.IsRunning)
                client.ActiveCharacter.SendWarMsg(string.Format("War is not started yet. Wait {0} mins.",
                    (object)(int)(client.ActiveCharacter.CurrentBattleGround.StartTime - DateTime.Now).TotalMinutes));
            else if (client.ActiveCharacter.MapId == MapId.BatleField)
            {
                client.ActiveCharacter.SendWarMsg("You already on war.");
                SendInitialWarState(client.ActiveCharacter);
            }
            else
            {
                Character character = client.ActiveCharacter;
                Asda2Battleground battleground = character.CurrentBattleGround;
                character.IsChangingAsda2BattleGroundMap = true;
                battleground.TeleportToWar(character);
                WCell.RealmServer.Global.World.TaskQueue.CallDelayed(1000,
                    () =>
                    {
                        if (character.CurrentBattleGround != null && character.MapId == MapId.BatleField)
                        {
                            SendInitialWarState(character);
                            character.IsChangingAsda2BattleGroundMap = false;
                        }
                    });
            }
        }

        public static void SendCharacterPositionInfoOnWarResponse(Character chr)
        {
            if (chr.CurrentBattleGround == null)
                return;
            using (RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.CharacterPositionInfoOnWar))
            {
                packet.WriteInt16(chr.Asda2FactionId);
                packet.WriteInt32(chr.AccId);
                packet.WriteInt16(chr.SessionId);
                packet.WriteInt16((short)chr.Asda2Position.X);
                packet.WriteInt16((short)chr.Asda2Position.Y);
                chr.CurrentBattleGround.Send(packet, true, new short?(chr.Asda2FactionId), Locale.Any);
            }
        }

        public static void SendSomeOneKilledSomeOneResponse(Asda2Battleground btlgrnd, int killerAccId, int killerWarId,
            string killerName, string victimName)
        {
            Character characterByAccId = WCell.RealmServer.Global.World.GetCharacterByAccId((uint)killerAccId);
            AchievementProgressRecord progressRecord = characterByAccId.Achievements.GetOrCreateProgressRecord(21U);
            switch (++progressRecord.Counter)
            {
                case 13:
                    characterByAccId.DiscoverTitle(Asda2TitleId.Soldier129);
                    break;
                case 25:
                    characterByAccId.GetTitle(Asda2TitleId.Soldier129);
                    break;
                case 75:
                    characterByAccId.DiscoverTitle(Asda2TitleId.Killer130);
                    break;
                case 100:
                    characterByAccId.GetTitle(Asda2TitleId.Killer130);
                    break;
                case 500:
                    characterByAccId.DiscoverTitle(Asda2TitleId.Assassin131);
                    break;
                case 1000:
                    characterByAccId.GetTitle(Asda2TitleId.Assassin131);
                    break;
            }

            progressRecord.SaveAndFlush();
            using (RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.SomeOneKilledSomeOne))
            {
                packet.WriteInt32(killerAccId);
                packet.WriteInt32(killerWarId);
                packet.WriteInt32(1);
                packet.WriteFixedAsciiString(killerName, 20, Locale.Start);
                packet.WriteFixedAsciiString(victimName, 20, Locale.Start);
                btlgrnd.Send(packet, true, new short?(), Locale.Any);
            }
        }

        public static void SendTeamPointsResponse(Asda2Battleground btlgrnd, Character chr = null)
        {
            using (RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.TeamPoints))
            {
                packet.WriteInt32(btlgrnd.LightScores);
                packet.WriteInt32(btlgrnd.DarkScores);
                if (chr != null)
                    chr.Send(packet, true);
                else
                    btlgrnd.Send(packet, true, new short?(), Locale.Any);
            }
        }

        public static void SendWarRemainingTimeResponse(IRealmClient client, Asda2Battleground battleground = null)
        {
            if (client == null)
                return;
            TimeSpan remainingTime = TimeSpan.Zero;
            Asda2Battleground activeBattleground = battleground ??
                (client.ActiveCharacter == null ? null : client.ActiveCharacter.CurrentBattleGround);
            if (activeBattleground != null && activeBattleground.IsRunning)
            {
                remainingTime = activeBattleground.EndTime - DateTime.Now;
                if (remainingTime < TimeSpan.Zero)
                    remainingTime = TimeSpan.Zero;
            }

            using (RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.WarRemainingTime))
            {
                packet.WriteInt16((short)Math.Min(short.MaxValue, (int)remainingTime.TotalHours));
                packet.WriteInt16((short)remainingTime.Minutes);
                packet.WriteInt16((short)remainingTime.Seconds);
                client.Send(packet, false);
            }
        }

        public static void SendWarRemainingTimeResponse(Asda2Battleground battleground)
        {
            if (battleground == null)
                return;
            List<Character> characters = new List<Character>();
            lock (battleground.JoinLock)
            {
                characters.AddRange(battleground.LightTeam.Values);
                characters.AddRange(battleground.DarkTeam.Values);
            }

            foreach (Character character in characters)
            {
                if (character != null && character.Client != null)
                    SendWarRemainingTimeResponse(character.Client, battleground);
            }
        }

        [PacketHandler(RealmServerOpCode.WarChatRequest)]
        public static void WarChatRequestRequest(IRealmClient client, RealmPacketIn packet)
        {
            int num1 = (int)packet.ReadInt16();
            int num2 = (int)packet.ReadInt16();
            packet.Position += 20;
            string str = packet.ReadAsdaString(200, client.Locale);
            if (str.Length < 1 ||
                RealmCommandHandler.HandleCommand((IUser)client.ActiveCharacter, str,
                    (IGenericChatTarget)(client.ActiveCharacter.Target as Character)) ||
                !client.ActiveCharacter.IsAsda2BattlegroundInProgress)
                return;
            int senderIndex = client.ActiveCharacter.Asda2FactionId == 0 ? client.ActiveCharacter.CurrentBattleGround.LightTeam.Values.ToList().IndexOf(client.ActiveCharacter) : client.ActiveCharacter.CurrentBattleGround.DarkTeam.Values.ToList().IndexOf(client.ActiveCharacter);
            Asda2BattlegroundHandler.SendWarChatResponseResponse(client.ActiveCharacter.CurrentBattleGround, senderIndex,
                client.ActiveCharacter.Name, str, (int)client.ActiveCharacter.Asda2FactionId);
        }

        public static void SendWarChatResponseResponse(Asda2Battleground btlgrnd, int senderIndex, string senderName, string message,
            int factionId)
        {
            using (RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.WarChatResponse))
            {
                packet.WriteByte(senderIndex);
                packet.WriteFixedAsciiString(senderName, 20, Locale.Any);
                packet.WriteFixedAsciiString(message, 200, Locale.Any);
                btlgrnd.Send(packet, true, new short?((short)factionId), Locale.Any);
            }
        }

        public static void SendHowManyPeopleInWarTeamsResponse(Asda2Battleground btlgrnd, Character chr = null)
        {
            using (RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.HowManyPeopleInWarTeams))
            {
                packet.WriteInt16(btlgrnd.LightTeam.Count);
                packet.WriteInt16(btlgrnd.DarkTeam.Count);
                if (chr != null)
                    chr.Send(packet, true);
                else
                    btlgrnd.Send(packet, true, new short?(), Locale.Any);
            }
        }

        public static void SendWarTaxInfoResponse(IRealmClient client)
        {
            if (client == null)
                return;

            using (RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.WarTaxInfo))
            {
                packet.WriteByte(1);
                for (int i = 0; i < 3; i++)
                {
                    packet.WriteInt16(1);
                    packet.WriteInt32(6);
                    packet.WriteInt16(0);
                    packet.WriteAsdaString("a1234567890z", 14);
                    packet.WriteAsdaString("1234567890123456789012345678901234567890123456789012345678", 58);
                    packet.WriteSkip(warTaxGuildCrest);
                }

                client.Send(packet, true);
            }
        }

        public static void SendInitialWarState(Character chr)
        {
            if (chr == null || chr.Client == null || chr.CurrentBattleGround == null)
                return;

            Asda2Battleground battleground = chr.CurrentBattleGround;
            if (chr.MapId == MapId.BatleField)
            {
                chr.IsChangingAsda2BattleGroundMap = false;
                battleground.MarkTeamEnteredThisWar(chr.Asda2FactionId);
            }
            SendWarTaxInfoResponse(chr.Client);
            battleground.SendCurrentProgress(chr);
            SendWarTeamListResponse(chr);
            SendTeamPointsResponse(battleground, chr);
            SendHowManyPeopleInWarTeamsResponse(battleground, chr);
            SendWarTeamMarkers(chr);
            SendWarRemainingTimeResponse(chr.Client, battleground);
            if (!battleground.IsStarted)
                SendWarCurrentActionInfoResponse(battleground, BattleGroundInfoMessageType.PreWarCircle, -1, chr,
                    new short?());

            if (battleground.WarType != Asda2BattlegroundType.Occupation)
                return;

            foreach (Asda2WarPoint point in battleground.Points)
            {
                SendWarPointsPreInitResponse(chr.Client, point);
                SendUpdatePointInfoResponse(chr.Client, point);
            }
        }

        public static void SendWarTeamMarkers(Character chr)
        {
            if (chr == null || chr.Client == null || chr.CurrentBattleGround == null)
                return;

            Asda2Battleground battleground = chr.CurrentBattleGround;
            List<Character> characters = new List<Character>();
            lock (battleground.JoinLock)
            {
                characters.AddRange(battleground.LightTeam.Values);
                characters.AddRange(battleground.DarkTeam.Values);
            }

            foreach (Character target in characters)
            {
                if (target == null)
                    continue;
                GlobalHandler.SendWarTeamMarkerIfNeeded(chr.Client, target);
            }

            foreach (Character receiver in characters)
            {
                if (receiver == null || receiver == chr || receiver.Client == null)
                    continue;
                GlobalHandler.SendWarTeamMarkerIfNeeded(receiver.Client, chr);
            }
        }

        public static void SendCharacterHasLeftWarResponse(Asda2Battleground btlgrnd, byte status, int leaverAccId, byte leaverWarId,
            string leaverName, int factionId)
        {
            using (RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.CharacterHasLeftWar))
            {
                packet.WriteByte(status);
                packet.WriteInt32(leaverAccId);
                packet.WriteByte(leaverWarId);
                packet.WriteFixedAsciiString(leaverName, 20, Locale.Start);
                btlgrnd.Send(packet, true, new short?((short)factionId), Locale.Any);
            }
        }

        public static void SendWarCurrentActionInfoResponse(Asda2Battleground btlgrnd,
            BattleGroundInfoMessageType status, short value, Character chr = null, short? factionId = null)
        {
            using (RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.WarCurrentActionInfo))
            {
                packet.WriteByte((byte)status);
                packet.WriteInt16(value);
                if (chr == null)
                    btlgrnd.Send(packet, false, factionId, Locale.Any);
                else
                    chr.Send(packet, false);
            }
        }

        public static void SendWarEndedResponse(IRealmClient client, byte winingFaction, int winingFactionPoints,
            int losserFactionPoints, int honorPoints, short honorCoin, long expReward, string mvpName)
        {
            using (RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.WarEnded))
            {
                packet.WriteInt16(winingFaction);
                packet.WriteByte(winingFaction == 0 ? 0 : 1);
                packet.WriteByte(0);
                packet.WriteByte(winingFaction == 1 ? 1 : 0);
                packet.WriteByte(0);
                packet.WriteSkip(Asda2BattlegroundHandler.stab9);
                packet.WriteInt32(winingFactionPoints);
                packet.WriteInt32(losserFactionPoints);
                packet.WriteInt32(honorPoints);
                packet.WriteInt16(honorCoin);
                packet.WriteSkip(Asda2BattlegroundHandler.stab35);
                packet.WriteInt64(expReward);
                packet.WriteSkip(Asda2BattlegroundHandler.stab46);
                packet.WriteFixedAsciiString(mvpName, 20, Locale.Start);
                client.Send(packet, true);
            }
        }

        public static void SendWarEndedOneResponse(IRealmClient client, IEnumerable<Asda2Item> prizeItems)
        {
            Asda2Item[] asda2ItemArray = new Asda2Item[4];
            int num = 0;
            foreach (Asda2Item prizeItem in prizeItems)
                asda2ItemArray[num++] = prizeItem;
            using (RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.WarEndedOne))
            {
                packet.WriteByte(1);
                packet.WriteInt16(client.ActiveCharacter.Asda2Inventory.Weight);
                packet.WriteInt32(client.ActiveCharacter.Money);
                for (int index = 0; index < 4; ++index)
                {
                    Asda2Item asda2Item = asda2ItemArray[index];
                    Asda2InventoryHandler.WriteItemInfoToPacket(packet, asda2Item, false);
                }

                client.Send(packet, true);
            }
        }

        [PacketHandler(RealmServerOpCode.ShowWarUnit)]
        public static void ShowWarUnitRequest(IRealmClient client, RealmPacketIn packet)
        {
            if (!client.ActiveCharacter.IsAsda2BattlegroundInProgress)
                return;
            Asda2BattlegroundHandler.SendWarTeamListResponse(client.ActiveCharacter);
        }

        [PacketHandler(RealmServerOpCode.CancleWarPatipication)]
        public static void CancleWarPatipicationRequest(IRealmClient client, RealmPacketIn packet)
        {
            Asda2Battleground asda2Battleground = Asda2BattlegroundMgr.AllBattleGrounds[
                client.ActiveCharacter.MapId == MapId.Alpia
                    ? Asda2BattlegroundTown.Alpia
                    : (client.ActiveCharacter.MapId == MapId.Silaris
                        ? Asda2BattlegroundTown.Silaris
                        : (client.ActiveCharacter.MapId == MapId.Aquaton
                            ? Asda2BattlegroundTown.Aquaton
                            : Asda2BattlegroundTown.Flamio))][0];

            asda2Battleground.LeaveList(client.ActiveCharacter);
            Asda2BattlegroundHandler.SendWarPartipicationCanceledResponse(client);
        }

        public static void SendWarPartipicationCanceledResponse(IRealmClient client)
        {
            using (RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.WarPartipicationCanceled))
            {
                packet.WriteByte(1);
                packet.WriteByte(client.ActiveCharacter.Asda2FactionId);
                client.Send(packet, false);
            }
        }

        public static void SendWarTeamListResponse(Character chr)
        {
            var btldrnd = chr.CurrentBattleGround;
            var chrsLists = new List<List<Character>>();
            lock (btldrnd.JoinLock)
            {
                if (chr.Asda2FactionId == 0)
                {
                    var listsCount = btldrnd.LightTeam.Count / 6;
                    if (listsCount == 0)
                        listsCount = 1;
                    for (int i = 0; i < listsCount; i++)
                    {
                        chrsLists.Add(btldrnd.LightTeam.Values.Skip(i * 6).Take(6).ToList());
                    }
                }
                else if (chr.Asda2FactionId == 1)
                {
                    var listsCount = btldrnd.DarkTeam.Count / 6;
                    if (listsCount == 0)
                        listsCount = 1;
                    for (int i = 0; i < listsCount; i++)
                    {
                        chrsLists.Add(btldrnd.DarkTeam.Values.Skip(i * 6).Take(6).ToList());
                    }
                }

                foreach (var characterList in chrsLists)
                {
                    using (RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.WarTeamList))
                    {
                        foreach (Character character in characterList)
                        {
                            packet.WriteByte(character.CurrentBattleGroundId);
                            packet.WriteByte(1);
                            packet.WriteInt16(character.SessionId);
                            packet.WriteInt32(character.AccId);
                            packet.WriteByte(character.CharNum);
                            packet.WriteByte(character.CurrentBattleGroundId);
                            packet.WriteByte(character.Asda2FactionRank);
                            packet.WriteInt16(character.Level);
                            packet.WriteByte(character.ProfessionLevel);
                            packet.WriteByte((byte)character.Archetype.ClassId);
                            packet.WriteByte(character.Guild == null ? 0 : (character.Guild.ClanCrest == null ? 0 : 1));
                            packet.WriteSkip(character.Guild == null
                                ? Asda2BattlegroundHandler.guildCrest
                                : character.Guild.ClanCrest ?? Asda2BattlegroundHandler.guildCrest);
                            packet.WriteInt16(character.IsInGroup ? 1 : -1);
                            packet.WriteInt16(character.BattlegroundDeathes);
                            packet.WriteInt16(character.BattlegroundKills);
                            packet.WriteInt16(character.BattlegroundActPoints);
                            packet.WriteFixedAsciiString(character.Name, 20, Locale.Start);
                            packet.WriteFixedAsciiString(character.Guild == null ? "" : character.Guild.Name, 17,
                                Locale.Start);
                            packet.WriteInt16((short)character.Asda2Position.X);
                            packet.WriteInt16((short)character.Asda2Position.Y);
                        }

                        chr.Send(packet, false);
                    }
                }


                Asda2BattlegroundHandler.SendWarTeamListEndedResponse(chr.Client);
            }
        }

        public static void SendUpdatePointInfoResponse(IRealmClient client, Asda2WarPoint point)
        {
            if (point == null)
                return;

            using (RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.UpdatePointInfo))
            {
                packet.WriteInt16(point.Id);
                packet.WriteInt16(point.X);
                packet.WriteInt16(point.Y);
                packet.WriteInt16(point.OwnedFaction);
                packet.WriteInt16((short)point.Status);
                packet.WriteByte(0);
                if (client != null)
                    client.Send(packet, true);
                else if (point.BattleGround != null)
                    point.BattleGround.Send(packet, true, new short?(), Locale.Any);
            }
        }

        public static void SendWarPointsPreInitResponse(IRealmClient client, Asda2WarPoint point)
        {
            if (client == null || point == null)
                return;

            using (RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.WarPointsPreInit))
            {
                packet.WriteInt16(point.Id);
                packet.WriteInt16(point.X);
                packet.WriteInt16(point.Y);
                packet.WriteInt16(point.OwnedFaction);
                packet.WriteInt16((short)point.Status);
                packet.WriteByte(0);
                client.Send(packet, true);
            }
        }

        public static void SendWarPointStateResponse(Asda2WarPoint point)
        {
            if (point == null || point.BattleGround == null)
                return;

            List<Character> characters = new List<Character>();
            lock (point.BattleGround.JoinLock)
            {
                characters.AddRange(point.BattleGround.LightTeam.Values);
                characters.AddRange(point.BattleGround.DarkTeam.Values);
            }

            foreach (Character character in characters)
            {
                if (character == null || character.Client == null)
                    continue;

                SendWarPointsPreInitResponse(character.Client, point);
                SendUpdatePointInfoResponse(character.Client, point);
            }
        }

        [PacketHandler(RealmServerOpCode.StartOcupyPoint)]
        public static void StartOcupyPointRequest(IRealmClient client, RealmPacketIn packet)
        {
            packet.Position += 2;
            byte num = packet.ReadByte();
            if (!client.ActiveCharacter.IsAsda2BattlegroundInProgress ||
                client.ActiveCharacter.MapId != MapId.BatleField)
            {
                client.ActiveCharacter.YouAreFuckingCheater("Trying to occupy point while not in war.", 20);
            }
            else
            {
                Asda2Battleground currentBattleGround = client.ActiveCharacter.CurrentBattleGround;
                Asda2WarPoint point = GetCapturePoint(currentBattleGround, client.ActiveCharacter, num);
                if (point == null)
                    client.ActiveCharacter.YouAreFuckingCheater("Trying to occupy unknown war point.", 20);
                else
                    point.TryCapture(client.ActiveCharacter);
            }
        }

        private static Asda2WarPoint GetCapturePoint(Asda2Battleground battleground, Character chr, byte clientPointId)
        {
            if (battleground == null || battleground.Points == null || battleground.Points.Count == 0)
                return null;

            Asda2WarPoint exactPoint = battleground.Points.FirstOrDefault(point => point.Id == clientPointId);
            if (IsCloseToPoint(chr, exactPoint))
                return exactPoint;

            if (clientPointId < battleground.Points.Count &&
                IsCloseToPoint(chr, battleground.Points[(int)clientPointId]))
                return battleground.Points[(int)clientPointId];

            int oneBasedIndex = clientPointId - 1;
            if (oneBasedIndex >= 0 && oneBasedIndex < battleground.Points.Count &&
                IsCloseToPoint(chr, battleground.Points[oneBasedIndex]))
                return battleground.Points[oneBasedIndex];

            Asda2WarPoint nearestPoint = battleground.Points
                .Where(point => IsCloseToPoint(chr, point))
                .OrderBy(point => GetDistanceToPoint(chr, point))
                .FirstOrDefault();

            if (nearestPoint != null)
                return nearestPoint;

            if (exactPoint != null)
                return exactPoint;
            if (clientPointId < battleground.Points.Count)
                return battleground.Points[(int)clientPointId];

            return null;
        }

        private static bool IsCloseToPoint(Character chr, Asda2WarPoint point)
        {
            return chr != null && point != null && GetDistanceToPoint(chr, point) <= 7.0;
        }

        private static double GetDistanceToPoint(Character chr, Asda2WarPoint point)
        {
            return chr.Asda2Position.GetDistance(new Vector3((float)point.X, (float)point.Y));
        }

        public static void SendOccupyingPointStartedResponse(IRealmClient client, short pointId,
            OcupationPointStartedStatus status)
        {
            using (RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.OccupyingPointStarted))
            {
                packet.WriteInt16(pointId);
                packet.WriteByte((byte)status);
                packet.WriteInt32(client.ActiveCharacter.AccId);
                client.ActiveCharacter.SendPacketToArea(packet, true, true, Locale.Any, new float?());
            }
        }

        public static void SendWarTeamListEndedResponse(IRealmClient client)
        {
            using (RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.WarTeamListEnded))
                client.Send(packet, false);
        }

        [PacketHandler(RealmServerOpCode.DismissPlayerFromWar)]
        public static void DismissPlayerFromWarRequest(IRealmClient client, RealmPacketIn packet)
        {
            short num = packet.ReadInt16();
            if (!client.ActiveCharacter.IsAsda2BattlegroundInProgress)
            {
                client.ActiveCharacter.YouAreFuckingCheater("Trying to dissmis someone while not on war.", 50);
            }
            else
            {
                Character character =
                    client.ActiveCharacter.CurrentBattleGround.GetCharacter(client.ActiveCharacter.Asda2FactionId,
                        (byte)num);
                if (character == null)
                    client.ActiveCharacter.SendWarMsg("Target character not found.");
                using (RealmPacketOut packet1 = new RealmPacketOut(RealmServerOpCode.DismissPlayerFromWarRequestResult))
                {
                    if (character == null ||
                        !client.ActiveCharacter.CurrentBattleGround.TryStartDissmisProgress(client.ActiveCharacter,
                            character) || client.ActiveCharacter.Money < 10000U)
                    {
                        packet1.WriteByte(0);
                        packet1.WriteInt16(client.ActiveCharacter.Asda2Inventory.Weight);
                        Asda2InventoryHandler.WriteItemInfoToPacket(packet1, (Asda2Item)null, false);
                    }
                    else
                    {
                        packet1.WriteByte(1);
                        packet1.WriteInt16(client.ActiveCharacter.Asda2Inventory.Weight);
                        Asda2InventoryHandler.WriteItemInfoToPacket(packet1,
                            client.ActiveCharacter.Asda2Inventory.GetRegularItem((short)0), false);
                        client.ActiveCharacter.SubtractMoney(10000U);
                    }

                    client.Send(packet1, true);
                }
            }
        }

        public static void SendQuestionDismissPlayerOrNotResponse(Asda2Battleground client, Character initer,
            Character target)
        {
            using (RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.QuestionDismissPlayerOrNot))
            {
                packet.WriteInt16(initer.SessionId);
                packet.WriteInt16(initer.Asda2FactionId);
                packet.WriteInt16(target.SessionId);
                packet.WriteInt32(target.AccId);
                client.Send(packet, true, new short?(initer.Asda2FactionId), Locale.Any);
            }
        }

        [PacketHandler(RealmServerOpCode.AnswerDismissPlayer)]
        public static void AnswerDismissPlayerRequest(IRealmClient client, RealmPacketIn packet)
        {
            packet.Position -= 4;
            bool kick = packet.ReadByte() == 1;
            if (!client.ActiveCharacter.IsAsda2BattlegroundInProgress)
                client.ActiveCharacter.SendWarMsg("Player not found.");
            else
                client.ActiveCharacter.CurrentBattleGround.AnswerDismiss(kick, client.ActiveCharacter);
        }

        public static void SendDissmissResultResponse(Asda2Battleground client, DismissPlayerResult status,
            short targetSessId, int targetAccId)
        {
            using (RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.DissmissResult))
            {
                packet.WriteByte((byte)status);
                packet.WriteInt16(targetSessId);
                packet.WriteInt32(targetAccId);
                client.Send(packet, true, new short?(), Locale.Any);
            }
        }
    }
}
