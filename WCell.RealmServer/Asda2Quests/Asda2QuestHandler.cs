using System;
using System.IO;
using System.Text;
using System.Linq;
using WCell.Constants;
using WCell.Constants.Items;
using WCell.Core.Network;
using WCell.RealmServer.Asda2_Items;
using WCell.RealmServer.Database;
using WCell.RealmServer.Entities;
using WCell.RealmServer.Handlers;
using WCell.RealmServer.Items;
using WCell.RealmServer.Network;

namespace WCell.RealmServer.Asda2Quests
{
    internal class Asda2QuestHandler
    {
        private const RealmServerOpCode CompleteQuestRequestOpcode = (RealmServerOpCode)5041;
        private const RealmServerOpCode CompleteQuestResponseOpcode = (RealmServerOpCode)5042;
        private const RealmServerOpCode AcceptQuestRequestOpcode = (RealmServerOpCode)5043;
        private const RealmServerOpCode AcceptQuestResponseOpcode = (RealmServerOpCode)5044;
        private const RealmServerOpCode RemoveQuestRequestOpcode = RealmServerOpCode.CMSG_QUESTLOG_REMOVE_QUEST;
        private const byte AcceptQuestSuccessStatus = 0;
        private const byte AcceptQuestFailedStatus = 1;
        private const RealmServerOpCode ClientUiActionRequestOpcode = RealmServerOpCode.ClientUiActionRequest;
        private const RealmServerOpCode QuestObjectiveCounterOpcode = (RealmServerOpCode)5046;
        private const RealmServerOpCode QuestProgressUpdateOpcode = (RealmServerOpCode)5052;
        private const int ClientUiActionTargetIdOffset = 20;
        private const int QuestProgressHeaderLength = 22;
        private const int QuestProgressSlotCount = 11;
        private static readonly object ClientUiTraceLock = new object();

        private sealed class QuestRewardInfo
        {
            public int Gold;
            public int Exp;
            public readonly int[] ItemIds = new int[5];
            public readonly int[] ItemAmounts = new int[5];
        }

        [PacketHandler(CompleteQuestRequestOpcode)]
        public static void CompleteQuestRequest(IRealmClient client, RealmPacketIn packet)
        {
            var npcId = packet.ReadInt16();
            var questNum = packet.ReadByte();
            var chr = client.ActiveCharacter;

            var questId = GetRewardQuestId(npcId, questNum);
            var quest = questId > 0
                ? Asda2QuestProgressRecord.GetActiveQuestRecord(chr.EntityId.Low, questId)
                : GetActiveQuestByCompleter(chr, npcId, questNum);
            if (questId <= 0 && quest != null)
                questId = quest.QuestTemplateId;
            var completed = questId > 0 ? Asda2QuestProgressRecord.GetQuestRecord(chr.EntityId.Low, questId) : null;
            var template = questId > 0 ? Asda2QuestTemplateResolver.Get(questId) : null;
            var reward = GetReward(questId, template);

            if (questId <= 0)
            {
                chr.SendQuestMsg("Quest is not configured yet.");
                return;
            }
            if (reward == null)
            {
                chr.SendQuestMsg("Quest reward is not configured yet.");
                return;
            }
            if (quest == null)
            {
                chr.SendQuestMsg(completed != null && completed.CompleteStatus >= 2
                    ? "You already completed this quest."
                    : "This quest is not in your quest log.");
                if (completed != null && completed.CompleteStatus >= 2)
                    HideCompletedNpcQuestDisplay(client, chr);
                return;
            }
            if (quest.CompleteStatus != 1 && template != null && Asda2QuestMgr.IsQuestReady(quest, template))
            {
                quest.QuestStage = template.AfterStage;
                quest.CompleteStatus = 1;
                quest.Update();
                quest.Save();
            }
            if (quest.CompleteStatus != 1)
            {
                chr.SendQuestMsg("This quest is not complete yet.");
                return;
            }

            if (!TryAddQuestRewards(chr, reward))
            {
                chr.SendQuestMsg("Not enough inventory space or reward data is missing.");
                return;
            }

            chr.Money += (uint)Math.Max(0, reward.Gold);
            if (reward.Exp > 0)
                chr.GainXp(reward.Exp, "Quest", true);

            ApplyClassReward(chr, questId);
            Asda2QuestInventoryHelper.RemoveQuestItems(chr, quest, template);

            var completedSlot = quest.Slot;
            var completedSlots = CompleteQuestRecords(chr, quest, template);

            foreach (var slot in completedSlots)
                ClearQuestSlot(chr, slot);
            SetQuests(chr);
            foreach (var slot in GetQuestProgressUpdateSlots(completedSlot, completedSlots))
                SendQuestProgressStateResponse(client, slot);
            WCell.RealmServer.Asda2Quest.Asda2QuestHandler.SendQuestsListResponse(client);
            chr.SendMoneyUpdate();
            SendCompleteQuestResponse(client, 20);
            CloseNpcQuestConversation(chr);
        }

        [PacketHandler(AcceptQuestRequestOpcode)]
        public static void AcceptQuestRequest(IRealmClient client, RealmPacketIn packet)
        {
            var npcId = packet.ReadInt16();
            var questNum = packet.ReadByte();
            var chr = client.ActiveCharacter;

            var questId = GetAcceptQuestId(npcId, questNum, chr);
            var template = questId > 0 ? Asda2QuestTemplateResolver.GetStandard(questId) : null;
            if (template == null)
            {
                template = Asda2QuestTemplateResolver.GetStandardByStarter(npcId, questNum);
                if (template != null)
                    questId = template.Id;
            }
            var questFileId = GetQuestFileId(questId, template);
            var completedCount = questId > 0 ? Asda2QuestMgr.GetCompletedCount(chr.EntityId.Low, questId) : 0;
            var latestRecord = questId > 0 ? Asda2QuestProgressRecord.GetQuestRecord(chr.EntityId.Low, questId) : null;

            if (questId <= 0 || questFileId < 0 || template == null)
            {
                SendAcceptQuestResponse(client, AcceptQuestFailedStatus, questFileId, questId);
                chr.SendQuestMsg("Quest is not configured yet.");
                return;
            }
            if (!Asda2QuestMgr.IsQuestClientSafe(template, questFileId))
            {
                SendAcceptQuestResponse(client, AcceptQuestFailedStatus, questFileId, questId);
                chr.SendQuestMsg("Quest client data is incomplete.");
                return;
            }
            if (completedCount > 0)
            {
                SendAcceptQuestResponse(client, AcceptQuestFailedStatus, questFileId, questId);
                chr.SendQuestMsg("You already completed this quest.");
                HideCompletedNpcQuestDisplay(client, chr);
                return;
            }
            if (chr.Level < template.Level)
            {
                SendAcceptQuestResponse(client, AcceptQuestFailedStatus, questFileId, questId);
                chr.SendQuestMsg("Your level is too low.");
                return;
            }
            if (latestRecord != null && latestRecord.IsActive)
            {
                SendAcceptQuestResponse(client, AcceptQuestFailedStatus, questFileId, questId);
                chr.SendQuestMsg("You already accepted this quest.");
                return;
            }

            var slot = Asda2QuestMgr.FindFreeSlot(chr);
            if (slot < 1 || slot > 12)
            {
                SendAcceptQuestResponse(client, AcceptQuestFailedStatus, questFileId, questId);
                chr.SendQuestMsg("Your quest log is full.");
                return;
            }

            if (!Asda2QuestInventoryHelper.TryAddStartItems(chr, template.StartItemIds))
            {
                SendAcceptQuestResponse(client, AcceptQuestFailedStatus, questFileId, questId);
                chr.SendQuestMsg("Not enough inventory space or start item data is missing.");
                return;
            }

            var record = new Asda2QuestProgressRecord(questId, questFileId, chr.EntityId.Low, slot,
                template.ItemIds[0], template.ItemIds[1], template.ItemIds[2], template.ItemIds[3], template.ItemIds[4],
                template.InitialAmounts[0], template.InitialAmounts[1], template.InitialAmounts[2],
                template.InitialAmounts[3], template.InitialAmounts[4],
                template.MonsterIds[0], template.MonsterIds[1], template.MonsterIds[2],
                template.MonsterIds[3], template.MonsterIds[4], template.QuestType, template.InitState);

            if (Asda2QuestMgr.IsQuestReady(record, template))
            {
                record.QuestStage = template.AfterStage;
                record.CompleteStatus = 1;
            }

            record.Create();
            SetQuestInSlot(chr, record.Slot, questId);
            SendAcceptQuestResponse(client, AcceptQuestSuccessStatus, questFileId, questId);
            SetQuests(chr);
            SendQuestProgressStateResponse(client, record.Slot);
            WCell.RealmServer.Asda2Quest.Asda2QuestHandler.SendQuestsListResponse(client);
            SendActiveQuestObjectiveCounterResponses(client);
            CloseNpcQuestConversation(chr);
        }

        [PacketHandler(RemoveQuestRequestOpcode)]
        public static void RemoveQuestRequest(IRealmClient client, RealmPacketIn packet)
        {
            var chr = client == null ? null : client.ActiveCharacter;
            if (chr == null)
                return;

            var quest = ResolveQuestToRemove(chr, packet);
            if (quest == null)
            {
                SetQuests(chr);
                WCell.RealmServer.Asda2Quest.Asda2QuestHandler.SendQuestsListResponse(client);
                chr.SendQuestMsg("Quest was not found in your quest log.");
                return;
            }

            RemoveActiveQuest(client, quest);
        }

        public static void RemoveQuestFromInventoryDeleteRequest(IRealmClient client, int slot)
        {
            var chr = client == null ? null : client.ActiveCharacter;
            if (chr == null)
                return;

            var quest = ResolveQuestToRemoveFromInventorySlot(chr, slot);
            if (quest == null)
            {
                SetQuests(chr);
                WCell.RealmServer.Asda2Quest.Asda2QuestHandler.SendQuestsListResponse(client);
                chr.SendQuestMsg("Quest was not found in your quest log.");
                return;
            }

            RemoveActiveQuest(client, quest);
        }

        [PacketHandler(ClientUiActionRequestOpcode)]
        public static void ClientUiActionRequest(IRealmClient client, RealmPacketIn packet)
        {
            var chr = client == null ? null : client.ActiveCharacter;
            if (chr == null || packet == null)
                return;

            var raw = CopyPacketBytes(packet);
            var npcIds = GetNpcQuestIdsFromClientUiAction(client, chr, raw);
            if (npcIds.Length == 0)
                return;

            if (npcIds.Any(npcId => Asda2QuestMgr.GetNpcQuestMark(chr, npcId) != 0))
            {
                SetQuests(chr);
                WCell.RealmServer.Asda2Quest.Asda2QuestHandler.SendQuestsListResponse(client);
                return;
            }

            if (npcIds.Any(npcId => Asda2QuestMgr.HasCompletedNpcQuest(chr, npcId)))
                HideCompletedNpcQuestDisplay(client, chr);
        }

        private static void RemoveActiveQuest(IRealmClient client, Asda2QuestProgressRecord quest)
        {
            var chr = client == null ? null : client.ActiveCharacter;
            if (chr == null || quest == null)
                return;

            var recordsToRemove = GetActiveQuestRecordsToRemove(chr, quest);
            var removedSlots = recordsToRemove
                .Where(record => record != null && record.Slot >= 1 && record.Slot <= 12)
                .Select(record => record.Slot)
                .Distinct()
                .ToArray();

            var template = Asda2QuestTemplateResolver.Get(quest.QuestTemplateId);
            Asda2QuestInventoryHelper.RemoveQuestItems(chr, quest, template);

            foreach (var record in recordsToRemove)
            {
                if (record == null)
                    continue;

                AbandonQuestRecord(record, template);
            }

            foreach (var slot in removedSlots)
                chr.ResetQuest(slot - 1);

            SetQuests(chr);
            SendQuestProgressStateResponse(client, quest);
            WCell.RealmServer.Asda2Quest.Asda2QuestHandler.SendQuestsListResponse(client);
            chr.SendQuestMsg("Quest has been deleted.");
        }

        private static void AbandonQuestRecord(Asda2QuestProgressRecord record, Asda2QuestTemplateInfo template)
        {
            record.Slot = 0;
            record.CompleteStatus = 0;
            record.QuestStage = 2;
            record.Item1Amount = template == null ? 0 : template.InitialAmounts[0];
            record.Item2Amount = template == null ? 0 : template.InitialAmounts[1];
            record.Item3Amount = template == null ? 0 : template.InitialAmounts[2];
            record.Item4Amount = template == null ? 0 : template.InitialAmounts[3];
            record.Item5Amount = template == null ? 0 : template.InitialAmounts[4];
            record.Update();
            record.Save();
        }

        private static Asda2QuestProgressRecord[] GetActiveQuestRecordsToRemove(Character chr,
            Asda2QuestProgressRecord quest)
        {
            if (chr == null || quest == null)
                return new Asda2QuestProgressRecord[0];

            var activeRecords = Asda2QuestProgressRecord.GetActiveQuestRecordsForCharacter(chr.EntityId.Low);
            return activeRecords
                .Where(record => record != null &&
                                 (record.Slot == quest.Slot ||
                                  record.QuestTemplateId == quest.QuestTemplateId))
                .OrderByDescending(record => record.QuestRecordId)
                .ToArray();
        }

        private static Asda2QuestProgressRecord ResolveQuestToRemove(Character chr, RealmPacketIn packet)
        {
            if (chr == null)
                return null;

            var activeRecords = Asda2QuestProgressRecord.GetActiveQuestRecordsForCharacter(chr.EntityId.Low);
            if (activeRecords.Length == 0)
                return null;

            var raw = CopyPacketBytes(packet);
            int slot;
            if (TryReadWrappedRemoveQuestSlot(raw, out slot) || TryReadRemoveQuestSlot(packet, out slot))
            {
                var quest = GetQuestByClientSlot(activeRecords, slot);
                if (quest != null)
                    return quest;
            }

            foreach (var candidate in GetQuestRemoveCandidates(raw))
            {
                var quest = activeRecords
                    .Where(record => record != null &&
                                     (record.QuestTemplateId == candidate ||
                                      (candidate > 100 && record.QuestFileId == candidate)))
                    .OrderByDescending(record => record.QuestRecordId)
                    .FirstOrDefault();
                if (quest != null)
                    return quest;
            }

            return null;
        }

        private static Asda2QuestProgressRecord ResolveQuestToRemoveFromInventorySlot(Character chr, int slot)
        {
            if (chr == null)
                return null;

            var activeRecords = Asda2QuestProgressRecord.GetActiveQuestRecordsForCharacter(chr.EntityId.Low);
            if (activeRecords.Length == 0)
                return null;

            if (slot >= 1 && slot <= 12)
            {
                var quest = GetQuestByInternalSlot(activeRecords, slot);
                if (quest != null)
                    return quest;
            }

            if (slot >= 0 && slot <= 11)
                return GetQuestByInternalSlot(activeRecords, slot + 1);

            return null;
        }

        private static bool TryReadWrappedRemoveQuestSlot(byte[] raw, out int slot)
        {
            slot = -1;
            if (raw == null || raw.Length < 4)
                return false;

            if (raw[0] != 0 || raw[1] != 0x94 || raw[2] != 0x01)
                return false;

            slot = raw[3];
            return true;
        }

        private static bool TryReadRemoveQuestSlot(RealmPacketIn packet, out int slot)
        {
            slot = -1;
            if (packet == null)
                return false;

            var position = packet.Position;
            try
            {
                if (packet.Position >= packet.Length)
                    return false;

                slot = packet.ReadByte();
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                packet.Position = position;
            }
        }

        private static Asda2QuestProgressRecord GetQuestByClientSlot(Asda2QuestProgressRecord[] activeRecords,
            int slot)
        {
            if (activeRecords == null)
                return null;

            if (slot >= 0 && slot <= 11)
            {
                var quest = GetQuestByInternalSlot(activeRecords, slot + 1);
                if (quest != null)
                    return quest;
            }

            return slot >= 1 && slot <= 12 ? GetQuestByInternalSlot(activeRecords, slot) : null;
        }

        private static Asda2QuestProgressRecord GetQuestByInternalSlot(Asda2QuestProgressRecord[] activeRecords,
            int slot)
        {
            return activeRecords
                .Where(record => record != null && record.Slot == slot)
                .OrderByDescending(record => record.QuestRecordId)
                .FirstOrDefault();
        }

        private static int[] GetQuestRemoveCandidates(byte[] raw)
        {
            if (raw == null || raw.Length == 0)
                return new int[0];

            var candidates = new System.Collections.Generic.List<int>();
            for (var index = 0; index < raw.Length; index++)
                candidates.Add(raw[index]);

            for (var index = 0; index <= raw.Length - 2; index++)
                candidates.Add(BitConverter.ToInt16(raw, index));

            for (var index = 0; index <= raw.Length - 4; index++)
                candidates.Add(BitConverter.ToInt32(raw, index));

            return candidates.Where(candidate => candidate >= 0).Distinct().ToArray();
        }

        public static void SendCompleteQuestResponse(IRealmClient client, byte status)
        {
            using (var packet = new RealmPacketOut(CompleteQuestResponseOpcode))
            {
                packet.WriteByte(status);
                packet.WriteInt32(client.ActiveCharacter.AccId);
                packet.WriteInt16(client.ActiveCharacter.SessionId);
                packet.WriteInt32(1);
                packet.WriteInt32(1);
                packet.WriteInt16(1);
                packet.WriteInt32(1);
                packet.WriteInt32(1);
                packet.WriteInt16(1);

                client.Send(packet);
            }
        }

        private static void WriteClientUiActionTrace(IRealmClient client, RealmPacketIn packet, byte[] raw,
            byte[] handlerPayload)
        {
            try
            {
                var traceDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", "PacketTrace");
                Directory.CreateDirectory(traceDir);
                var traceFile = Path.Combine(traceDir,
                    string.Format("packet-trace-5045-payload-{0}.jsonl", DateTime.UtcNow.ToString("yyyyMMdd")));

                var chr = client == null ? null : client.ActiveCharacter;
                var map = chr == null || chr.Map == null ? "" : chr.Map.Id.ToString();
                var json = BuildClientUiActionTraceJson(client, chr == null ? "" : chr.Name, map, packet, raw,
                    handlerPayload);

                lock (ClientUiTraceLock)
                {
                    File.AppendAllText(traceFile, json + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch
            {
            }
        }

        private static string BuildClientUiActionTraceJson(IRealmClient client, string characterName, string map,
            RealmPacketIn packet, byte[] raw, byte[] handlerPayload)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            AppendJsonField(sb, "TimestampUtc", DateTime.UtcNow.ToString("o"));
            AppendJsonField(sb, "Direction", "Inbound");
            AppendJsonField(sb, "PacketName", "ClientUiActionRequest");
            AppendJsonNumber(sb, "Opcode", 5045);
            AppendJsonNumber(sb, "PacketLength", packet == null ? 0 : packet.Length);
            AppendJsonNumber(sb, "ContentLength", packet == null ? 0 : packet.ContentLength);
            AppendJsonNumber(sb, "RawLength", raw == null ? 0 : raw.Length);
            AppendJsonNumber(sb, "HandlerPayloadLength", handlerPayload == null ? 0 : handlerPayload.Length);
            AppendJsonField(sb, "AccountName", client == null ? "" : client.AccountName);
            AppendJsonField(sb, "CharacterName", characterName);
            AppendJsonField(sb, "Map", map);
            AppendJsonField(sb, "RawHex", ToHex(raw));
            AppendJsonField(sb, "HandlerPayloadHex", ToHex(handlerPayload));
            AppendJsonField(sb, "Opaque16Hex", ToHex(raw, 3, Math.Min(raw == null ? 0 : raw.Length - 3, 16)));
            AppendJsonNullableNumber(sb, "RawInt32At19", ReadInt32(raw, 19));
            AppendJsonNullableNumber(sb, "RawInt32At20", ReadInt32(raw, 20));
            AppendJsonNullableNumber(sb, "RawUInt16At20", ReadUInt16(raw, 20));
            AppendJsonNullableNumber(sb, "RawInt32At23", ReadInt32(raw, 23));
            AppendJsonNullableNumber(sb, "RawInt32At27", ReadInt32(raw, 27));
            AppendJsonNullableNumber(sb, "RawInt32At31", ReadInt32(raw, 31));
            AppendJsonNullableNumber(sb, "RawInt32At35", ReadInt32(raw, 35));
            AppendJsonField(sb, "Candidate", "client_ui_action_or_transition_followup");
            AppendJsonField(sb, "Note", "5045 trace; NPC quest selection is expected to continue through 5043");
            sb.Append('}');
            return sb.ToString();
        }

        private static int[] GetNpcQuestIdsFromClientUiAction(IRealmClient client, Character chr, byte[] raw)
        {
            var targetId = GetClientUiActionTargetId(raw);
            if (targetId <= 0)
                return new int[0];

            var npcIds = new System.Collections.Generic.List<int>();
            AddNpcQuestId(npcIds, targetId);

            var npc = ResolveClientUiActionNpc(client, chr, targetId);
            if (npc != null)
            {
                if (npc.Entry != null)
                    AddNpcQuestId(npcIds, (int)npc.Entry.NPCId);
                AddNpcQuestId(npcIds, (int)npc.EntryId);
                if (npc.SpawnEntry != null)
                    AddNpcQuestId(npcIds, (int)npc.SpawnEntry.EntryId);
            }

            return npcIds.Distinct().ToArray();
        }

        private static int GetClientUiActionTargetId(byte[] raw)
        {
            var targetId = ReadInt32(raw, ClientUiActionTargetIdOffset);
            if (targetId.HasValue && targetId.Value > 0 && targetId.Value < 100000)
                return targetId.Value;

            var shortTargetId = ReadUInt16(raw, ClientUiActionTargetIdOffset);
            return shortTargetId.HasValue && shortTargetId.Value > 0 ? shortTargetId.Value : 0;
        }

        private static NPC ResolveClientUiActionNpc(IRealmClient client, Character chr, int targetId)
        {
            if (chr == null || chr.Map == null || targetId <= 0 || targetId > ushort.MaxValue)
                return null;

            var uniqueId = (ushort)targetId;
            var npc = client == null
                ? chr.Map.GetNpcByUniqMapId(uniqueId)
                : chr.Map.GetNpcByUniqMapId(uniqueId, client.Channel);
            if (npc != null)
                return npc;

            var npcs = client == null
                ? chr.Map.GetNpcsById(uniqueId)
                : chr.Map.GetNpcsById(uniqueId, client.Channel);
            return npcs == null ? null : npcs.FirstOrDefault();
        }

        private static void AddNpcQuestId(System.Collections.Generic.List<int> npcIds, int npcId)
        {
            if (npcIds != null && npcId > 0 && npcId < 100000 && !npcIds.Contains(npcId))
                npcIds.Add(npcId);
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

        private static int? ReadInt32(byte[] payload, int offset)
        {
            if (payload == null || payload.Length < offset + 4)
                return null;
            return BitConverter.ToInt32(payload, offset);
        }

        private static int? ReadUInt16(byte[] payload, int offset)
        {
            if (payload == null || payload.Length < offset + 2)
                return null;
            return BitConverter.ToUInt16(payload, offset);
        }

        private static string ToHex(byte[] payload)
        {
            return ToHex(payload, 0, payload == null ? 0 : payload.Length);
        }

        private static string ToHex(byte[] payload, int offset, int count)
        {
            if (payload == null || count <= 0 || offset < 0 || offset >= payload.Length)
                return "";
            count = Math.Min(count, payload.Length - offset);
            var slice = new byte[count];
            Buffer.BlockCopy(payload, offset, slice, 0, count);
            return BitConverter.ToString(slice).Replace("-", " ");
        }

        private static void AppendJsonField(StringBuilder sb, string name, string value)
        {
            AppendJsonComma(sb);
            sb.Append('"').Append(name).Append("\":\"").Append(EscapeJson(value)).Append('"');
        }

        private static void AppendJsonNumber(StringBuilder sb, string name, int value)
        {
            AppendJsonComma(sb);
            sb.Append('"').Append(name).Append("\":").Append(value);
        }

        private static void AppendJsonNullableNumber(StringBuilder sb, string name, int? value)
        {
            AppendJsonComma(sb);
            sb.Append('"').Append(name).Append("\":");
            if (value.HasValue)
                sb.Append(value.Value);
            else
                sb.Append("null");
        }

        private static void AppendJsonComma(StringBuilder sb)
        {
            if (sb.Length > 1)
                sb.Append(',');
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        public static void SendAcceptQuestResponse(IRealmClient client, byte status, int questIdByFileName, int questId)
        {
            using (var packet = new RealmPacketOut(AcceptQuestResponseOpcode))
            {
                packet.WriteByte(status);
                packet.WriteInt16(questIdByFileName);
                packet.WriteInt32(questId);
                packet.WriteInt32(questIdByFileName);
                packet.WriteInt32(questIdByFileName);
                client.Send(packet);
            }
        }

        public static void SendUpdateQuestResponse(IRealmClient client, Asda2QuestProgressRecord quest, int itemId, int amount)
        {
            var objectiveIndex = GetQuestObjectiveIndex(quest, itemId);
            var requiredAmount = GetQuestObjectiveRequiredAmount(quest == null ? -1 : quest.QuestTemplateId, objectiveIndex);
            SendUpdateQuestResponse(client, quest, objectiveIndex, itemId, amount, requiredAmount);
        }

        public static void SendUpdateQuestResponse(IRealmClient client, Asda2QuestProgressRecord quest,
            int objectiveIndex, int targetId, int amount, int requiredAmount)
        {
            SendUpdateQuestResponse(client, quest, objectiveIndex, targetId, -1, amount, requiredAmount);
        }

        public static void SendUpdateQuestResponse(IRealmClient client, Asda2QuestProgressRecord quest,
            int objectiveIndex, int targetId, int alternateTargetId, int amount, int requiredAmount)
        {
            var primaryTargetId = alternateTargetId > 0 ? alternateTargetId : targetId;
            if (primaryTargetId > 0)
                SendQuestObjectiveCounterResponse(client, quest, objectiveIndex, primaryTargetId, amount, requiredAmount);

            SendQuestProgressStateResponse(client, quest);
            WCell.RealmServer.Asda2Quest.Asda2QuestHandler.SendQuestsListResponse(client);
        }

        public static void SendBulletinUpdateQuestResponse(IRealmClient client, Asda2QuestProgressRecord quest,
            int objectiveIndex, int targetId, int alternateTargetId, int amount, int requiredAmount)
        {
            var primaryTargetId = alternateTargetId > 0 ? alternateTargetId : targetId;

            SendQuestProgressStateResponse(client, quest);
            if (primaryTargetId > 0)
                SendQuestObjectiveCounterResponse(client, quest, objectiveIndex, primaryTargetId, amount, requiredAmount);

            WCell.RealmServer.Asda2Quest.Asda2QuestHandler.SendQuestsListResponse(client);
        }

        public static void SendActiveQuestObjectiveCounterResponses(IRealmClient client)
        {
            if (client == null || client.ActiveCharacter == null)
                return;

            var activeRecords = Asda2QuestProgressRecord.GetActiveQuestRecordsForCharacter(
                    client.ActiveCharacter.EntityId.Low)
                .Where(record => record != null && record.Slot >= 1 && record.Slot <= 12 &&
                                 Asda2QuestMgr.IsQuestClientSafe(record))
                .GroupBy(record => record.Slot)
                .Select(group => group.OrderByDescending(record => record.QuestRecordId).First())
                .OrderBy(record => record.Slot)
                .ToArray();

            foreach (var quest in activeRecords)
            {
                for (var objectiveIndex = 1; objectiveIndex <= 5; objectiveIndex++)
                {
                    var requiredAmount = GetQuestObjectiveRequiredAmount(quest.QuestTemplateId, objectiveIndex);
                    if (requiredAmount <= 0)
                        continue;

                    var targetId = GetQuestObjectiveCounterTargetId(quest, objectiveIndex);
                    if (targetId <= 0)
                        continue;

                    SendQuestObjectiveCounterResponse(client, quest, objectiveIndex, targetId,
                        GetQuestObjectiveAmount(quest, objectiveIndex), requiredAmount);
                }
            }
        }

        private static void SendQuestProgressStateResponse(IRealmClient client, Asda2QuestProgressRecord updatedQuest)
        {
            SendQuestProgressStateResponse(client, updatedQuest == null ? -1 : updatedQuest.Slot);
        }

        public static void SendQuestProgressStateResponse(IRealmClient client, int updatedSlot)
        {
            if (client == null || client.ActiveCharacter == null)
                return;

            var activeRecords = Asda2QuestProgressRecord.GetActiveQuestRecordsForCharacter(
                client.ActiveCharacter.EntityId.Low);
            var recordsBySlot = activeRecords
                .Where(record => record != null && record.Slot >= 1 && record.Slot <= 12 &&
                                 Asda2QuestMgr.IsQuestClientSafe(record))
                .GroupBy(record => record.Slot)
                .ToDictionary(group => group.Key, group => group.OrderByDescending(record => record.QuestRecordId).First());

            using (var packet = new RealmPacketOut(QuestProgressUpdateOpcode))
            {
                for (var index = 0; index < QuestProgressHeaderLength; index++)
                    packet.WriteByte(0);

                foreach (var slot in GetQuestProgressSlotOrder(updatedSlot))
                {
                    Asda2QuestProgressRecord quest;
                    recordsBySlot.TryGetValue(slot, out quest);
                    WriteQuestProgressSlot(packet, quest, slot);
                }

                packet.WriteInt16(0);
                client.Send(packet);
            }
        }

        private static int[] GetQuestProgressSlotOrder(Asda2QuestProgressRecord updatedQuest)
        {
            return GetQuestProgressSlotOrder(updatedQuest == null ? -1 : updatedQuest.Slot);
        }

        private static int[] GetQuestProgressSlotOrder(int updatedSlot)
        {
            var slots = new System.Collections.Generic.List<int>();
            if (updatedSlot >= 1 && updatedSlot <= 12)
                slots.Add(updatedSlot);

            for (var slot = 1; slot <= 12 && slots.Count < QuestProgressSlotCount; slot++)
            {
                if (!slots.Contains(slot))
                    slots.Add(slot);
            }

            return slots.ToArray();
        }

        private static void WriteQuestProgressSlot(RealmPacketOut packet, Asda2QuestProgressRecord quest, int slot)
        {
            packet.WriteInt32(quest == null ? -1 : quest.QuestTemplateId);
            packet.WriteByte(0);
            packet.WriteInt16(slot - 1);
            packet.WriteByte((byte)(quest == null ? 0 : quest.QuestStage));
            packet.WriteInt16(quest == null ? -1 : quest.QuestFileId);
            packet.WriteInt16(quest == null ? 2 : quest.CompleteStatus);
            packet.WriteInt16(-1);
            WriteQuestProgressObjective(packet, quest == null ? -1 : quest.Item1Id, quest == null ? 0 : quest.Item1Amount);
            WriteQuestProgressObjective(packet, quest == null ? -1 : quest.Item2Id, quest == null ? 0 : quest.Item2Amount);
            WriteQuestProgressObjective(packet, quest == null ? -1 : quest.Item3Id, quest == null ? 0 : quest.Item3Amount);
            WriteQuestProgressObjective(packet, quest == null ? -1 : quest.Item4Id, quest == null ? 0 : quest.Item4Amount);
            WriteQuestProgressObjective(packet, quest == null ? -1 : quest.Item5Id, quest == null ? 0 : quest.Item5Amount);
        }

        private static void WriteQuestProgressObjective(RealmPacketOut packet, int targetId, int amount)
        {
            packet.WriteInt32(targetId <= 0 ? -1 : targetId);
            packet.WriteInt16(amount < 0 ? 0 : amount);
        }

        private static void SendQuestObjectiveCounterResponse(IRealmClient client, Asda2QuestProgressRecord quest,
            int objectiveIndex, int targetId, int amount, int requiredAmount)
        {
            if (quest == null)
                return;

            var normalizedRequiredAmount = Math.Max(0, requiredAmount);
            var normalizedAmount = Math.Max(0, amount);
            if (normalizedRequiredAmount > 0 && normalizedAmount > normalizedRequiredAmount)
                normalizedAmount = normalizedRequiredAmount;

            using (var packet = new RealmPacketOut(QuestObjectiveCounterOpcode))
            {
                packet.WriteInt32(quest.QuestTemplateId);
                packet.WriteInt32(targetId);
                packet.WriteInt32(normalizedAmount);
                packet.WriteInt32(normalizedRequiredAmount);
                packet.WriteInt16((short)(quest.Slot <= 0 ? -1 : quest.Slot - 1));
                packet.WriteByte((byte)(objectiveIndex <= 0 ? 0 : objectiveIndex - 1));
                packet.WriteByte((byte)(quest.CompleteStatus == 1 ? 1 : 0));
                client.Send(packet);
            }
        }

        private static int GetQuestObjectiveIndex(Asda2QuestProgressRecord quest, int itemId)
        {
            if (quest == null || itemId <= 0)
                return 1;
            if (quest.Item1Id == itemId)
                return 1;
            if (quest.Item2Id == itemId)
                return 2;
            if (quest.Item3Id == itemId)
                return 3;
            if (quest.Item4Id == itemId)
                return 4;
            if (quest.Item5Id == itemId)
                return 5;
            for (var index = 1; index <= 5; index++)
            {
                if (Asda2QuestMgr.GetQuestObjectiveDisplayId(quest, index) == itemId)
                    return index;
            }
            return 1;
        }

        private static int GetQuestObjectiveRequiredAmount(int questId, int objectiveIndex)
        {
            var template = Asda2QuestTemplateResolver.Get(questId);
            return template == null || objectiveIndex < 1 || objectiveIndex > 5
                ? 0
                : template.RequiredAmounts[objectiveIndex - 1];
        }

        private static int GetQuestObjectiveCounterTargetId(Asda2QuestProgressRecord quest, int objectiveIndex)
        {
            if (quest == null)
                return -1;

            switch (objectiveIndex)
            {
                case 1:
                    return GetQuestObjectiveCounterTargetId(quest, quest.Item1Id, objectiveIndex);
                case 2:
                    return GetQuestObjectiveCounterTargetId(quest, quest.Item2Id, objectiveIndex);
                case 3:
                    return GetQuestObjectiveCounterTargetId(quest, quest.Item3Id, objectiveIndex);
                case 4:
                    return GetQuestObjectiveCounterTargetId(quest, quest.Item4Id, objectiveIndex);
                case 5:
                    return GetQuestObjectiveCounterTargetId(quest, quest.Item5Id, objectiveIndex);
                default:
                    return -1;
            }
        }

        private static int GetQuestObjectiveCounterTargetId(Asda2QuestProgressRecord quest, int itemId, int objectiveIndex)
        {
            return itemId > 0 ? itemId : Asda2QuestMgr.GetQuestObjectiveDisplayId(quest, objectiveIndex);
        }

        private static int GetQuestObjectiveAmount(Asda2QuestProgressRecord quest, int objectiveIndex)
        {
            if (quest == null)
                return 0;

            switch (objectiveIndex)
            {
                case 1:
                    return Math.Max(0, quest.Item1Amount);
                case 2:
                    return Math.Max(0, quest.Item2Amount);
                case 3:
                    return Math.Max(0, quest.Item3Amount);
                case 4:
                    return Math.Max(0, quest.Item4Amount);
                case 5:
                    return Math.Max(0, quest.Item5Amount);
                default:
                    return 0;
            }
        }

        private static int GetRewardQuestId(short npcId, byte questNum)
        {
            try
            {
                var questId = Asda2QuestRewardNpc.GetQuest(npcId, questNum);
                if (questId > 0)
                    return questId;
            }
            catch
            {
            }

            try
            {
                var questId = Asda2QuestNpc.GetQuest(npcId, questNum);
                if (questId > 0)
                    return questId;
            }
            catch
            {
            }

            var template = Asda2QuestTemplateResolver.GetStandardByCompleter(npcId, questNum);
            return template == null ? 0 : template.Id;
        }

        private static Asda2QuestProgressRecord GetActiveQuestByCompleter(Character chr, short npcId, byte questNum)
        {
            if (chr == null)
                return null;

            var activeRecords = Asda2QuestProgressRecord.GetActiveQuestRecordsForCharacter(chr.EntityId.Low);
            foreach (var activeRecord in activeRecords)
            {
                if (activeRecord == null)
                    continue;

                var template = Asda2QuestTemplateResolver.Get(activeRecord.QuestTemplateId);
                if (template != null && template.CompleteNpcId == npcId && template.CompleteQuestNum == questNum)
                    return activeRecord;
            }

            return null;
        }

        private static int GetAcceptQuestId(short npcId, byte questNum, Character chr)
        {
            var configuredQuestId = 0;
            try
            {
                configuredQuestId = Asda2QuestNpc.GetQuest(npcId, questNum);
                if (IsAcceptQuestCandidate(chr, configuredQuestId))
                    return configuredQuestId;
            }
            catch
            {
            }

            var template = Asda2QuestTemplateResolver.GetStandardByStarter(npcId, questNum);
            if (template != null && IsAcceptQuestCandidate(chr, template.Id, template))
                return template.Id;

            var availableTemplateQuestId = GetAvailableTemplateStarterQuestId(chr, npcId, questNum);
            if (availableTemplateQuestId > 0)
                return availableTemplateQuestId;

            var availableQuestId = chr == null
                ? 0
                : Asda2QuestFallbackData.GetStarterQuestIdForCharacter(npcId, questNum, chr.EntityId.Low, chr.Level);
            return availableQuestId > 0 ? availableQuestId : configuredQuestId;
        }

        private static int GetAvailableTemplateStarterQuestId(Character chr, int npcId, int questNum)
        {
            if (chr == null || npcId <= 0)
                return 0;

            try
            {
                var records = Asda2QuestTemplateRecord.GetAllByStarter(npcId)
                    .Where(record => record != null)
                    .OrderBy(record => record.QuestNum)
                    .ThenBy(record => record.QuestId)
                    .Select(record => record.QuestId)
                    .Distinct()
                    .ToArray();
                if (records.Length == 0)
                    return 0;

                var availableQuestIds = records
                    .Where(questId => IsAcceptQuestCandidate(chr, questId))
                    .ToArray();
                if (questNum >= 0 && questNum < availableQuestIds.Length)
                    return availableQuestIds[questNum];
                if (questNum > 0 && questNum - 1 < availableQuestIds.Length)
                    return availableQuestIds[questNum - 1];
            }
            catch
            {
            }

            return 0;
        }

        private static bool IsAcceptQuestCandidate(Character chr, int questId)
        {
            return IsAcceptQuestCandidate(chr, questId, questId > 0
                ? Asda2QuestTemplateResolver.GetStandard(questId)
                : null);
        }

        private static bool IsAcceptQuestCandidate(Character chr, int questId, Asda2QuestTemplateInfo template)
        {
            if (chr == null || questId <= 0 || template == null)
                return false;

            if (!Asda2QuestMgr.IsQuestClientSafe(template, template.FileId))
                return false;

            if (chr.Level < template.Level)
                return false;

            var completedCount = Asda2QuestMgr.GetCompletedCount(chr.EntityId.Low, questId);
            if (completedCount > 0)
                return false;

            var latestRecord = Asda2QuestProgressRecord.GetQuestRecord(chr.EntityId.Low, questId);
            return latestRecord == null || !latestRecord.IsActive;
        }

        private static QuestRewardInfo GetReward(int questId, Asda2QuestTemplateInfo template)
        {
            try
            {
                var record = Asda2QuestRewardTable.GetRecordByID(questId);
                if (record != null)
                    return CreateReward(record.Gold, record.Exp,
                        record.Item1Id, record.Item1Amount, record.Item2Id, record.Item2Amount,
                        record.Item3Id, record.Item3Amount, record.Item4Id, record.Item4Amount,
                        -1, 0);
            }
            catch
            {
            }

            return template == null ? null : CreateReward(template);
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

        private static int GetQuestFileId(int questId, Asda2QuestTemplateInfo template)
        {
            if (template != null && template.FileId >= 0)
                return template.FileId;
            return GetQuestFileId(questId);
        }

        private static QuestRewardInfo CreateReward(Asda2QuestTemplateInfo template)
        {
            return CreateReward(template.Gold, template.Exp,
                template.RewardIds[0], template.RewardAmounts[0],
                template.RewardIds[1], template.RewardAmounts[1],
                template.RewardIds[2], template.RewardAmounts[2],
                template.RewardIds[3], template.RewardAmounts[3],
                template.RewardIds[4], template.RewardAmounts[4]);
        }

        private static QuestRewardInfo CreateReward(int gold, int exp,
            int item1, int item1Amount, int item2, int item2Amount, int item3, int item3Amount,
            int item4, int item4Amount, int item5, int item5Amount)
        {
            var reward = new QuestRewardInfo
            {
                Gold = gold,
                Exp = exp
            };
            reward.ItemIds[0] = item1;
            reward.ItemIds[1] = item2;
            reward.ItemIds[2] = item3;
            reward.ItemIds[3] = item4;
            reward.ItemIds[4] = item5;
            reward.ItemAmounts[0] = item1Amount;
            reward.ItemAmounts[1] = item2Amount;
            reward.ItemAmounts[2] = item3Amount;
            reward.ItemAmounts[3] = item4Amount;
            reward.ItemAmounts[4] = item5Amount;
            return reward;
        }

        private static bool TryAddQuestRewards(Character chr, QuestRewardInfo reward)
        {
            if (reward == null)
                return false;

            return Asda2QuestInventoryHelper.TryAddItems(chr, reward.ItemIds, reward.ItemAmounts);
        }

        private static void ApplyClassReward(Character chr, int questId)
        {
            switch (questId)
            {
                case 2022:
                    chr.SetClass(1, 1);
                    break;
                case 2473:
                    chr.SetClass(1, 2);
                    break;
                case 2472:
                    chr.SetClass(1, 3);
                    break;
                case 2023:
                    chr.SetClass(1, 4);
                    break;
                case 2474:
                    chr.SetClass(1, 5);
                    break;
                case 2024:
                    chr.SetClass(1, 7);
                    break;
                case 2475:
                    chr.SetClass(1, 8);
                    break;
                case 2476:
                    chr.SetClass(1, 9);
                    break;
                case 2055:
                    switch (chr.Class)
                    {
                        case ClassId.OHS:
                            chr.SetClass(2, 1);
                            break;
                        case ClassId.THS:
                            chr.SetClass(2, 3);
                            break;
                        case ClassId.Spear:
                            chr.SetClass(2, 2);
                            break;
                    }
                    break;
                case 2057:
                    switch (chr.Class)
                    {
                        case ClassId.Bow:
                            chr.SetClass(2, 5);
                            break;
                        case ClassId.Crossbow:
                            chr.SetClass(2, 4);
                            break;
                    }
                    break;
                case 2058:
                    switch (chr.Class)
                    {
                        case ClassId.AtackMage:
                            chr.SetClass(2, 7);
                            break;
                        case ClassId.SupportMage:
                            chr.SetClass(2, 8);
                            break;
                        case ClassId.HealMage:
                            chr.SetClass(2, 9);
                            break;
                    }
                    break;
            }
        }

        private static int[] CompleteQuestRecords(Character chr, Asda2QuestProgressRecord quest,
            Asda2QuestTemplateInfo template)
        {
            if (chr == null || quest == null)
                return new int[0];

            var records = Asda2QuestProgressRecord.GetActiveQuestRecordsForCharacter(chr.EntityId.Low)
                .Where(record => record != null && record.QuestTemplateId == quest.QuestTemplateId)
                .ToList();

            if (!records.Any(record => record.QuestRecordId == quest.QuestRecordId))
                records.Add(quest);

            var slots = records
                .Where(record => record.Slot >= 1 && record.Slot <= 12)
                .Select(record => record.Slot)
                .Distinct()
                .ToArray();

            foreach (var record in records)
                MarkQuestCompleted(record, template);

            return slots;
        }

        private static void MarkQuestCompleted(Asda2QuestProgressRecord record, Asda2QuestTemplateInfo template)
        {
            if (record == null)
                return;

            record.Slot = 0;
            record.CompleteStatus = 2;
            record.QuestStage = template == null ? 1 : template.AfterStage;
            record.Update();
            record.Save();
        }

        private static void ClearQuestSlot(Character chr, int slot)
        {
            if (chr != null && slot >= 1 && slot <= 12)
                chr.ResetQuest(slot - 1);
        }

        private static int[] GetQuestProgressUpdateSlots(int primarySlot, int[] clearedSlots)
        {
            var slots = clearedSlots == null
                ? new int[0]
                : clearedSlots.Where(slot => slot >= 1 && slot <= 12).ToArray();

            if (primarySlot >= 1 && primarySlot <= 12 && !slots.Contains(primarySlot))
                slots = new[] {primarySlot}.Concat(slots).ToArray();

            return slots.Length == 0 ? new[] {primarySlot} : slots.Distinct().ToArray();
        }

        private static void SetQuestInSlot(Character chr, int slot, int questId)
        {
            if (slot >= 1 && slot <= 12)
                chr.SetQuestId(slot - 1, questId <= 0 ? 0U : (uint)questId);
        }

        private static void HideCompletedNpcQuestDisplay(IRealmClient client, Character chr)
        {
            if (client == null || chr == null)
                return;

            SetQuests(chr);
            SendQuestProgressStateResponse(client, -1);
            WCell.RealmServer.Asda2Quest.Asda2QuestHandler.SendQuestsListResponse(client);
            CloseNpcQuestConversation(chr);
        }

        private static void CloseNpcQuestConversation(Character chr)
        {
            if (chr == null)
                return;

            var conversation = chr.GossipConversation;
            if (conversation != null)
            {
                conversation.Dispose();
                return;
            }

            GossipHandler.SendConversationComplete(chr);
        }

        public static void SetQuests(Character chr)
        {
            if (chr == null)
                return;

            var questRecords = Asda2QuestProgressRecord.GetActiveQuestRecordsForCharacter(chr.EntityId.Low);
            for (var slot = 1; slot <= 12; slot++)
            {
                var record = questRecords
                    .Where(qr => qr != null && qr.Slot == slot && Asda2QuestMgr.IsQuestClientSafe(qr))
                    .OrderByDescending(qr => qr.QuestRecordId)
                    .FirstOrDefault();
                chr.SetQuestId(slot - 1, record == null ? 0U : (uint)record.QuestTemplateId);
            }
        }
    }
}
