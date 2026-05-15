using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using WCell.Core;
using WCell.Core.Network;
using WCell.RealmServer.Database;

namespace WCell.RealmServer.Asda2Quests
{
    internal static class Asda2QuestFallbackData
    {
        private static readonly object LoadLock = new object();
        private static bool _loaded;
        private static Dictionary<int, FallbackQuestRecord> _recordsByQuestId;
        private static Dictionary<string, int> _starterQuestIds;
        private static Dictionary<string, int> _completerQuestIds;
        private static Dictionary<int, List<int>> _starterQuestIdsByNpc;
        private static Dictionary<int, List<int>> _completerQuestIdsByNpc;

        private static readonly byte[] ClientDataKey = new byte[]
        {
            0x3E, 0xC6, 0xC4, 0xFC, 0xA9, 0x31, 0xE7, 0xE5, 0x5E, 0x34, 0x5A, 0xE2, 0x1D, 0xB8, 0xBA, 0xEF,
            0x5D, 0x40, 0xA8, 0xE4, 0x6D, 0x27, 0xA1, 0x17, 0xF9, 0x62, 0xEC, 0xC5, 0x56, 0x66, 0x06, 0xDE,
            0xA3, 0xE0, 0x1C, 0xAE, 0x4B, 0x85, 0x48, 0xFA, 0xFC, 0x25, 0x15, 0x25, 0x67, 0xF3, 0x0E, 0x9D,
            0x63, 0x1A, 0x33, 0x7E, 0xCD, 0x2D, 0xFF, 0x50, 0x10, 0x1E, 0x64, 0x48, 0x83, 0xE4, 0xFE, 0xD9,
            0xDA, 0x9C, 0x28, 0xC9, 0x60, 0xF1, 0x7E, 0xF0, 0x20, 0xED, 0xB8, 0x3F, 0x98, 0x78, 0x9F, 0xA2,
            0x7B, 0xA8, 0x4A, 0x7C, 0xDC, 0x07, 0xFD, 0x5B, 0xA7, 0x77, 0xAC, 0x73, 0x91, 0xE9, 0xDF, 0xD5,
            0x9E, 0xAB, 0xCC, 0x6F, 0xE1, 0x59, 0x4D, 0x96, 0xB4, 0x42, 0x77, 0x0B, 0xAC, 0xC3, 0xFB, 0x08,
            0xBF, 0x88, 0x3E, 0x02, 0xA0, 0x5A, 0x49, 0x9B, 0xA5, 0x81, 0x8C, 0x00, 0xF4, 0x30, 0xA6, 0x28,
            0xCE, 0xB6, 0x58, 0x10, 0x68, 0x55, 0x01, 0x90, 0x02, 0x97, 0x5E, 0xED, 0xC5, 0x8E, 0x54, 0xE2,
            0x4A, 0xF2, 0x6C, 0xB0, 0xEA, 0xCA, 0x12, 0xD5, 0x39, 0x00, 0x3C, 0xA7, 0x3A, 0x86, 0x61, 0xEF,
            0x76, 0xB2, 0xCB, 0x2E, 0x41, 0x65, 0x0C, 0xBD, 0xD8, 0xD1, 0x66, 0x0E, 0x80, 0x7D, 0x40, 0xC9,
            0x2F, 0xC8, 0x0C, 0xA4, 0x17, 0x9A, 0xD4, 0xAA, 0x24, 0x8D, 0x85, 0x8D, 0x51, 0xD6, 0x95, 0xC0,
            0xCB, 0xA4, 0xDB, 0x20, 0xDB, 0x52, 0x89, 0xF6, 0x22, 0x3F, 0x60, 0x75, 0x5F, 0xC7, 0x4F, 0x44,
            0x14, 0x3B, 0xF0, 0x98, 0x6B, 0x93, 0xB0, 0xCC, 0xAF, 0x29, 0xC3, 0xB4, 0xC2, 0xCF, 0xF5, 0x8C,
            0x05, 0x82, 0x88, 0x90, 0x37, 0x01, 0xC1, 0x92, 0x45, 0xB5, 0x87, 0xD4, 0x09, 0xAE, 0x94, 0x47,
            0xD2, 0x26, 0xBE, 0x16, 0x1B, 0x74, 0x79, 0x32, 0x9F, 0x8B, 0x22, 0xD0, 0xBC, 0x4F, 0x6E, 0x0F
        };

        public static Asda2QuestTemplateInfo Get(int questId)
        {
            EnsureLoaded();
            FallbackQuestRecord record;
            return _recordsByQuestId.TryGetValue(questId, out record) ? CreateTemplate(record) : null;
        }

        public static Asda2QuestTemplateInfo GetByStarter(int npcId, int questNum)
        {
            var questId = GetStarterQuestId(npcId, questNum);
            return questId <= 0 ? null : Get(questId);
        }

        public static Asda2QuestTemplateInfo GetByCompleter(int npcId, int questNum)
        {
            var questId = GetCompleterQuestId(npcId, questNum);
            return questId <= 0 ? null : Get(questId);
        }

        public static int GetStarterQuestId(int npcId, int questNum)
        {
            EnsureLoaded();
            int questId;
            return _starterQuestIds.TryGetValue(GetNpcQuestKey(npcId, questNum), out questId) ? questId : 0;
        }

        public static int GetStarterQuestIdForCharacter(int npcId, int questNum, uint ownerId, int characterLevel)
        {
            EnsureLoaded();

            var questId = GetStarterQuestId(npcId, questNum);
            if (questId > 0 && IsAvailableForCharacter(ownerId, characterLevel, questId))
                return questId;

            List<int> questIds;
            if (!_starterQuestIdsByNpc.TryGetValue(npcId, out questIds))
                return 0;

            var availableQuestIds = new List<int>();
            foreach (var candidateQuestId in questIds)
            {
                FallbackQuestRecord record;
                if (!_recordsByQuestId.TryGetValue(candidateQuestId, out record))
                    continue;
                if (record.Level > characterLevel)
                    continue;
                if (!IsAvailable(ownerId, record))
                    continue;

                availableQuestIds.Add(candidateQuestId);
            }

            if (questNum >= 0 && questNum < availableQuestIds.Count)
                return availableQuestIds[questNum];
            if (questNum > 0 && questNum - 1 < availableQuestIds.Count)
                return availableQuestIds[questNum - 1];

            return 0;
        }

        private static bool IsAvailableForCharacter(uint ownerId, int characterLevel, int questId)
        {
            FallbackQuestRecord record;
            return _recordsByQuestId.TryGetValue(questId, out record) &&
                   record.Level <= characterLevel &&
                   IsAvailable(ownerId, record);
        }

        public static int GetCompleterQuestId(int npcId, int questNum)
        {
            EnsureLoaded();
            int questId;
            return _completerQuestIds.TryGetValue(GetNpcQuestKey(npcId, questNum), out questId) ? questId : 0;
        }

        public static bool HasCompleter(int npcId, int questId)
        {
            EnsureLoaded();
            List<int> questIds;
            return _completerQuestIdsByNpc.TryGetValue(npcId, out questIds) && questIds.Contains(questId);
        }

        public static bool HasAvailableStarter(int npcId, uint ownerId, int characterLevel)
        {
            EnsureLoaded();
            List<int> questIds;
            if (!_starterQuestIdsByNpc.TryGetValue(npcId, out questIds))
                return false;

            foreach (var questId in questIds)
            {
                FallbackQuestRecord record;
                if (!_recordsByQuestId.TryGetValue(questId, out record))
                    continue;
                if (record.Level > characterLevel)
                    continue;
                if (!IsAvailable(ownerId, record))
                    continue;
                return true;
            }

            return false;
        }

        public static Asda2QuestTemplateInfo[] GetAllByStarter(int npcId)
        {
            EnsureLoaded();
            List<int> questIds;
            if (!_starterQuestIdsByNpc.TryGetValue(npcId, out questIds))
                return new Asda2QuestTemplateInfo[0];

            var templates = new List<Asda2QuestTemplateInfo>();
            foreach (var questId in questIds)
            {
                var template = Get(questId);
                if (template != null)
                    templates.Add(template);
            }
            return templates.ToArray();
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
                return;

            lock (LoadLock)
            {
                if (_loaded)
                    return;

                var records = new Dictionary<int, FallbackQuestRecord>();
                var starterQuestIds = new Dictionary<string, int>();
                var completerQuestIds = new Dictionary<string, int>();
                var clientStarterQuestIdsByNpc = new Dictionary<int, List<int>>();
                var clientCompleterQuestIdsByNpc = new Dictionary<int, List<int>>();

                foreach (var sqlFile in FindSqlFiles())
                    LoadSqlFile(sqlFile, records, starterQuestIds, completerQuestIds);

                LoadClientQuestData(records, clientStarterQuestIdsByNpc, clientCompleterQuestIdsByNpc);
                BuildNpcIndexes(records, starterQuestIds, completerQuestIds,
                    clientStarterQuestIdsByNpc, clientCompleterQuestIdsByNpc);

                _recordsByQuestId = records;
                _starterQuestIds = starterQuestIds;
                _completerQuestIds = completerQuestIds;
                _loaded = true;
            }
        }

        private static bool IsAvailable(uint ownerId, FallbackQuestRecord record)
        {
            var completedCount = Asda2QuestMgr.GetCompletedCount(ownerId, record.QuestId);
            if (completedCount > 0)
                return false;

            var existing = Asda2QuestProgressRecord.GetQuestRecord(ownerId, record.QuestId);
            if (existing == null)
                return true;
            if (existing.IsActive)
                return false;
            return existing.CompleteStatus < 2;
        }

        private static void BuildNpcIndexes(Dictionary<int, FallbackQuestRecord> records,
            Dictionary<string, int> starterQuestIds, Dictionary<string, int> completerQuestIds,
            Dictionary<int, List<int>> clientStarterQuestIdsByNpc,
            Dictionary<int, List<int>> clientCompleterQuestIdsByNpc)
        {
            _starterQuestIdsByNpc = new Dictionary<int, List<int>>();
            foreach (var pair in clientStarterQuestIdsByNpc)
                AddNpcIndexRange(_starterQuestIdsByNpc, pair.Key, pair.Value);
            foreach (var pair in starterQuestIds)
                AddNpcIndex(_starterQuestIdsByNpc, GetNpcIdFromKey(pair.Key), pair.Value);

            _completerQuestIdsByNpc = new Dictionary<int, List<int>>();
            foreach (var pair in clientCompleterQuestIdsByNpc)
                AddNpcIndexRange(_completerQuestIdsByNpc, pair.Key, pair.Value);
            foreach (var pair in completerQuestIds)
                AddNpcIndex(_completerQuestIdsByNpc, GetNpcIdFromKey(pair.Key), pair.Value);

            foreach (var pair in records)
            {
                var record = pair.Value;
                if (record.StartNpcId > 0)
                    AddNpcIndex(_starterQuestIdsByNpc, record.StartNpcId, record.QuestId);
                if (record.CompleteNpcId > 0)
                AddNpcIndex(_completerQuestIdsByNpc, record.CompleteNpcId, record.QuestId);
            }
        }

        private static void AddNpcIndexRange(Dictionary<int, List<int>> index, int npcId, List<int> questIds)
        {
            if (questIds == null)
                return;

            foreach (var questId in questIds)
                AddNpcIndex(index, npcId, questId);
        }

        private static void AddNpcIndex(Dictionary<int, List<int>> index, int npcId, int questId)
        {
            if (npcId <= 0 || questId <= 0)
                return;

            List<int> questIds;
            if (!index.TryGetValue(npcId, out questIds))
            {
                questIds = new List<int>();
                index[npcId] = questIds;
            }
            if (!questIds.Contains(questId))
                questIds.Add(questId);
        }

        private static void LoadSqlFile(string path, Dictionary<int, FallbackQuestRecord> records,
            Dictionary<string, int> starterQuestIds, Dictionary<string, int> completerQuestIds)
        {
            try
            {
                foreach (var line in File.ReadLines(path))
                {
                    if (line.IndexOf("INSERT INTO `asda2questnpc` VALUES", StringComparison.OrdinalIgnoreCase) >= 0)
                        LoadQuestNpc(line, records, starterQuestIds, true);
                    else if (line.IndexOf("INSERT INTO `asda2questrewardnpc` VALUES", StringComparison.OrdinalIgnoreCase) >= 0)
                        LoadQuestNpc(line, records, completerQuestIds, false);
                    else if (line.IndexOf("INSERT INTO `asda2questrecord` VALUES", StringComparison.OrdinalIgnoreCase) >= 0)
                        LoadQuestRecord(line, records);
                    else if (line.IndexOf("INSERT INTO `asda2questrewardtable` VALUES", StringComparison.OrdinalIgnoreCase) >= 0)
                        LoadLegacyReward(line, records);
                }
            }
            catch
            {
            }
        }

        private static void LoadQuestNpc(string line, Dictionary<int, FallbackQuestRecord> records,
            Dictionary<string, int> npcQuestIds, bool isStarter)
        {
            var values = SplitSqlValues(line);
            if (values.Count < 5)
                return;

            var npcId = ToInt(values[1]);
            var questNum = ToInt(values[2]);
            var questId = ToInt(values[3]);
            if (npcId <= 0 || questId <= 0)
                return;

            npcQuestIds[GetNpcQuestKey(npcId, questNum)] = questId;

            var record = GetOrCreate(records, questId);
            if (isStarter)
            {
                if (record.StartNpcId <= 0 || record.StartQuestNum < 0)
                {
                    record.StartNpcId = npcId;
                    record.StartQuestNum = questNum;
                }
            }
            else if (record.CompleteNpcId <= 0 || record.CompleteQuestNum < 0)
            {
                record.CompleteNpcId = npcId;
                record.CompleteQuestNum = questNum;
            }

            if (string.IsNullOrEmpty(record.Name))
                record.Name = Unquote(values[4]);
        }

        private static void LoadQuestRecord(string line, Dictionary<int, FallbackQuestRecord> records)
        {
            var values = SplitSqlValues(line);
            if (values.Count < 26)
                return;

            var questId = ToInt(values[1]);
            if (questId <= 0)
                return;

            var record = GetOrCreate(records, questId);
            record.Name = Unquote(values[0]);
            record.Level = ToInt(values[2]);
            record.Gold = ToInt(values[3]);
            record.Exp = ToInt(values[4]);

            for (var index = 0; index < 5; index++)
                record.MonsterIds[index] = ToInt(values[5 + index]);

            for (var index = 0; index < 5; index++)
            {
                record.ItemIds[index] = ToInt(values[10 + index * 2]);
                record.InitialAmounts[index] = ToInt(values[11 + index * 2]);
            }

            for (var index = 0; index < 5; index++)
                record.RequiredAmounts[index] = ToInt(values[20 + index]);

            record.QuestType = ToInt(values[25]);
        }

        private static void LoadLegacyReward(string line, Dictionary<int, FallbackQuestRecord> records)
        {
            var values = SplitSqlValues(line);
            if (values.Count < 11)
                return;

            var questId = ToInt(values[0]);
            if (questId <= 0)
                return;

            var record = GetOrCreate(records, questId);
            record.LegacyGold = ToInt(values[1]);
            record.LegacyExp = ToInt(values[2]);
            for (var index = 0; index < 4; index++)
            {
                record.LegacyRewardIds[index] = ToInt(values[3 + index * 2]);
                record.LegacyRewardAmounts[index] = ToInt(values[4 + index * 2]);
            }
        }

        private static void LoadClientQuestData(Dictionary<int, FallbackQuestRecord> records,
            Dictionary<int, List<int>> clientStarterQuestIdsByNpc,
            Dictionary<int, List<int>> clientCompleterQuestIdsByNpc)
        {
            var clientRoot = FindClientRoot();
            if (clientRoot == null)
            {
                ApplyLegacyRewards(records);
                return;
            }

            try
            {
                var episodePath = Path.Combine(clientRoot, "data", "NPC", "Episode", "Episode_Table.BIN");
                if (File.Exists(episodePath))
                    LoadEpisodeTable(DecodeClientFile(episodePath), records);

                var rewardPath = Path.Combine(clientRoot, "data", "NPC", "RewardTable.BIN");
                if (File.Exists(rewardPath))
                    LoadClientRewardTable(DecodeClientFile(rewardPath), records);

                LoadClientNpcQuestLinks(clientRoot, records, clientStarterQuestIdsByNpc, clientCompleterQuestIdsByNpc);
            }
            catch
            {
            }

            ApplyLegacyRewards(records);
        }

        private static void LoadClientNpcQuestLinks(string clientRoot, Dictionary<int, FallbackQuestRecord> records,
            Dictionary<int, List<int>> clientStarterQuestIdsByNpc,
            Dictionary<int, List<int>> clientCompleterQuestIdsByNpc)
        {
            var nsrPath = Path.Combine(clientRoot, "data", "NPC", "Episode", "NPC", "Nsr.bsz");
            if (!File.Exists(nsrPath))
                return;

            var recordsByFileId = BuildRecordsByFileId(records);
            if (recordsByFileId.Count == 0)
                return;

            try
            {
                using (var stream = File.OpenRead(nsrPath))
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (entry == null || !entry.FullName.EndsWith(".nsr", StringComparison.OrdinalIgnoreCase))
                            continue;

                        using (var entryStream = entry.Open())
                        using (var memory = new MemoryStream())
                        {
                            entryStream.CopyTo(memory);
                            LoadClientNpcQuestLinksFromNsr(DecodeClientBuffer(memory.ToArray()), recordsByFileId,
                                clientStarterQuestIdsByNpc, clientCompleterQuestIdsByNpc);
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private static Dictionary<int, FallbackQuestRecord> BuildRecordsByFileId(
            Dictionary<int, FallbackQuestRecord> records)
        {
            var recordsByFileId = new Dictionary<int, FallbackQuestRecord>();
            foreach (var pair in records)
            {
                var record = pair.Value;
                if (record.FileId >= 0 && !recordsByFileId.ContainsKey(record.FileId))
                    recordsByFileId[record.FileId] = record;
            }
            return recordsByFileId;
        }

        private static void LoadClientNpcQuestLinksFromNsr(byte[] data,
            Dictionary<int, FallbackQuestRecord> recordsByFileId,
            Dictionary<int, List<int>> clientStarterQuestIdsByNpc,
            Dictionary<int, List<int>> clientCompleterQuestIdsByNpc)
        {
            if (data == null || data.Length < 176)
                return;

            var npcId = ReadInt32(data, 4);
            var recordCount = ReadInt32(data, 40);
            const int tableOffset = 176;
            const int recordSize = 120;
            if (npcId <= 0 || recordCount <= 0 || recordCount > 128 ||
                tableOffset + recordCount * recordSize > data.Length)
                return;

            var starterIndex = 0;
            var completerIndex = 0;
            for (var index = 0; index < recordCount; index++)
            {
                var offset = tableOffset + index * recordSize;
                var kind = ReadInt16(data, offset + 6);
                var status = ReadInt16(data, offset + 10);

                FallbackQuestRecord record;
                if (!TryFindQuestRecordInClientNpcRecord(data, offset, recordSize, recordsByFileId, out record))
                    continue;

                if (status == 150 && kind == 2)
                {
                    AddNpcIndex(clientStarterQuestIdsByNpc, npcId, record.QuestId);
                    if (record.StartNpcId <= 0)
                    {
                        record.StartNpcId = npcId;
                        record.StartQuestNum = starterIndex;
                    }
                    starterIndex++;
                }
                else if ((status == 111 || status == 118) && kind == 2)
                {
                    AddNpcIndex(clientCompleterQuestIdsByNpc, npcId, record.QuestId);
                    if (record.CompleteNpcId <= 0)
                    {
                        record.CompleteNpcId = npcId;
                        record.CompleteQuestNum = completerIndex;
                    }
                    completerIndex++;
                }
            }
        }

        private static bool TryFindQuestRecordInClientNpcRecord(byte[] data, int offset, int recordSize,
            Dictionary<int, FallbackQuestRecord> recordsByFileId, out FallbackQuestRecord record)
        {
            record = null;
            var fileId = ReadInt32(data, offset + 24);
            return fileId > 0 && recordsByFileId.TryGetValue(fileId, out record);
        }

        private static void LoadEpisodeTable(byte[] data, Dictionary<int, FallbackQuestRecord> records)
        {
            if (data == null || data.Length < 20)
                return;

            var count = ReadInt32(data, 4);
            if (count <= 0)
                return;

            var recordSize = (data.Length - 20) / count;
            if (recordSize < 248)
                return;

            for (var index = 0; index < count; index++)
            {
                var offset = 20 + index * recordSize;
                var questId = ReadInt32(data, offset + 200);
                if (questId <= 0)
                    continue;

                var record = GetOrCreate(records, questId);
                record.FileId = ReadInt32(data, offset + 236);
                record.Level = ReadInt32(data, offset + 240);
                var name = Asda2EncodingHelper.Decode(GetBytes(data, offset, 200), Locale.Ar).TrimEnd('\0', ' ');
                if (!string.IsNullOrEmpty(name))
                    record.Name = name;
            }
        }

        private static void LoadClientRewardTable(byte[] data, Dictionary<int, FallbackQuestRecord> records)
        {
            if (data == null || data.Length < 20)
                return;

            var count = ReadInt32(data, 4);
            if (count <= 0)
                return;

            var recordsByFileId = new Dictionary<int, FallbackQuestRecord>();
            foreach (var pair in records)
            {
                var record = pair.Value;
                if (record.FileId >= 0 && !recordsByFileId.ContainsKey(record.FileId))
                    recordsByFileId[record.FileId] = record;
            }

            var recordSize = (data.Length - 20) / count;
            if (recordSize < 92)
                return;

            for (var index = 0; index < count; index++)
            {
                var offset = 20 + index * recordSize;
                var fileId = ReadInt32(data, offset + 4);
                FallbackQuestRecord record;
                if (!recordsByFileId.TryGetValue(fileId, out record))
                    continue;

                record.Gold = ReadInt32(data, offset + 16);
                record.Exp = ReadInt32(data, offset + 20);
                record.HasClientReward = true;
                SetReward(record, 0, ReadInt32(data, offset + 52), ReadInt32(data, offset + 56));
                SetReward(record, 1, ReadInt32(data, offset + 60), ReadInt32(data, offset + 64));
                SetReward(record, 2, ReadInt32(data, offset + 68), ReadInt32(data, offset + 72));
                SetReward(record, 3, ReadInt32(data, offset + 80), ReadInt32(data, offset + 84));
            }
        }

        private static void ApplyLegacyRewards(Dictionary<int, FallbackQuestRecord> records)
        {
            foreach (var pair in records)
            {
                var record = pair.Value;
                if (record.HasClientReward)
                    continue;

                if (record.LegacyGold != int.MinValue)
                    record.Gold = record.LegacyGold;
                if (record.LegacyExp != int.MinValue)
                    record.Exp = record.LegacyExp;
                for (var index = 0; index < record.RewardIds.Length; index++)
                {
                    if (record.LegacyRewardIds[index] != int.MinValue)
                    {
                        record.RewardIds[index] = record.LegacyRewardIds[index];
                        record.RewardAmounts[index] = record.LegacyRewardAmounts[index];
                    }
                }
            }
        }

        private static void SetReward(FallbackQuestRecord record, int index, int itemId, int amount)
        {
            if (index < 0 || index >= record.RewardIds.Length)
                return;

            record.RewardIds[index] = itemId;
            record.RewardAmounts[index] = itemId <= 0 ? 0 : amount;
        }

        private static FallbackQuestRecord GetOrCreate(Dictionary<int, FallbackQuestRecord> records, int questId)
        {
            FallbackQuestRecord record;
            if (records.TryGetValue(questId, out record))
                return record;

            record = new FallbackQuestRecord(questId)
            {
                FileId = GetQuestFileId(questId),
                StartNpcId = -1,
                StartQuestNum = -1,
                CompleteNpcId = -1,
                CompleteQuestNum = -1,
                QuestType = 1,
                RepeatCount = 0
            };
            records[questId] = record;
            return record;
        }

        private static Asda2QuestTemplateInfo CreateTemplate(FallbackQuestRecord record)
        {
            var info = new Asda2QuestTemplateInfo
            {
                Id = record.QuestId,
                FileId = record.FileId,
                Level = record.Level,
                QuestType = record.QuestType,
                IsBulletin = false,
                StartNpcId = record.StartNpcId,
                StartQuestNum = record.StartQuestNum,
                CompleteNpcId = record.CompleteNpcId,
                CompleteQuestNum = record.CompleteQuestNum,
                Gold = record.Gold,
                Exp = record.Exp,
                RepeatCount = record.RepeatCount,
                AfterStage = 1,
                AfterComplete = 1,
                DoSort = true,
                SeqId = -1
            };

            for (var index = 0; index < 5; index++)
            {
                info.MonsterIds[index] = record.MonsterIds[index];
                info.ItemIds[index] = NormalizeItemId(record.ItemIds[index]);
                info.InitialAmounts[index] = record.InitialAmounts[index];
                info.RequiredAmounts[index] = record.RequiredAmounts[index];
                info.ItemChances[index] = 100;
                info.RewardIds[index] = NormalizeItemId(record.RewardIds[index]);
                info.RewardAmounts[index] = record.RewardAmounts[index];
                info.RewardOptional[index] = false;
            }

            for (var index = 0; index < info.HiddenItemIds.Length; index++)
            {
                info.HiddenItemIds[index] = -1;
                info.HiddenItemGivers[index] = -1;
            }
            for (var index = 0; index < info.StartItemIds.Length; index++)
                info.StartItemIds[index] = -1;

            return info;
        }

        private static int NormalizeItemId(int itemId)
        {
            return itemId <= 0 ? -1 : itemId;
        }

        private static byte[] DecodeClientFile(string path)
        {
            return DecodeClientBuffer(File.ReadAllBytes(path));
        }

        private static byte[] DecodeClientBuffer(byte[] data)
        {
            if (data.Length <= 5)
                return new byte[0];

            var decoded = new byte[data.Length - 5];
            for (var index = 0; index < decoded.Length; index++)
                decoded[index] = (byte)(data[index + 5] ^ ClientDataKey[index & 255]);
            return decoded;
        }

        private static IEnumerable<string> FindSqlFiles()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var root in GetSearchRoots())
            {
                AddFileIfExists(seen, Path.Combine(root, "Asda2_DB.sql"));
                AddFileIfExists(seen, Path.Combine(root, "asda2_db.sql"));
                AddFileIfExists(seen, Path.Combine(root, "Asda2 - New Source", "Asda2_DB.sql"));
            }
            return seen;
        }

        private static string FindClientRoot()
        {
            foreach (var root in GetSearchRoots())
            {
                var candidate = Path.Combine(root, "Asda2 - Client");
                if (Directory.Exists(Path.Combine(candidate, "data", "NPC")))
                    return candidate;
            }
            return null;
        }

        private static IEnumerable<string> GetSearchRoots()
        {
            var roots = new List<string>();
            AddSearchRoot(roots, Directory.GetCurrentDirectory());
            var directory = AppDomain.CurrentDomain.BaseDirectory;
            for (var depth = 0; depth < 8 && !string.IsNullOrEmpty(directory); depth++)
            {
                AddSearchRoot(roots, directory);
                var name = new DirectoryInfo(directory).Name;
                if (string.Equals(name, "Asda2 - Latest", StringComparison.OrdinalIgnoreCase))
                    break;

                var parent = Directory.GetParent(directory);
                if (parent == null)
                    break;
                directory = parent.FullName;
            }
            return roots;
        }

        private static void AddSearchRoot(List<string> roots, string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            var fullPath = Path.GetFullPath(path);
            if (!roots.Contains(fullPath))
                roots.Add(fullPath);
        }

        private static void AddFileIfExists(HashSet<string> files, string path)
        {
            if (File.Exists(path))
                files.Add(path);
        }

        private static List<string> SplitSqlValues(string line)
        {
            var values = new List<string>();
            var start = line.IndexOf("VALUES", StringComparison.OrdinalIgnoreCase);
            if (start < 0)
                return values;

            start = line.IndexOf('(', start);
            var end = line.LastIndexOf(");", StringComparison.Ordinal);
            if (start < 0 || end <= start)
                return values;

            var text = line.Substring(start + 1, end - start - 1);
            var value = new System.Text.StringBuilder();
            var inQuote = false;
            for (var index = 0; index < text.Length; index++)
            {
                var ch = text[index];
                if (ch == '\'')
                {
                    inQuote = !inQuote;
                    value.Append(ch);
                }
                else if (ch == ',' && !inQuote)
                {
                    values.Add(value.ToString().Trim());
                    value.Length = 0;
                }
                else
                {
                    value.Append(ch);
                }
            }
            values.Add(value.ToString().Trim());
            return values;
        }

        private static int ToInt(string value)
        {
            int result;
            return int.TryParse(Unquote(value), out result) ? result : 0;
        }

        private static string Unquote(string value)
        {
            if (value == null)
                return string.Empty;

            value = value.Trim();
            if (value.Length >= 2 && value[0] == '\'' && value[value.Length - 1] == '\'')
                return value.Substring(1, value.Length - 2).Replace("\\'", "'");
            return value;
        }

        private static string GetNpcQuestKey(int npcId, int questNum)
        {
            return npcId + ":" + questNum;
        }

        private static int GetNpcIdFromKey(string key)
        {
            var separator = key.IndexOf(':');
            if (separator <= 0)
                return 0;

            int npcId;
            return int.TryParse(key.Substring(0, separator), out npcId) ? npcId : 0;
        }

        private static int GetQuestFileId(int questId)
        {
            if (questId <= 0)
                return -1;
            if (questId <= 2411)
                return questId - 2001;
            if (questId <= 2995)
                return questId - 1999;
            return questId;
        }

        private static int ReadInt32(byte[] data, int offset)
        {
            return BitConverter.ToInt32(data, offset);
        }

        private static short ReadInt16(byte[] data, int offset)
        {
            return BitConverter.ToInt16(data, offset);
        }

        private static byte[] GetBytes(byte[] data, int offset, int length)
        {
            var bytes = new byte[length];
            Buffer.BlockCopy(data, offset, bytes, 0, length);
            return bytes;
        }

        private sealed class FallbackQuestRecord
        {
            public readonly int QuestId;
            public string Name;
            public int FileId;
            public int Level;
            public int QuestType;
            public int StartNpcId;
            public int StartQuestNum;
            public int CompleteNpcId;
            public int CompleteQuestNum;
            public int Gold;
            public int Exp;
            public int RepeatCount;
            public bool HasClientReward;
            public int LegacyGold = int.MinValue;
            public int LegacyExp = int.MinValue;
            public readonly int[] MonsterIds = new int[5];
            public readonly int[] ItemIds = new int[5];
            public readonly int[] InitialAmounts = new int[5];
            public readonly int[] RequiredAmounts = new int[5];
            public readonly int[] RewardIds = new int[5];
            public readonly int[] RewardAmounts = new int[5];
            public readonly int[] LegacyRewardIds = new int[5];
            public readonly int[] LegacyRewardAmounts = new int[5];

            public FallbackQuestRecord(int questId)
            {
                QuestId = questId;
                for (var index = 0; index < 5; index++)
                {
                    MonsterIds[index] = -1;
                    ItemIds[index] = -1;
                    RewardIds[index] = -1;
                    LegacyRewardIds[index] = int.MinValue;
                }
            }
        }
    }
}
