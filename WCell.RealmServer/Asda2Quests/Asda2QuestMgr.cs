using System;
using System.Linq;
using NLog;
using WCell.RealmServer.Database;
using WCell.RealmServer.Entities;
using WCell.Util;

namespace WCell.RealmServer.Asda2Quests
{
    public static class Asda2QuestMgr
    {
        private static readonly Logger log = LogManager.GetCurrentClassLogger();
        private const byte NpcQuestMarkNone = 0;
        private const byte NpcQuestMarkReady = 7;
        private const byte NpcQuestMarkAvailable = 8;
        private const int MaxNpcQuestNumProbe = 31;
        private static readonly int[] MissingClientQuestBookFileIds =
        {
            417, 443, 444, 447, 733, 932, 941, 942, 943, 944, 946, 947, 948
        };

        public static int FindFreeSlot(Character chr)
        {
            if (chr == null)
                return -1;

            var activeQuests = Asda2QuestProgressRecord.GetActiveQuestRecordsForCharacter(chr.EntityId.Low);
            for (var slot = 1; slot <= 12; slot++)
            {
                if (!activeQuests.Any(qr => qr != null && qr.Slot == slot))
                    return slot;
            }
            return -1;
        }

        public static void OnMonsterKilled(Character chr, NPC npc)
        {
            if (chr == null || chr.Client == null || npc == null)
                return;

            var activeQuests = Asda2QuestProgressRecord.GetActiveQuestRecordsForCharacter(chr.EntityId.Low);
            foreach (var activeQuest in activeQuests)
            {
                if (activeQuest == null || activeQuest.CompleteStatus == 1)
                    continue;

                var template = Asda2QuestTemplateResolver.Get(activeQuest.QuestTemplateId);
                if (template == null)
                    continue;

                for (var index = 0; index < template.MonsterIds.Length; index++)
                {
                    var monsterId = template.MonsterIds[index];
                    if (IsKilledNpcMatch(npc, monsterId))
                        TryUpdateProgress(chr, npc, activeQuest, template, index + 1, monsterId);
                }
            }
        }

        private static void TryUpdateProgress(Character chr, NPC npc, Asda2QuestProgressRecord quest,
            Asda2QuestTemplateInfo template, int objectiveIndex, int matchedMonsterId)
        {
            var requiredAmount = GetRequiredAmount(template, objectiveIndex);
            if (requiredAmount <= 0)
                return;

            var currentAmount = GetObjectiveAmount(quest, objectiveIndex);
            if (currentAmount >= requiredAmount)
                return;

            var nextAmount = Math.Min(currentAmount + 1, requiredAmount);
            SetObjectiveAmount(quest, objectiveIndex, nextAmount);

            if (IsQuestReady(quest, template))
            {
                quest.QuestStage = template.AfterStage;
                quest.CompleteStatus = 1;
                log.Debug("QUEST READY: Character={0}, QuestId={1}", chr.Name, quest.QuestTemplateId);
            }

            quest.Update();
            quest.Save();

            log.Debug("QUEST PROGRESS applied: Character={0}, QuestId={1}, MonsterId={2}, NpcEntryId={3}, Objective={4}, Amount={5}/{6}",
                chr.Name, quest.QuestTemplateId, matchedMonsterId, npc.EntryId, objectiveIndex, nextAmount, requiredAmount);

            if (template.IsBulletin)
            {
                Asda2QuestHandler.SendBulletinUpdateQuestResponse(chr.Client, quest, objectiveIndex, matchedMonsterId,
                    GetObjectiveItemId(quest, objectiveIndex),
                    nextAmount, requiredAmount);
            }
            else
            {
                Asda2QuestHandler.SendUpdateQuestResponse(chr.Client, quest, objectiveIndex, matchedMonsterId,
                    GetObjectiveItemId(quest, objectiveIndex),
                    nextAmount, requiredAmount);
            }
        }

        private static bool IsKilledNpcMatch(NPC npc, int monsterId)
        {
            if (npc == null || monsterId <= 0)
                return false;

            if (IsSameId(npc.EntryId, monsterId))
                return true;

            if (npc.Entry != null)
            {
                if (IsSameId((uint)npc.Entry.NPCId, monsterId))
                    return true;
                if (IsSameId(npc.Entry.Id, monsterId))
                    return true;
            }

            if (npc.Template != null && IsSameId(npc.Template.Id, monsterId))
                return true;

            var spawnEntry = npc.SpawnEntry;
            if (spawnEntry != null && IsSameId((uint)spawnEntry.EntryId, monsterId))
                return true;

            return IsSameId(npc.EntityId.Entry, monsterId);
        }

        private static bool IsSameId(uint npcId, int monsterId)
        {
            return monsterId > 0 && npcId == (uint)monsterId;
        }

        internal static bool IsQuestReady(Asda2QuestProgressRecord quest, Asda2QuestTemplateInfo template)
        {
            if (quest == null || template == null)
                return false;

            for (var index = 1; index <= 5; index++)
            {
                if (!IsObjectiveReady(GetObjectiveAmount(quest, index), GetRequiredAmount(template, index)))
                    return false;
            }
            return true;
        }

        internal static byte GetNpcQuestMark(Character chr, int npcId)
        {
            if (chr == null || npcId <= 0)
                return NpcQuestMarkNone;

            var ownerId = chr.EntityId.Low;
            var activeQuests = Asda2QuestProgressRecord.GetActiveQuestRecordsForCharacter(ownerId);

            foreach (var activeQuest in activeQuests)
            {
                if (activeQuest == null)
                    continue;

                var template = Asda2QuestTemplateResolver.Get(activeQuest.QuestTemplateId);
                if (!CanCompleteQuestAtNpc(npcId, activeQuest.QuestTemplateId, template))
                    continue;

                if (activeQuest.CompleteStatus == 1 || (template != null && IsQuestReady(activeQuest, template)))
                    return NpcQuestMarkReady;
            }

            if (HasAvailableNpcQuestStarter(chr, npcId, ownerId))
                return NpcQuestMarkAvailable;

            return NpcQuestMarkNone;
        }

        internal static bool HasCompletedNpcQuest(Character chr, int npcId)
        {
            if (chr == null || npcId <= 0)
                return false;

            var records = Asda2QuestProgressRecord.GetQuestRecordForCharacter(chr.EntityId.Low);
            foreach (var record in records)
            {
                if (record == null || record.CompleteStatus < 2)
                    continue;

                var template = Asda2QuestTemplateResolver.Get(record.QuestTemplateId);
                if (CanStartQuestAtNpc(npcId, record.QuestTemplateId, template) ||
                    CanCompleteQuestAtNpc(npcId, record.QuestTemplateId, template))
                    return true;
            }

            return false;
        }

        internal static int GetCompletedCount(uint ownerId, int questId)
        {
            return Asda2QuestProgressRecord.GetQuestRecordForCharacter(ownerId)
                .Count(record => record != null &&
                                 record.QuestTemplateId == questId &&
                                 record.CompleteStatus >= 2);
        }

        internal static bool CanRepeat(Asda2QuestTemplateInfo template, int completedCount)
        {
            return template != null && template.RepeatCount > 0 && completedCount < template.RepeatCount;
        }

        internal static bool IsQuestClientSafe(Asda2QuestProgressRecord quest)
        {
            if (quest == null)
                return false;

            var template = Asda2QuestTemplateResolver.Get(quest.QuestTemplateId);
            return IsQuestClientSafe(template, quest.QuestFileId);
        }

        internal static bool IsQuestClientSafe(Asda2QuestTemplateInfo template, int questFileId)
        {
            if (template == null)
                return false;

            if (template.IsBulletin)
                return true;

            var fileId = template.FileId >= 0 ? template.FileId : questFileId;
            if (fileId < 0 || IsKnownMissingClientQuestBook(fileId))
                return false;

            return template.CompleteNpcId > 0 || HasRequiredObjective(template);
        }

        internal static bool HasRequiredObjective(Asda2QuestTemplateInfo template)
        {
            if (template == null)
                return false;

            for (var index = 0; index < 5; index++)
            {
                if (template.RequiredAmounts[index] > 0 &&
                    (template.MonsterIds[index] > 0 || template.ItemIds[index] > 0))
                    return true;
            }

            return false;
        }

        internal static int GetQuestObjectiveDisplayId(Asda2QuestProgressRecord quest, int objectiveIndex)
        {
            if (quest == null || objectiveIndex < 1 || objectiveIndex > 5)
                return -1;

            var template = Asda2QuestTemplateResolver.Get(quest.QuestTemplateId);
            var monsterId = template == null ? GetObjectiveMonsterId(quest, objectiveIndex) : template.MonsterIds[objectiveIndex - 1];
            if (monsterId > 0)
                return monsterId;

            var itemId = GetObjectiveItemId(quest, objectiveIndex);
            return itemId > 0 ? itemId : -1;
        }

        internal static int GetQuestObjectiveRequiredAmount(Asda2QuestProgressRecord quest, int objectiveIndex)
        {
            if (quest == null || objectiveIndex < 1 || objectiveIndex > 5)
                return 0;

            var template = Asda2QuestTemplateResolver.Get(quest.QuestTemplateId);
            return template == null ? 0 : GetRequiredAmount(template, objectiveIndex);
        }

        private static bool CanCompleteQuestAtNpc(int npcId, int questId, Asda2QuestTemplateInfo template)
        {
            return (template != null && template.CompleteNpcId == npcId) ||
                   Asda2QuestRewardNpc.HasQuest(npcId, questId) ||
                   HasRewardNpcQuest(npcId, questId) ||
                   Asda2QuestTemplateRecord.HasCompleter(npcId, questId);
        }

        private static bool CanStartQuestAtNpc(int npcId, int questId, Asda2QuestTemplateInfo template)
        {
            return (template != null && template.StartNpcId == npcId) ||
                   HasStarterNpcQuest(npcId, questId) ||
                   HasTemplateStarterNpcQuest(npcId, questId);
        }

        private static bool HasStarterNpcQuest(int npcId, int questId)
        {
            if (npcId <= 0 || questId <= 0)
                return false;

            for (var questNum = 0; questNum <= MaxNpcQuestNumProbe; questNum++)
            {
                if (Asda2QuestNpc.GetQuest(npcId, questNum) == questId ||
                    Asda2QuestFallbackData.GetStarterQuestId(npcId, questNum) == questId)
                    return true;
            }

            return false;
        }

        private static bool HasTemplateStarterNpcQuest(int npcId, int questId)
        {
            if (npcId <= 0 || questId <= 0)
                return false;

            try
            {
                return Asda2QuestTemplateRecord.GetAllByStarter(npcId)
                    .Any(record => record != null && record.QuestId == questId);
            }
            catch
            {
                return false;
            }
        }

        private static bool HasRewardNpcQuest(int npcId, int questId)
        {
            if (npcId <= 0 || questId <= 0)
                return false;

            for (var questNum = 0; questNum <= MaxNpcQuestNumProbe; questNum++)
            {
                if (Asda2QuestRewardNpc.GetQuest(npcId, questNum) == questId)
                    return true;
            }

            return false;
        }

        private static bool HasAvailableNpcQuestStarter(Character chr, int npcId, uint ownerId)
        {
            if (HasAvailableUnifiedQuestStarter(chr, npcId, ownerId))
                return true;

            for (var questNum = 0; questNum <= MaxNpcQuestNumProbe; questNum++)
            {
                var questId = Asda2QuestNpc.GetQuest(npcId, questNum);
                if (questId <= 0)
                    continue;

                var template = Asda2QuestTemplateResolver.GetStandard(questId);
                if (IsQuestAvailableForNpc(chr, ownerId, questId, template))
                    return true;
            }

            if (Asda2QuestFallbackData.HasAvailableStarter(npcId, ownerId, chr.Level))
                return true;

            return false;
        }

        private static bool HasAvailableUnifiedQuestStarter(Character chr, int npcId, uint ownerId)
        {
            try
            {
                var records = Asda2QuestTemplateRecord.GetAllByStarter(npcId);
                foreach (var record in records)
                {
                    if (record == null)
                        continue;

                    var template = Asda2QuestTemplateResolver.GetStandard(record.QuestId);
                    if (IsQuestAvailableForNpc(chr, ownerId, record.QuestId, template))
                        return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static bool IsQuestAvailableForNpc(Character chr, uint ownerId, int questId,
            Asda2QuestTemplateInfo template)
        {
            if (chr == null || questId <= 0)
                return false;

            if (!IsQuestClientSafe(template, template == null ? -1 : template.FileId))
                return false;

            if (chr.Level < template.Level)
                return false;

            if (GetCompletedCount(ownerId, questId) > 0)
                return false;

            var existing = Asda2QuestProgressRecord.GetQuestRecord(ownerId, questId);
            if (existing == null)
                return true;

            if (existing.IsActive)
                return false;

            if (existing.CompleteStatus < 2)
                return true;

            return false;
        }

        private static bool IsKnownMissingClientQuestBook(int fileId)
        {
            return MissingClientQuestBookFileIds.Contains(fileId);
        }

        private static bool IsObjectiveReady(int amount, int requiredAmount)
        {
            return requiredAmount <= 0 || amount >= requiredAmount;
        }

        private static int GetRequiredAmount(Asda2QuestTemplateInfo template, int objectiveIndex)
        {
            return objectiveIndex < 1 || objectiveIndex > 5 ? 0 : template.RequiredAmounts[objectiveIndex - 1];
        }

        private static int GetObjectiveItemId(Asda2QuestProgressRecord quest, int objectiveIndex)
        {
            if (quest == null)
                return -1;

            switch (objectiveIndex)
            {
                case 1:
                    return quest.Item1Id;
                case 2:
                    return quest.Item2Id;
                case 3:
                    return quest.Item3Id;
                case 4:
                    return quest.Item4Id;
                case 5:
                    return quest.Item5Id;
                default:
                    return -1;
            }
        }

        private static int GetObjectiveMonsterId(Asda2QuestProgressRecord quest, int objectiveIndex)
        {
            if (quest == null)
                return -1;

            switch (objectiveIndex)
            {
                case 1:
                    return quest.Monster1;
                case 2:
                    return quest.Monster2;
                case 3:
                    return quest.Monster3;
                case 4:
                    return quest.Monster4;
                case 5:
                    return quest.Monster5;
                default:
                    return -1;
            }
        }

        private static int GetObjectiveAmount(Asda2QuestProgressRecord quest, int objectiveIndex)
        {
            switch (objectiveIndex)
            {
                case 1:
                    return quest.Item1Amount;
                case 2:
                    return quest.Item2Amount;
                case 3:
                    return quest.Item3Amount;
                case 4:
                    return quest.Item4Amount;
                case 5:
                    return quest.Item5Amount;
                default:
                    return 0;
            }
        }

        private static void SetObjectiveAmount(Asda2QuestProgressRecord quest, int objectiveIndex, int amount)
        {
            switch (objectiveIndex)
            {
                case 1:
                    quest.Item1Amount = amount;
                    break;
                case 2:
                    quest.Item2Amount = amount;
                    break;
                case 3:
                    quest.Item3Amount = amount;
                    break;
                case 4:
                    quest.Item4Amount = amount;
                    break;
                case 5:
                    quest.Item5Amount = amount;
                    break;
            }
        }
    }
}
