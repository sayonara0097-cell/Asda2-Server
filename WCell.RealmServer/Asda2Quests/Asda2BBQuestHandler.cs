using System;
using WCell.Constants;
using WCell.Core.Network;
using WCell.RealmServer.Database;
using WCell.RealmServer.Entities;
using WCell.RealmServer.Handlers;
using WCell.RealmServer.Network;

namespace WCell.RealmServer.Asda2Quests
{
    internal static class Asda2BBQuestHandler
    {
        private const RealmServerOpCode BulletinQuestInfoRequestOpcode = RealmServerOpCode.DailyQuestBoardInfo;
        private const RealmServerOpCode BulletinQuestRequestOpcode = RealmServerOpCode.AcceptBBQuest;
        private const RealmServerOpCode BulletinQuestAcceptResponseOpcode = (RealmServerOpCode)4012;
        private const int MinBulletinQuestId = 20000;
        private const int MaxBulletinQuestId = 29999;

        [PacketHandler(BulletinQuestInfoRequestOpcode)]
        public static void BulletinQuestInfoRequest(IRealmClient client, RealmPacketIn packet)
        {
            var chr = client == null ? null : client.ActiveCharacter;
            if (chr == null)
                return;

            Asda2QuestHandler.SetQuests(chr);
            SendBulletinQuestClientState(client, -1);
        }

        [PacketHandler(BulletinQuestRequestOpcode)]
        public static void BulletinQuestRequest(IRealmClient client, RealmPacketIn packet)
        {
            var chr = client == null ? null : client.ActiveCharacter;
            if (chr == null)
                return;

            int questId;
            Asda2BBQuestRecord template;
            if (!TryResolveBulletinQuest(packet, out questId, out template))
            {
                chr.SendQuestMsg("Bulletin quest is not configured yet.");
                SendBulletinAccept(client, -1, questId);
                return;
            }

            if (chr.GodMode)
                chr.SendErrorMsg(questId.ToString());

            if (chr.Level < template.StartLevel || chr.Level > template.EndLevel)
            {
                chr.SendQuestMsg("Your level is not valid for this bulletin quest.");
                SendBulletinAccept(client, -1, questId);
                return;
            }

            var activeQuest = Asda2QuestProgressRecord.GetActiveQuestRecord(chr.EntityId.Low, questId);
            var completedCount = Asda2QuestMgr.GetCompletedCount(chr.EntityId.Low, questId);
            if (completedCount > 0 && completedCount >= template.RepeatCount)
            {
                chr.SendQuestMsg("You reached the repeat limit for this bulletin quest.");
                SendBulletinAccept(client, -1, questId);
                return;
            }

            if (activeQuest != null)
            {
                var questTemplate = Asda2QuestTemplateResolver.GetBulletin(questId);
                if (activeQuest.CompleteStatus != 1 && Asda2QuestMgr.IsQuestReady(activeQuest, questTemplate))
                {
                    activeQuest.QuestStage = questTemplate == null ? 1 : questTemplate.AfterStage;
                    activeQuest.CompleteStatus = 1;
                    activeQuest.Update();
                    activeQuest.Save();
                }

                if (activeQuest.CompleteStatus == 1)
                    CompleteBulletinQuest(client, activeQuest, template);
                else
                {
                    chr.SendQuestMsg("You already accepted this bulletin quest.");
                    SendBulletinAccept(client, -1, questId);
                }
                return;
            }

            AcceptBulletinQuest(client, template);
        }

        private static void AcceptBulletinQuest(IRealmClient client, Asda2BBQuestRecord template)
        {
            var chr = client.ActiveCharacter;
            var questId = (int)template.Id;
            var slot = Asda2QuestMgr.FindFreeSlot(chr);
            if (slot < 1 || slot > 12)
            {
                chr.SendQuestMsg("Your quest log is full.");
                SendBulletinAccept(client, -1, questId);
                return;
            }

            var record = new Asda2QuestProgressRecord(questId, questId, chr.EntityId.Low, slot,
                template.Item1Id, template.Item2Id, template.Item3Id, template.Item4Id, template.Item5Id,
                template.Item1Amount, template.Item2Amount, template.Item3Amount, template.Item4Amount,
                template.Item5Amount,
                template.Monster1Id, template.Monster2Id, template.Monster3Id, template.Monster4Id,
                template.Monster5Id, 1, 0);

            var questTemplate = Asda2QuestTemplateResolver.GetBulletin(questId);
            if (Asda2QuestMgr.IsQuestReady(record, questTemplate))
            {
                record.QuestStage = questTemplate.AfterStage;
                record.CompleteStatus = 1;
            }

            record.Create();
            chr.SetQuestId(record.Slot - 1, (uint)questId);
            SendBulletinAccept(client, 25, questId);
            SendBulletinQuestClientState(client, record.Slot);
        }

        private static void CompleteBulletinQuest(IRealmClient client, Asda2QuestProgressRecord quest,
            Asda2BBQuestRecord template)
        {
            var chr = client.ActiveCharacter;
            var questTemplate = Asda2QuestTemplateResolver.GetBulletin(quest.QuestTemplateId);
            if (quest.CompleteStatus != 1 && !Asda2QuestMgr.IsQuestReady(quest, questTemplate))
            {
                chr.SendQuestMsg("This bulletin quest is not complete yet.");
                SendBulletinAccept(client, -1, quest.QuestTemplateId);
                return;
            }

            var rewardIds = new[] { template.Reward1Id, template.Reward2Id };
            var rewardAmounts = new[] { template.Reward1Amount, template.Reward2Amount };
            if (!Asda2QuestInventoryHelper.TryAddItems(chr, rewardIds, rewardAmounts))
            {
                chr.SendQuestMsg("Not enough inventory space or reward data is missing.");
                SendBulletinAccept(client, -1, quest.QuestTemplateId);
                return;
            }

            chr.Money += (uint)Math.Max(0, template.Gold);
            if (template.Exp > 0)
                chr.GainXp(template.Exp, "BulletinBoardQuests", true);

            Asda2QuestInventoryHelper.RemoveQuestItems(chr, quest, questTemplate);

            var completedSlot = quest.Slot;
            quest.Slot = 0;
            quest.CompleteStatus = 2;
            quest.QuestStage = questTemplate == null ? 1 : questTemplate.AfterStage;
            quest.Update();
            quest.Save();

            Asda2QuestHandler.SetQuests(chr);
            Asda2QuestHandler.SendQuestProgressStateResponse(client, completedSlot);
            WCell.RealmServer.Asda2Quest.Asda2QuestHandler.SendQuestsListResponse(client);
            chr.SendMoneyUpdate();
            Asda2QuestHandler.SendCompleteQuestResponse(client, 20);
            SendBulletinAccept(client, 25, quest.QuestTemplateId);
        }

        public static void SendBulletinAccept(IRealmClient client, int status, int questId)
        {
            using (var packet = new RealmPacketOut(BulletinQuestAcceptResponseOpcode))
            {
                packet.WriteByte((byte)status);
                packet.WriteInt16(client.ActiveCharacter.SessionId);
                packet.WriteInt32(questId);
                packet.WriteInt16(0);
                packet.WriteInt16(questId);
                packet.WriteInt32(-1);
                packet.WriteInt16(0);
                client.Send(packet);
            }
        }

        private static void SendBulletinQuestClientState(IRealmClient client, int updatedSlot)
        {
            var chr = client == null ? null : client.ActiveCharacter;
            if (chr == null)
                return;

            Asda2QuestHandler.SetQuests(chr);
            Asda2QuestHandler.SendQuestProgressStateResponse(client, updatedSlot);
            WCell.RealmServer.Asda2Quest.Asda2QuestHandler.SendQuestsListResponse(client);
            Asda2QuestHandler.SendActiveQuestObjectiveCounterResponses(client);
        }

        private static bool TryResolveBulletinQuest(RealmPacketIn packet, out int questId,
            out Asda2BBQuestRecord template)
        {
            questId = 0;
            template = null;
            if (packet == null)
                return false;

            var position = packet.Position;
            try
            {
                if (packet.RemainingLength >= 2 && TryUseBulletinQuestId(packet.ReadInt16(), out questId, out template))
                    return true;

                packet.Position = position;
                if (packet.RemainingLength >= 4 && TryUseBulletinQuestId(packet.ReadInt32(), out questId, out template))
                    return true;
            }
            catch
            {
            }
            finally
            {
                packet.Position = position;
            }

            var raw = CopyPacketBytes(packet);
            if (raw == null || raw.Length == 0)
                return false;

            var startOffset = packet.HeaderSize >= 0 && packet.HeaderSize < raw.Length ? packet.HeaderSize : 0;
            for (var offset = startOffset; offset <= raw.Length - 2; offset++)
            {
                if (TryUseBulletinQuestId(BitConverter.ToInt16(raw, offset), out questId, out template))
                    return true;
            }

            for (var offset = startOffset; offset <= raw.Length - 4; offset++)
            {
                if (TryUseBulletinQuestId(BitConverter.ToInt32(raw, offset), out questId, out template))
                    return true;
            }

            return false;
        }

        private static bool TryUseBulletinQuestId(int candidate, out int questId,
            out Asda2BBQuestRecord template)
        {
            questId = 0;
            template = null;
            if (candidate < MinBulletinQuestId || candidate > MaxBulletinQuestId)
                return false;

            try
            {
                template = Asda2BBQuestRecord.GetRecordByID(candidate);
            }
            catch
            {
                template = null;
            }

            if (template == null)
                return false;

            questId = candidate;
            return true;
        }

        private static byte[] CopyPacketBytes(RealmPacketIn packet)
        {
            if (packet == null)
                return null;

            var position = packet.Position;
            try
            {
                packet.Position = 0;
                return packet.ReadBytes(packet.Length);
            }
            catch
            {
                return null;
            }
            finally
            {
                packet.Position = position;
            }
        }
    }
}
