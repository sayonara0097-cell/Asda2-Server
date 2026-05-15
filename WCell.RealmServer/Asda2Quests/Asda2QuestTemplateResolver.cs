using WCell.RealmServer.Database;

namespace WCell.RealmServer.Asda2Quests
{
    internal sealed class Asda2QuestTemplateInfo
    {
        public int Id;
        public int FileId = -1;
        public int Level;
        public int QuestType;
        public bool IsBulletin;
        public int StartNpcId = -1;
        public int StartQuestNum = -1;
        public int CompleteNpcId = -1;
        public int CompleteQuestNum = -1;
        public int Gold;
        public int Exp;
        public int RepeatCount;
        public readonly int[] ItemIds = new int[5];
        public readonly int[] ItemChances = new int[5];
        public readonly int[] InitialAmounts = new int[5];
        public readonly int[] RequiredAmounts = new int[5];
        public readonly int[] MonsterIds = new int[5];
        public readonly int[] RewardIds = new int[5];
        public readonly int[] RewardAmounts = new int[5];
        public readonly bool[] RewardOptional = new bool[5];
        public readonly int[] HiddenItemIds = new int[3];
        public readonly int[] HiddenItemAmounts = new int[3];
        public readonly int[] HiddenItemGivers = new int[3];
        public readonly int[] StartItemIds = new int[3];
        public int XpPerItem;
        public int AfterStage = 1;
        public int AfterComplete = 1;
        public bool DoSort = true;
        public int SeqId = -1;
        public int InitState;
    }

    internal static class Asda2QuestTemplateResolver
    {
        private static bool _useUnifiedQuestTemplate = true;

        public static Asda2QuestTemplateInfo Get(int questId)
        {
            return GetStandard(questId) ?? GetBulletin(questId);
        }

        public static Asda2QuestTemplateInfo GetStandard(int questId)
        {
            var unifiedTemplate = GetUnifiedByQuestId(questId);
            if (unifiedTemplate != null)
                return unifiedTemplate;

            try
            {
                var record = Asda2QuestRecord.GetRecordByID(questId);
                if (record != null)
                {
                    return CreateTemplate((int)record.Id, record.Level, record.QuestType, false,
                        record.Item1Id, record.Item2Id, record.Item3Id, record.Item4Id, record.Item5Id,
                        record.Item1Amount, record.Item2Amount, record.Item3Amount, record.Item4Amount, record.Item5Amount,
                        record.Item1ReqAmount, record.Item2ReqAmount, record.Item3ReqAmount, record.Item4ReqAmount, record.Item5ReqAmount,
                        record.Monster1Id, record.Monster2Id, record.Monster3Id, record.Monster4Id, record.Monster5Id,
                        -1, -1, -1, -1, -1, record.Gold, record.Exp);
                }
            }
            catch
            {
            }

            try
            {
                var info = Asda2QuestInfo.GetQuestInfoByQuestId(questId);
                if (info == null)
                    return Asda2QuestFallbackData.Get(questId);

                return CreateTemplate(info.questid, info.questlvl, 0, false,
                    info.questItemId1, info.questItemId2, info.questItemId3, info.questItemId4, info.questItemId5,
                    0, 0, 0, 0, 0,
                    info.questItemAmount1, info.questItemAmount2, info.questItemAmount3, info.questItemAmount4, info.questItemAmount5,
                    0, 0, 0, 0, 0);
            }
            catch
            {
                return Asda2QuestFallbackData.Get(questId);
            }
        }

        public static Asda2QuestTemplateInfo GetStandardByStarter(int npcId, int questNum)
        {
            if (!_useUnifiedQuestTemplate)
                return Asda2QuestFallbackData.GetByStarter(npcId, questNum);

            try
            {
                var record = Asda2QuestTemplateRecord.GetByStarter(npcId, questNum);
                return FromUnifiedRecord(record) ?? Asda2QuestFallbackData.GetByStarter(npcId, questNum);
            }
            catch
            {
                _useUnifiedQuestTemplate = false;
                return Asda2QuestFallbackData.GetByStarter(npcId, questNum);
            }
        }

        public static Asda2QuestTemplateInfo GetStandardByCompleter(int npcId, int questNum)
        {
            if (!_useUnifiedQuestTemplate)
                return Asda2QuestFallbackData.GetByCompleter(npcId, questNum);

            try
            {
                var record = Asda2QuestTemplateRecord.GetByCompleter(npcId, questNum);
                return FromUnifiedRecord(record) ?? Asda2QuestFallbackData.GetByCompleter(npcId, questNum);
            }
            catch
            {
                _useUnifiedQuestTemplate = false;
                return Asda2QuestFallbackData.GetByCompleter(npcId, questNum);
            }
        }

        public static Asda2QuestTemplateInfo GetBulletin(int questId)
        {
            try
            {
                var record = Asda2BBQuestRecord.GetRecordByID(questId);
                if (record == null)
                    return null;

                return CreateTemplate((int)record.Id, record.StartLevel, 1, true,
                    record.Item1Id, record.Item2Id, record.Item3Id, record.Item4Id, record.Item5Id,
                    record.Item1Amount, record.Item2Amount, record.Item3Amount, record.Item4Amount, record.Item5Amount,
                    record.Item1ReqAmount, record.Item2ReqAmount, record.Item3ReqAmount, record.Item4ReqAmount, record.Item5ReqAmount,
                    record.Monster1Id, record.Monster2Id, record.Monster3Id, record.Monster4Id, record.Monster5Id);
            }
            catch
            {
                return null;
            }
        }

        private static Asda2QuestTemplateInfo CreateTemplate(int id, int level, int questType, bool isBulletin,
            int item1, int item2, int item3, int item4, int item5,
            int amount1, int amount2, int amount3, int amount4, int amount5,
            int required1, int required2, int required3, int required4, int required5,
            int monster1, int monster2, int monster3, int monster4, int monster5,
            int fileId = -1, int startNpcId = -1, int startQuestNum = -1,
            int completeNpcId = -1, int completeQuestNum = -1,
            int gold = 0, int exp = 0, int repeatCount = 0,
            int reward1 = -1, int reward1Amount = 0, int reward2 = -1, int reward2Amount = 0,
            int reward3 = -1, int reward3Amount = 0, int reward4 = -1, int reward4Amount = 0,
            int reward5 = -1, int reward5Amount = 0)
        {
            var info = new Asda2QuestTemplateInfo
            {
                Id = id,
                FileId = fileId,
                Level = level,
                QuestType = questType,
                IsBulletin = isBulletin,
                StartNpcId = startNpcId,
                StartQuestNum = startQuestNum,
                CompleteNpcId = completeNpcId,
                CompleteQuestNum = completeQuestNum,
                Gold = gold,
                Exp = exp,
                RepeatCount = repeatCount
            };

            info.ItemIds[0] = NormalizeItemId(item1);
            info.ItemIds[1] = NormalizeItemId(item2);
            info.ItemIds[2] = NormalizeItemId(item3);
            info.ItemIds[3] = NormalizeItemId(item4);
            info.ItemIds[4] = NormalizeItemId(item5);

            info.InitialAmounts[0] = amount1;
            info.InitialAmounts[1] = amount2;
            info.InitialAmounts[2] = amount3;
            info.InitialAmounts[3] = amount4;
            info.InitialAmounts[4] = amount5;

            info.RequiredAmounts[0] = required1;
            info.RequiredAmounts[1] = required2;
            info.RequiredAmounts[2] = required3;
            info.RequiredAmounts[3] = required4;
            info.RequiredAmounts[4] = required5;

            info.MonsterIds[0] = monster1;
            info.MonsterIds[1] = monster2;
            info.MonsterIds[2] = monster3;
            info.MonsterIds[3] = monster4;
            info.MonsterIds[4] = monster5;

            info.RewardIds[0] = NormalizeItemId(reward1);
            info.RewardIds[1] = NormalizeItemId(reward2);
            info.RewardIds[2] = NormalizeItemId(reward3);
            info.RewardIds[3] = NormalizeItemId(reward4);
            info.RewardIds[4] = NormalizeItemId(reward5);

            info.RewardAmounts[0] = reward1Amount;
            info.RewardAmounts[1] = reward2Amount;
            info.RewardAmounts[2] = reward3Amount;
            info.RewardAmounts[3] = reward4Amount;
            info.RewardAmounts[4] = reward5Amount;

            for (var index = 0; index < info.ItemChances.Length; index++)
                info.ItemChances[index] = 100;
            for (var index = 0; index < info.HiddenItemIds.Length; index++)
            {
                info.HiddenItemIds[index] = -1;
                info.HiddenItemGivers[index] = -1;
            }
            for (var index = 0; index < info.StartItemIds.Length; index++)
                info.StartItemIds[index] = -1;

            return info;
        }

        private static Asda2QuestTemplateInfo GetUnifiedByQuestId(int questId)
        {
            if (!_useUnifiedQuestTemplate)
                return Asda2QuestFallbackData.Get(questId);

            try
            {
                return FromUnifiedRecord(Asda2QuestTemplateRecord.GetByQuestId(questId)) ??
                       Asda2QuestFallbackData.Get(questId);
            }
            catch
            {
                _useUnifiedQuestTemplate = false;
                return Asda2QuestFallbackData.Get(questId);
            }
        }

        private static Asda2QuestTemplateInfo FromUnifiedRecord(Asda2QuestTemplateRecord record)
        {
            if (record == null)
                return null;

            var info = CreateTemplate(record.QuestId, record.Level, 1, false,
                record.Item1, record.Item2, record.Item3, record.Item4, record.Item5,
                0, 0, 0, 0, 0,
                record.Item1Amount, record.Item2Amount, record.Item3Amount, record.Item4Amount, record.Item5Amount,
                record.Monster1, record.Monster2, record.Monster3, record.Monster4, record.Monster5,
                record.FileId, record.NpcId, record.QuestNum, record.CompleteNpcId, record.CompleteQuestNum,
                record.Gold, record.Exp, record.RepeatCount,
                record.Reward1, record.Reward1Amount, record.Reward2, record.Reward2Amount,
                record.Reward3, record.Reward3Amount, record.Reward4, record.Reward4Amount,
                record.Reward5, record.Reward5Amount);

            info.ItemChances[0] = NormalizeChance(record.Item1Chance);
            info.ItemChances[1] = NormalizeChance(record.Item2Chance);
            info.ItemChances[2] = NormalizeChance(record.Item3Chance);
            info.ItemChances[3] = NormalizeChance(record.Item4Chance);
            info.ItemChances[4] = NormalizeChance(record.Item5Chance);

            info.RewardOptional[0] = record.Reward1OP != 0;
            info.RewardOptional[1] = record.Reward2OP != 0;
            info.RewardOptional[2] = record.Reward3OP != 0;
            info.RewardOptional[3] = record.Reward4OP != 0;
            info.RewardOptional[4] = record.Reward5OP != 0;

            info.HiddenItemIds[0] = NormalizeItemId(record.HiddenItem1);
            info.HiddenItemIds[1] = NormalizeItemId(record.HiddenItem2);
            info.HiddenItemIds[2] = NormalizeItemId(record.HiddenItem3);
            info.HiddenItemAmounts[0] = record.HiddenItem1Amount;
            info.HiddenItemAmounts[1] = record.HiddenItem2Amount;
            info.HiddenItemAmounts[2] = record.HiddenItem3Amount;
            info.HiddenItemGivers[0] = record.HiddenItem1Giver;
            info.HiddenItemGivers[1] = record.HiddenItem2Giver;
            info.HiddenItemGivers[2] = record.HiddenItem3Giver;

            info.StartItemIds[0] = NormalizeItemId(record.StartItem1);
            info.StartItemIds[1] = NormalizeItemId(record.StartItem2);
            info.StartItemIds[2] = NormalizeItemId(record.StartItem3);
            info.XpPerItem = record.XpPerItem;
            info.AfterStage = record.AfterStage <= 0 ? 1 : record.AfterStage;
            info.AfterComplete = record.AfterComplete <= 0 ? 1 : record.AfterComplete;
            info.DoSort = record.DoSort != 0;
            info.SeqId = record.SeqId;
            info.InitState = record.InitState;

            return info;
        }

        private static int NormalizeItemId(int itemId)
        {
            return itemId <= 0 ? -1 : itemId;
        }

        private static int NormalizeChance(int chance)
        {
            if (chance <= 0)
                return 0;
            return chance > 100 ? 100 : chance;
        }
    }
}
