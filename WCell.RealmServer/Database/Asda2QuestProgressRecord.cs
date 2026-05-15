using System;
using System.Linq;
using Castle.ActiveRecord;
using NHibernate.Criterion;
using WCell.Core.Database;

namespace WCell.RealmServer.Database
{
    [ActiveRecord(Access = PropertyAccess.Property, Table = "asda2questrecords")]
    public class Asda2QuestProgressRecord : WCellRecord<Asda2QuestProgressRecord>
    {
        private static readonly NHIdGenerator _idGenerator =
            new NHIdGenerator(typeof(Asda2QuestProgressRecord), nameof(QuestRecordId), 1L);

        [Field("OwnerId", Access = PropertyAccess.FieldCamelcase, NotNull = false)]
        private long _ownerId;

        [Field("QuestId", Access = PropertyAccess.FieldCamelcase, NotNull = false)]
        private int _questId;

        [Field("QuestFileId", Access = PropertyAccess.FieldCamelcase, NotNull = false)]
        private int _questFileId;

        [Field("QuestTemplateId", Access = PropertyAccess.FieldCamelcase, NotNull = false)]
        private int _questTemplateMarker;

        [Field("Stage", Access = PropertyAccess.FieldCamelcase, NotNull = false)]
        private int _questStage;

        [Field("Completed", Access = PropertyAccess.FieldCamelcase, NotNull = false)]
        private int _completeStatus;

        [Field("Item1", Access = PropertyAccess.FieldCamelcase, NotNull = false)]
        private int _item1Id;

        [Field("Item2", Access = PropertyAccess.FieldCamelcase, NotNull = false)]
        private int _item2Id;

        [Field("Item3", Access = PropertyAccess.FieldCamelcase, NotNull = false)]
        private int _item3Id;

        [Field("Item4", Access = PropertyAccess.FieldCamelcase, NotNull = false)]
        private int _item4Id;

        [Field("Item5", Access = PropertyAccess.FieldCamelcase, NotNull = false)]
        private int _item5Id;

        [Field("Amount1", Access = PropertyAccess.FieldCamelcase, NotNull = false)]
        private int _item1Amount;

        [Field("Amount2", Access = PropertyAccess.FieldCamelcase, NotNull = false)]
        private int _item2Amount;

        [Field("Amount3", Access = PropertyAccess.FieldCamelcase, NotNull = false)]
        private int _item3Amount;

        [Field("Amount4", Access = PropertyAccess.FieldCamelcase, NotNull = false)]
        private int _item4Amount;

        [Field("Amount5", Access = PropertyAccess.FieldCamelcase, NotNull = false)]
        private int _item5Amount;

        [Field("ItemGiver1", Access = PropertyAccess.FieldCamelcase, NotNull = false)]
        private int _monster1;

        [Field("ItemGiver2", Access = PropertyAccess.FieldCamelcase, NotNull = false)]
        private int _monster2;

        [Field("ItemGiver3", Access = PropertyAccess.FieldCamelcase, NotNull = false)]
        private int _monster3;

        public Asda2QuestProgressRecord()
        {
        }

        public Asda2QuestProgressRecord(int questId, int questFileId, uint ownerId, int slot,
            int item1, int item2, int item3, int item4, int item5,
            int item1Amount, int item2Amount, int item3Amount, int item4Amount, int item5Amount,
            int monster1, int monster2, int monster3, int monster4, int monster5,
            int questType, int completeStatus)
        {
            QuestRecordId = NextId();
            QuestTemplateId = questId;
            QuestFileId = questFileId;
            QuestTemplateMarker = questId;
            OwnerId = ownerId;
            Slot = slot;
            Item1Id = item1;
            Item2Id = item2;
            Item3Id = item3;
            Item4Id = item4;
            Item5Id = item5;
            Item1Amount = item1Amount;
            Item2Amount = item2Amount;
            Item3Amount = item3Amount;
            Item4Amount = item4Amount;
            Item5Amount = item5Amount;
            Monster1 = monster1;
            Monster2 = monster2;
            Monster3 = monster3;
            Monster4 = monster4;
            Monster5 = monster5;
            QuestType = questType;
            CompleteStatus = completeStatus;
            QuestStage = completeStatus == 1 ? 1 : 2;
            State = RecordState.New;
        }

        public static long NextId()
        {
            return _idGenerator.Next();
        }

        [PrimaryKey(PrimaryKeyType.Assigned, "Guid")]
        public long QuestRecordId { get; set; }

        public uint OwnerId
        {
            get { return (uint)_ownerId; }
            set { _ownerId = value; }
        }

        public int QuestTemplateId
        {
            get { return _questId; }
            set { _questId = value; }
        }

        public int QuestFileId
        {
            get { return _questFileId; }
            set { _questFileId = value; }
        }

        public int QuestTemplateMarker
        {
            get { return _questTemplateMarker; }
            set { _questTemplateMarker = value; }
        }

        [Property(NotNull = false)]
        public int Slot { get; set; }

        public int QuestStage
        {
            get { return _questStage; }
            set { _questStage = value; }
        }

        public int CompleteStatus
        {
            get { return _completeStatus; }
            set { _completeStatus = value; }
        }

        public int Item1Id
        {
            get { return _item1Id; }
            set { _item1Id = value; }
        }

        public int Item2Id
        {
            get { return _item2Id; }
            set { _item2Id = value; }
        }

        public int Item3Id
        {
            get { return _item3Id; }
            set { _item3Id = value; }
        }

        public int Item4Id
        {
            get { return _item4Id; }
            set { _item4Id = value; }
        }

        public int Item5Id
        {
            get { return _item5Id; }
            set { _item5Id = value; }
        }

        public int Item1Amount
        {
            get { return _item1Amount; }
            set { _item1Amount = value; }
        }

        public int Item2Amount
        {
            get { return _item2Amount; }
            set { _item2Amount = value; }
        }

        public int Item3Amount
        {
            get { return _item3Amount; }
            set { _item3Amount = value; }
        }

        public int Item4Amount
        {
            get { return _item4Amount; }
            set { _item4Amount = value; }
        }

        public int Item5Amount
        {
            get { return _item5Amount; }
            set { _item5Amount = value; }
        }

        public int Monster1
        {
            get { return _monster1; }
            set { _monster1 = value; }
        }

        public int Monster2
        {
            get { return _monster2; }
            set { _monster2 = value; }
        }

        public int Monster3
        {
            get { return _monster3; }
            set { _monster3 = value; }
        }

        public int Monster4 { get; set; }

        public int Monster5 { get; set; }

        public int QuestType { get; set; }

        public bool IsActive
        {
            get { return Slot >= 1 && Slot <= 12 && CompleteStatus < 2; }
        }

        public static Asda2QuestProgressRecord[] GetQuestRecordForCharacter(uint chrId)
        {
            return FindAllByProperty("_ownerId", (long)chrId);
        }

        public static Asda2QuestProgressRecord[] GetActiveQuestRecordsForCharacter(uint chrId)
        {
            return GetQuestRecordForCharacter(chrId).Where(record => record != null && record.IsActive).ToArray();
        }

        public static Asda2QuestProgressRecord GetQuestRecord(uint ownerId, int questId)
        {
            return GetQuestRecordForCharacter(ownerId)
                .Where(record => record != null && record.QuestTemplateId == questId)
                .OrderByDescending(record => record.QuestRecordId)
                .FirstOrDefault();
        }

        public static Asda2QuestProgressRecord GetActiveQuestRecord(uint ownerId, int questId)
        {
            return GetActiveQuestRecordsForCharacter(ownerId)
                .Where(record => record.QuestTemplateId == questId)
                .OrderByDescending(record => record.QuestRecordId)
                .FirstOrDefault();
        }

        public static Asda2QuestProgressRecord GetQuestRecordBySlot(uint ownerId, int slot)
        {
            return GetActiveQuestRecordsForCharacter(ownerId)
                .Where(record => record.Slot == slot)
                .OrderByDescending(record => record.QuestRecordId)
                .FirstOrDefault();
        }
    }
}
