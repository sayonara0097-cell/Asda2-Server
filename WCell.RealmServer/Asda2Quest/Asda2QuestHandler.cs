using System.Linq;
using WCell.Constants;
using WCell.Core.Network;
using WCell.RealmServer.Asda2Quests;
using WCell.RealmServer.Database;
using WCell.RealmServer.Network;

namespace WCell.RealmServer.Asda2Quest
{
    public static class Asda2QuestHandler
    {
        public static void SendQuestsListResponse(IRealmClient client)
        {
            if (client == null || client.ActiveCharacter == null)
                return;

            using (var packet = new RealmPacketOut(RealmServerOpCode.QuestsList))
            {
                var activeRecords = Asda2QuestProgressRecord.GetActiveQuestRecordsForCharacter(
                    client.ActiveCharacter.EntityId.Low)
                    .Where(q => q != null && q.Slot >= 1 && q.Slot <= 12 &&
                                Asda2QuestMgr.IsQuestClientSafe(q))
                    .GroupBy(q => q.Slot)
                    .Select(g => g.OrderByDescending(q => q.QuestRecordId).First())
                    .OrderBy(q => q.Slot)
                    .ToArray();

                for (var index = 0; index < activeRecords.Length; index++)
                {
                    var quest = activeRecords[index];
                    WriteQuestListSlot(packet, quest);
                }

                packet.WriteByte(254);
                for (var index = 0; index < 149; ++index)
                    packet.WriteByte(byte.MaxValue);
                client.Send(packet, false);
            }

        }

        private static void WriteQuestListSlot(RealmPacketOut packet, Asda2QuestProgressRecord quest)
        {
            packet.WriteInt32(quest.QuestTemplateId);
            packet.WriteByte(1);
            packet.WriteInt16(ToClientQuestSlot(quest.Slot));
            packet.WriteByte(quest.QuestStage);
            packet.WriteInt16(quest.QuestFileId);
            packet.WriteInt16(quest.CompleteStatus);
            packet.WriteInt16(1);
            WriteQuestListObjective(packet, quest.Item1Id, quest.Item1Amount);
            WriteQuestListObjective(packet, quest.Item2Id, quest.Item2Amount);
            WriteQuestListObjective(packet, quest.Item3Id, quest.Item3Amount);
            WriteQuestListObjective(packet, quest.Item4Id, quest.Item4Amount);
            WriteQuestListObjective(packet, quest.Item5Id, quest.Item5Amount);
        }

        private static int ToClientQuestSlot(int slot)
        {
            return slot <= 0 ? -1 : slot - 1;
        }

        private static void WriteQuestListObjective(RealmPacketOut packet, int targetId, int amount)
        {
            packet.WriteInt32(targetId <= 0 ? -1 : targetId);
            packet.WriteInt16(amount < 0 ? 0 : amount);
        }
    }
}
