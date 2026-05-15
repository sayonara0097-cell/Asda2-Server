const fs = require("fs");
const path = require("path");
const zlib = require("zlib");

const root = path.resolve(__dirname, "..", "..");
const sourceRoot = path.resolve(__dirname, "..");
const clientRoot = path.join(root, "Asda2 - Client");
const oldSqlPath = path.join(root, "asda2_db.sql");
const sourceSqlPath = path.join(sourceRoot, "Asda2_DB.sql");
const outputPath = path.join(sourceRoot, "Asda2_AllQuests_Import.sql");

const templateColumns = [
  "QuestId", "FileId", "Name", "NpcId", "QuestNum", "CompleteNpcId", "CompleteQuestNum",
  "Level", "Gold", "Exp",
  "Monster1", "Monster2", "Monster3", "Monster4", "Monster5",
  "Item1", "Item1Amount", "Item1Chance",
  "Item2", "Item2Amount", "Item2Chance",
  "Item3", "Item3Amount", "Item3Chance",
  "Item4", "Item4Amount", "Item4Chance",
  "Item5", "Item5Amount", "Item5Chance",
  "Reward1", "Reward1Amount", "Reward1OP",
  "Reward2", "Reward2Amount", "Reward2OP",
  "Reward3", "Reward3Amount", "Reward3OP",
  "Reward4", "Reward4Amount", "Reward4OP",
  "Reward5", "Reward5Amount", "Reward5OP",
  "RepeatCount",
  "HiddenItem1", "HiddenItem1Amount", "HiddenItem1Giver",
  "HiddenItem2", "HiddenItem2Amount", "HiddenItem2Giver",
  "HiddenItem3", "HiddenItem3Amount", "HiddenItem3Giver",
  "StartItem1", "StartItem2", "StartItem3",
  "XpPerItem", "AfterStage", "AfterComplete", "DoSort", "SeqId", "InitState"
];

const records = new Map();
const starterLinks = new Map();
const completerLinks = new Map();
const legacyRewards = new Map();
const decoder1256 = new TextDecoder("windows-1256");
const clientQuestBookFileIds = new Set();

function getOrCreate(questId) {
  if (!records.has(questId)) {
    records.set(questId, {
      questId,
      fileId: getQuestFileId(questId),
      name: `Quest ${questId}`,
      npcId: -1,
      questNum: -1,
      completeNpcId: -1,
      completeQuestNum: -1,
      level: 0,
      gold: 0,
      exp: 0,
      monsters: [-1, -1, -1, -1, -1],
      items: [-1, -1, -1, -1, -1],
      itemAmounts: [0, 0, 0, 0, 0],
      itemChances: [100, 100, 100, 100, 100],
      required: [0, 0, 0, 0, 0],
      rewards: [-1, -1, -1, -1, -1],
      rewardAmounts: [0, 0, 0, 0, 0],
      rewardOptional: [0, 0, 0, 0, 0],
      repeatCount: 0,
      hiddenItems: [-1, -1, -1],
      hiddenAmounts: [0, 0, 0],
      hiddenGivers: [-1, -1, -1],
      startItems: [-1, -1, -1],
      xpPerItem: 0,
      afterStage: 1,
      afterComplete: 1,
      doSort: 1,
      seqId: -1,
      initState: 0,
      hasClientReward: false
    });
  }
  return records.get(questId);
}

function getQuestFileId(questId) {
  if (questId <= 0) return -1;
  if (questId <= 2411) return questId - 2001;
  if (questId <= 2995) return questId - 1999;
  return questId;
}

function readClientKey() {
  const source = fs.readFileSync(path.join(sourceRoot, "WCell.RealmServer", "Asda2Quests", "Asda2QuestFallbackData.cs"), "utf8");
  return [...source.matchAll(/0x[0-9A-Fa-f]{2}/g)].map(match => parseInt(match[0], 16));
}

const clientKey = readClientKey();

function decodeClientBuffer(buffer) {
  if (buffer.length <= 5) return Buffer.alloc(0);
  const decoded = Buffer.alloc(buffer.length - 5);
  for (let index = 0; index < decoded.length; index++)
    decoded[index] = buffer[index + 5] ^ clientKey[index & 255];
  return decoded;
}

function decodeClientFile(filePath) {
  return decodeClientBuffer(fs.readFileSync(filePath));
}

function readZipEntries(filePath) {
  const buffer = fs.readFileSync(filePath);
  const entries = [];
  let offset = 0;
  while (offset + 30 <= buffer.length && buffer.readUInt32LE(offset) === 0x04034b50) {
    const method = buffer.readUInt16LE(offset + 8);
    const compressedSize = buffer.readUInt32LE(offset + 18);
    const nameLength = buffer.readUInt16LE(offset + 26);
    const extraLength = buffer.readUInt16LE(offset + 28);
    const name = buffer.slice(offset + 30, offset + 30 + nameLength).toString("utf8");
    const dataOffset = offset + 30 + nameLength + extraLength;
    let data = buffer.slice(dataOffset, dataOffset + compressedSize);
    if (method === 8) data = zlib.inflateRawSync(data);
    entries.push({ name, data });
    offset = dataOffset + compressedSize;
  }
  return entries;
}

function splitSqlValues(line) {
  const valuesIndex = line.toUpperCase().indexOf("VALUES");
  if (valuesIndex < 0) return [];
  const start = line.indexOf("(", valuesIndex);
  const end = line.lastIndexOf(")");
  if (start < 0 || end <= start) return [];

  const text = line.slice(start + 1, end);
  const values = [];
  let current = "";
  let inQuote = false;
  for (let index = 0; index < text.length; index++) {
    const ch = text[index];
    if (ch === "\\" && inQuote && index + 1 < text.length) {
      current += ch + text[++index];
      continue;
    }
    if (ch === "'") {
      inQuote = !inQuote;
      continue;
    }
    if (ch === "," && !inQuote) {
      values.push(unescapeSql(current.trim()));
      current = "";
      continue;
    }
    current += ch;
  }
  values.push(unescapeSql(current.trim()));
  return values;
}

function unescapeSql(value) {
  if (value === "NULL") return "";
  return value.replace(/\\'/g, "'").replace(/\\\\/g, "\\");
}

function toInt(value, fallback = 0) {
  const result = Number.parseInt(value, 10);
  return Number.isFinite(result) ? result : fallback;
}

function addStarterLink(npcId, questNum, questId, name, overwriteRecordLink) {
  if (npcId <= 0 || questId <= 0 || questNum < 0) return;
  const key = `${npcId}:${questNum}`;
  const existing = starterLinks.get(key);
  const hasConflict = existing && existing.questId !== questId && !overwriteRecordLink;
  if (!hasConflict && (!existing || overwriteRecordLink)) starterLinks.set(key, { npcId, questNum, questId, name });
  const record = getOrCreate(questId);
  if (overwriteRecordLink || record.npcId <= 0) {
    record.npcId = npcId;
    record.questNum = questNum;
  }
}

function addCompleterLink(npcId, questNum, questId, name, overwriteRecordLink) {
  if (npcId <= 0 || questId <= 0 || questNum < 0) return;
  const key = `${npcId}:${questNum}`;
  const existing = completerLinks.get(key);
  if (existing && existing.questId !== questId && !overwriteRecordLink) return;
  if (!existing || overwriteRecordLink) completerLinks.set(key, { npcId, questNum, questId, name });
  const record = getOrCreate(questId);
  if (overwriteRecordLink || record.completeNpcId <= 0) {
    record.completeNpcId = npcId;
    record.completeQuestNum = questNum;
  }
}

function loadOldSql(filePath) {
  if (!fs.existsSync(filePath)) return;
  const lines = fs.readFileSync(filePath, "utf8").split(/\r?\n/);
  for (const line of lines) {
    if (line.includes("INSERT INTO `asda2questrecord` VALUES")) {
      const v = splitSqlValues(line);
      if (v.length < 26) continue;
      const questId = toInt(v[1]);
      const record = getOrCreate(questId);
      record.name = v[0] || record.name;
      record.level = toInt(v[2], record.level);
      record.gold = toInt(v[3], record.gold);
      record.exp = toInt(v[4], record.exp);
      for (let i = 0; i < 5; i++) record.monsters[i] = toInt(v[5 + i], -1);
      for (let i = 0; i < 5; i++) {
        record.items[i] = normalizeItem(toInt(v[10 + i * 2], -1));
        record.itemAmounts[i] = Math.max(0, toInt(v[11 + i * 2], 0));
      }
      for (let i = 0; i < 5; i++) record.required[i] = Math.max(0, toInt(v[20 + i], 0));
      record.questType = toInt(v[25], 1);
    } else if (line.includes("INSERT INTO `asda2questnpc` VALUES")) {
      const v = splitSqlValues(line);
      if (v.length >= 5) addStarterLink(toInt(v[1]), toInt(v[2]), toInt(v[3]), v[4], true);
    } else if (line.includes("INSERT INTO `asda2questrewardnpc` VALUES")) {
      const v = splitSqlValues(line);
      if (v.length >= 5) addCompleterLink(toInt(v[1]), toInt(v[2]), toInt(v[3]), v[4], true);
    } else if (line.includes("INSERT INTO `asda2questrewardtable` VALUES")) {
      const v = splitSqlValues(line);
      if (v.length >= 11) {
        legacyRewards.set(toInt(v[0]), {
          gold: toInt(v[1]),
          exp: toInt(v[2]),
          rewards: [toInt(v[3], -1), toInt(v[5], -1), toInt(v[7], -1), toInt(v[9], -1), -1],
          rewardAmounts: [toInt(v[4]), toInt(v[6]), toInt(v[8]), toInt(v[10]), 0]
        });
      }
    }
  }
}

function normalizeItem(itemId) {
  return itemId > 0 ? itemId : -1;
}

function loadEpisodeTable() {
  const data = decodeClientFile(path.join(clientRoot, "data", "NPC", "Episode", "Episode_Table.BIN"));
  const count = data.readInt32LE(4);
  const size = Math.floor((data.length - 20) / count);
  for (let index = 0; index < count; index++) {
    const offset = 20 + index * size;
    const questId = data.readInt32LE(offset + 200);
    if (questId <= 0) continue;
    const record = getOrCreate(questId);
    record.fileId = data.readInt32LE(offset + 236);
    record.level = data.readInt32LE(offset + 240);
    const rawName = data.slice(offset, offset + 200);
    const name = decoder1256.decode(rawName).replace(/\0/g, "").trim();
    if (name) record.name = name;
  }
}

function buildFileIdIndex() {
  const map = new Map();
  for (const record of records.values()) {
    if (record.fileId > 0 && !map.has(record.fileId)) map.set(record.fileId, record);
  }
  return map;
}

function loadRewardTable() {
  const data = decodeClientFile(path.join(clientRoot, "data", "NPC", "RewardTable.BIN"));
  const count = data.readInt32LE(4);
  const size = Math.floor((data.length - 20) / count);
  const byFileId = buildFileIdIndex();
  for (let index = 0; index < count; index++) {
    const offset = 20 + index * size;
    const fileId = data.readInt32LE(offset + 4);
    const record = byFileId.get(fileId);
    if (!record) continue;
    record.gold = data.readInt32LE(offset + 16);
    record.exp = data.readInt32LE(offset + 20);
    record.hasClientReward = true;
    setReward(record, 0, data.readInt32LE(offset + 52), data.readInt32LE(offset + 56));
    setReward(record, 1, data.readInt32LE(offset + 60), data.readInt32LE(offset + 64));
    setReward(record, 2, data.readInt32LE(offset + 68), data.readInt32LE(offset + 72));
    setReward(record, 3, data.readInt32LE(offset + 80), data.readInt32LE(offset + 84));
  }
}

function setReward(record, index, itemId, amount) {
  record.rewards[index] = normalizeItem(itemId);
  record.rewardAmounts[index] = itemId > 0 ? Math.max(0, amount) : 0;
}

function applyLegacyRewards() {
  for (const [questId, reward] of legacyRewards) {
    const record = getOrCreate(questId);
    if (!record.hasClientReward) {
      record.gold = reward.gold;
      record.exp = reward.exp;
      record.rewards = reward.rewards.map(normalizeItem);
      record.rewardAmounts = reward.rewardAmounts.map(amount => Math.max(0, amount));
    }
  }
}

function loadClientNpcStarterLinks() {
  const byFileId = buildFileIdIndex();
  const npcStarterIndexes = new Map();
  for (const entry of readZipEntries(path.join(clientRoot, "data", "NPC", "Episode", "NPC", "Nsr.bsz"))) {
    if (!entry.name.toLowerCase().endsWith(".nsr")) continue;
    const data = decodeClientBuffer(entry.data);
    if (data.length < 176) continue;
    const npcId = data.readInt32LE(4);
    const count = data.readInt32LE(40);
    if (npcId <= 0 || count <= 0 || count > 128 || 176 + count * 120 > data.length) continue;
    let starterIndex = npcStarterIndexes.get(npcId) || 0;
    for (let index = 0; index < count; index++) {
      const offset = 176 + index * 120;
      const kind = data.readInt16LE(offset + 6);
      const status = data.readInt16LE(offset + 10);
      if (status !== 150 || kind !== 2) continue;
      const fileId = data.readInt32LE(offset + 24);
      const record = byFileId.get(fileId);
      if (!record) continue;
      addStarterLink(npcId, starterIndex, record.questId, record.name, false);
      starterIndex++;
    }
    npcStarterIndexes.set(npcId, starterIndex);
  }
}

function loadClientQuestBookFileIds() {
  const bookPath = path.join(clientRoot, "data", "NPC", "Episode", "BOOK");
  if (!fs.existsSync(bookPath)) return;

  for (const fileName of fs.readdirSync(bookPath)) {
    const match = /^(\d+)/.exec(fileName);
    if (match) clientQuestBookFileIds.add(Number.parseInt(match[1], 10));
  }
}

function isQuestClientSafe(record) {
  if (!record || record.questId <= 0 || record.fileId < 0)
    return false;

  if (clientQuestBookFileIds.size > 0 && !clientQuestBookFileIds.has(record.fileId))
    return false;

  return record.completeNpcId > 0 || hasObjective(record);
}

function hasObjective(record) {
  for (let index = 0; index < 5; index++) {
    if (record.required[index] > 0 && (record.monsters[index] > 0 || record.items[index] > 0))
      return true;
  }
  return false;
}

function removeUnsafeStarterLinks() {
  for (const [key, link] of [...starterLinks.entries()]) {
    const record = records.get(link.questId);
    if (isQuestClientSafe(record)) continue;

    starterLinks.delete(key);
    if (record && record.npcId === link.npcId && record.questNum === link.questNum) {
      record.npcId = -1;
      record.questNum = -1;
    }
  }
}

function sqlValue(value) {
  if (typeof value === "number") return Number.isFinite(value) ? String(value) : "0";
  const text = String(value ?? "").replace(/\\/g, "\\\\").replace(/'/g, "\\'");
  return `'${text}'`;
}

function templateRow(record) {
  const values = [
    record.questId, record.fileId, record.name, record.npcId, record.questNum,
    record.completeNpcId, record.completeQuestNum, record.level, record.gold, record.exp,
    ...record.monsters,
    record.items[0], record.required[0], record.itemChances[0],
    record.items[1], record.required[1], record.itemChances[1],
    record.items[2], record.required[2], record.itemChances[2],
    record.items[3], record.required[3], record.itemChances[3],
    record.items[4], record.required[4], record.itemChances[4],
    record.rewards[0], record.rewardAmounts[0], record.rewardOptional[0],
    record.rewards[1], record.rewardAmounts[1], record.rewardOptional[1],
    record.rewards[2], record.rewardAmounts[2], record.rewardOptional[2],
    record.rewards[3], record.rewardAmounts[3], record.rewardOptional[3],
    record.rewards[4], record.rewardAmounts[4], record.rewardOptional[4],
    record.repeatCount,
    record.hiddenItems[0], record.hiddenAmounts[0], record.hiddenGivers[0],
    record.hiddenItems[1], record.hiddenAmounts[1], record.hiddenGivers[1],
    record.hiddenItems[2], record.hiddenAmounts[2], record.hiddenGivers[2],
    record.startItems[0], record.startItems[1], record.startItems[2],
    record.xpPerItem, record.afterStage, record.afterComplete, record.doSort, record.seqId, record.initState
  ];
  return `(${values.map(sqlValue).join(", ")})`;
}

function questRecordRow(record) {
  const values = [
    record.name, record.questId, record.level, record.gold, record.exp,
    ...record.monsters,
    record.items[0], record.itemAmounts[0],
    record.items[1], record.itemAmounts[1],
    record.items[2], record.itemAmounts[2],
    record.items[3], record.itemAmounts[3],
    record.items[4], record.itemAmounts[4],
    ...record.required,
    record.questType || 1
  ];
  return `(${values.map(sqlValue).join(", ")})`;
}

function rewardRow(record) {
  const values = [
    record.questId, record.gold, record.exp,
    record.rewards[0], record.rewardAmounts[0],
    record.rewards[1], record.rewardAmounts[1],
    record.rewards[2], record.rewardAmounts[2],
    record.rewards[3], record.rewardAmounts[3]
  ];
  return `(${values.map(sqlValue).join(", ")})`;
}

function linkRow(id, link) {
  return `(${[id, link.npcId, link.questNum, link.questId, link.name || getOrCreate(link.questId).name].map(sqlValue).join(", ")})`;
}

function chunkedInsert(table, columns, rows, chunkSize = 250) {
  const lines = [];
  for (let index = 0; index < rows.length; index += chunkSize) {
    const chunk = rows.slice(index, index + chunkSize);
    lines.push(`INSERT INTO \`${table}\` (${columns.map(column => `\`${column}\``).join(", ")}) VALUES\n${chunk.join(",\n")};`);
  }
  return lines.join("\n\n");
}

function generateSql() {
  const allRecords = [...records.values()]
    .filter(record => record.questId > 0 && record.fileId >= 0)
    .sort((a, b) => a.questId - b.questId);

  const startRows = [...starterLinks.values()]
    .sort((a, b) => a.npcId - b.npcId || a.questNum - b.questNum || a.questId - b.questId)
    .map((link, index) => linkRow(100000 + index, link));
  const completeRows = [...completerLinks.values()]
    .sort((a, b) => a.npcId - b.npcId || a.questNum - b.questNum || a.questId - b.questId)
    .map((link, index) => linkRow(200000 + index, link));
  const questIdMin = allRecords.length ? allRecords[0].questId : 2001;
  const questIdMax = allRecords.length ? allRecords[allRecords.length - 1].questId : 2001;

  const sql = [];
  sql.push("-- Generated by Tools/GenerateAsda2QuestDb.js");
  sql.push("-- This stores client quest data in DB tables so the server does not need client-file lookup during normal quest use.");
  sql.push("SET NAMES utf8;");
  sql.push("CREATE TABLE IF NOT EXISTS `asda2questtemplate` (\n" +
    "  `Id` int NOT NULL AUTO_INCREMENT,\n" +
    "  `QuestId` int NOT NULL,\n" +
    "  `FileId` int NOT NULL,\n" +
    "  `Name` varchar(255) CHARACTER SET utf8 COLLATE utf8_general_ci NOT NULL,\n" +
    "  `NpcId` int NOT NULL,\n" +
    "  `QuestNum` int NOT NULL,\n" +
    "  `CompleteNpcId` int NOT NULL,\n" +
    "  `CompleteQuestNum` int NOT NULL,\n" +
    "  `Level` int NOT NULL,\n" +
    "  `Gold` int NOT NULL DEFAULT 0,\n" +
    "  `Exp` int NOT NULL DEFAULT 0,\n" +
    "  `Monster1` int NULL DEFAULT -1, `Monster2` int NULL DEFAULT -1, `Monster3` int NULL DEFAULT -1, `Monster4` int NULL DEFAULT -1, `Monster5` int NULL DEFAULT -1,\n" +
    "  `Item1` int NULL DEFAULT -1, `Item1Amount` int NULL DEFAULT 0, `Item1Chance` int NOT NULL DEFAULT 100,\n" +
    "  `Item2` int NULL DEFAULT -1, `Item2Amount` int NULL DEFAULT 0, `Item2Chance` int NOT NULL DEFAULT 100,\n" +
    "  `Item3` int NULL DEFAULT -1, `Item3Amount` int NULL DEFAULT 0, `Item3Chance` int NOT NULL DEFAULT 100,\n" +
    "  `Item4` int NULL DEFAULT -1, `Item4Amount` int NULL DEFAULT 0, `Item4Chance` int NOT NULL DEFAULT 100,\n" +
    "  `Item5` int NULL DEFAULT -1, `Item5Amount` int NULL DEFAULT 0, `Item5Chance` int NOT NULL DEFAULT 100,\n" +
    "  `Reward1` int NULL DEFAULT -1, `Reward1Amount` int NULL DEFAULT 0, `Reward1OP` tinyint(1) NULL DEFAULT 0,\n" +
    "  `Reward2` int NULL DEFAULT -1, `Reward2Amount` int NULL DEFAULT 0, `Reward2OP` tinyint(1) NULL DEFAULT 0,\n" +
    "  `Reward3` int NULL DEFAULT -1, `Reward3Amount` int NULL DEFAULT 0, `Reward3OP` tinyint(1) NULL DEFAULT 0,\n" +
    "  `Reward4` int NULL DEFAULT -1, `Reward4Amount` int NULL DEFAULT 0, `Reward4OP` tinyint(1) NULL DEFAULT 0,\n" +
    "  `Reward5` int NULL DEFAULT -1, `Reward5Amount` int NULL DEFAULT 0, `Reward5OP` tinyint(1) NULL DEFAULT 0,\n" +
    "  `RepeatCount` int NOT NULL DEFAULT 0,\n" +
    "  `HiddenItem1` int NOT NULL DEFAULT -1, `HiddenItem1Amount` int NOT NULL DEFAULT 0, `HiddenItem1Giver` int NOT NULL DEFAULT -1,\n" +
    "  `HiddenItem2` int NOT NULL DEFAULT -1, `HiddenItem2Amount` int NOT NULL DEFAULT 0, `HiddenItem2Giver` int NOT NULL DEFAULT -1,\n" +
    "  `HiddenItem3` int NOT NULL DEFAULT -1, `HiddenItem3Amount` int NOT NULL DEFAULT 0, `HiddenItem3Giver` int NOT NULL DEFAULT -1,\n" +
    "  `StartItem1` int NOT NULL DEFAULT -1, `StartItem2` int NOT NULL DEFAULT -1, `StartItem3` int NOT NULL DEFAULT -1,\n" +
    "  `XpPerItem` int NOT NULL DEFAULT 0, `AfterStage` int NOT NULL DEFAULT 1, `AfterComplete` int NOT NULL DEFAULT 1,\n" +
    "  `DoSort` tinyint(1) NOT NULL DEFAULT 1, `SeqId` int NOT NULL DEFAULT -1, `InitState` int NOT NULL DEFAULT 0,\n" +
    "  PRIMARY KEY (`Id`)\n" +
    ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;");
  sql.push("CREATE TABLE IF NOT EXISTS `asda2questnpc` (`id` int NOT NULL, `npcid` int NOT NULL, `questnum` int NOT NULL, `questid` int NOT NULL, `questname` varchar(255) CHARACTER SET utf8 COLLATE utf8_unicode_ci NOT NULL, PRIMARY KEY (`id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8;");
  sql.push("CREATE TABLE IF NOT EXISTS `asda2questrewardnpc` (`id` int NOT NULL, `npcid` int NOT NULL, `questnum` int NOT NULL, `questid` int NOT NULL, `questname` varchar(255) CHARACTER SET utf8 COLLATE utf8_unicode_ci NOT NULL, PRIMARY KEY (`id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8;");
  sql.push("CREATE TABLE IF NOT EXISTS `asda2questrecord` (`QuestName` varchar(255) CHARACTER SET utf8 COLLATE utf8_bin NOT NULL, `Id` int NOT NULL, `Level` int NOT NULL, `Gold` int NOT NULL, `Exp` int NOT NULL, `Monster1Id` int NOT NULL, `Monster2Id` int NOT NULL, `Monster3Id` int NOT NULL, `Monster4Id` int NOT NULL, `Monster5Id` int NOT NULL, `Item1Id` int NOT NULL, `Item1Amount` int NOT NULL, `Item2Id` int NOT NULL, `Item2Amount` int NOT NULL, `Item3Id` int NOT NULL, `Item3Amount` int NOT NULL, `Item4Id` int NOT NULL, `Item4Amount` int NOT NULL, `Item5Id` int NOT NULL, `Item5Amount` int NOT NULL, `Item1ReqAmount` int NOT NULL, `Item2ReqAmount` int NOT NULL, `Item3ReqAmount` int NOT NULL, `Item4ReqAmount` int NOT NULL, `Item5ReqAmount` int NOT NULL, `QuestType` int NOT NULL, PRIMARY KEY (`Id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8;");
  sql.push("CREATE TABLE IF NOT EXISTS `asda2questrewardtable` (`Id` int NOT NULL, `Gold` int NOT NULL, `Exp` int NOT NULL, `Item1Id` int NOT NULL, `Item1Amount` int NOT NULL, `Item2Id` int NOT NULL, `Item2Amount` int NOT NULL, `Item3Id` int NOT NULL, `Item3Amount` int NOT NULL, `Item4Id` int NOT NULL, `Item4Amount` int NOT NULL, PRIMARY KEY (`Id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8;");
  sql.push(`DELETE FROM \`asda2questtemplate\` WHERE \`QuestId\` BETWEEN ${questIdMin} AND ${questIdMax};`);
  sql.push(`DELETE FROM \`asda2questrecord\` WHERE \`Id\` BETWEEN ${questIdMin} AND ${questIdMax};`);
  sql.push(`DELETE FROM \`asda2questrewardtable\` WHERE \`Id\` BETWEEN ${questIdMin} AND ${questIdMax};`);
  sql.push(`DELETE FROM \`asda2questnpc\` WHERE \`questid\` BETWEEN ${questIdMin} AND ${questIdMax};`);
  sql.push(`DELETE FROM \`asda2questrewardnpc\` WHERE \`questid\` BETWEEN ${questIdMin} AND ${questIdMax};`);
  sql.push(chunkedInsert("asda2questtemplate", templateColumns, allRecords.map(templateRow)));
  sql.push(chunkedInsert("asda2questrecord",
    ["QuestName", "Id", "Level", "Gold", "Exp", "Monster1Id", "Monster2Id", "Monster3Id", "Monster4Id", "Monster5Id", "Item1Id", "Item1Amount", "Item2Id", "Item2Amount", "Item3Id", "Item3Amount", "Item4Id", "Item4Amount", "Item5Id", "Item5Amount", "Item1ReqAmount", "Item2ReqAmount", "Item3ReqAmount", "Item4ReqAmount", "Item5ReqAmount", "QuestType"],
    allRecords.map(questRecordRow)));
  sql.push(chunkedInsert("asda2questrewardtable",
    ["Id", "Gold", "Exp", "Item1Id", "Item1Amount", "Item2Id", "Item2Amount", "Item3Id", "Item3Amount", "Item4Id", "Item4Amount"],
    allRecords.map(rewardRow)));
  if (startRows.length) sql.push(chunkedInsert("asda2questnpc", ["id", "npcid", "questnum", "questid", "questname"], startRows));
  if (completeRows.length) sql.push(chunkedInsert("asda2questrewardnpc", ["id", "npcid", "questnum", "questid", "questname"], completeRows));
  sql.push(`-- Summary: templates=${allRecords.length}, starters=${startRows.length}, completers=${completeRows.length}`);
  fs.writeFileSync(outputPath, sql.filter(Boolean).join("\n\n") + "\n", "utf8");
  return { templates: allRecords.length, starters: startRows.length, completers: completeRows.length, outputPath };
}

loadOldSql(oldSqlPath);
loadOldSql(sourceSqlPath);
loadEpisodeTable();
loadRewardTable();
applyLegacyRewards();
loadClientNpcStarterLinks();
loadClientQuestBookFileIds();
removeUnsafeStarterLinks();
const summary = generateSql();
console.log(JSON.stringify(summary, null, 2));
