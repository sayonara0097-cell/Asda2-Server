using Castle.ActiveRecord;
using Castle.ActiveRecord.Queries;
using NHibernate.Criterion;
using WCell.Core.Database;

namespace WCell.RealmServer.Database
{
    [ActiveRecord(Table = "asda2questtemplate", Access = PropertyAccess.Property)]
    public class Asda2QuestTemplateRecord : WCellRecord<Asda2QuestTemplateRecord>
    {
        private static bool _useSqlLookups = true;

        [PrimaryKey(PrimaryKeyType.Assigned)]
        public int Id { get; set; }

        [Property(NotNull = true)]
        public int QuestId { get; set; }

        [Property(NotNull = true)]
        public int FileId { get; set; }

        [Property(NotNull = false)]
        public string Name { get; set; }

        [Property(NotNull = true)]
        public int NpcId { get; set; }

        [Property(NotNull = true)]
        public int QuestNum { get; set; }

        [Property(NotNull = true)]
        public int CompleteNpcId { get; set; }

        [Property(NotNull = true)]
        public int CompleteQuestNum { get; set; }

        [Property(NotNull = true)]
        public int Level { get; set; }

        [Property(NotNull = true)]
        public int Gold { get; set; }

        [Property(NotNull = true)]
        public int Exp { get; set; }

        [Property(NotNull = false)]
        public int Monster1 { get; set; }

        [Property(NotNull = false)]
        public int Monster2 { get; set; }

        [Property(NotNull = false)]
        public int Monster3 { get; set; }

        [Property(NotNull = false)]
        public int Monster4 { get; set; }

        [Property(NotNull = false)]
        public int Monster5 { get; set; }

        [Property(NotNull = false)]
        public int Item1 { get; set; }

        [Property(NotNull = false)]
        public int Item1Amount { get; set; }

        [Property(NotNull = false)]
        public int Item1Chance { get; set; }

        [Property(NotNull = false)]
        public int Item2 { get; set; }

        [Property(NotNull = false)]
        public int Item2Amount { get; set; }

        [Property(NotNull = false)]
        public int Item2Chance { get; set; }

        [Property(NotNull = false)]
        public int Item3 { get; set; }

        [Property(NotNull = false)]
        public int Item3Amount { get; set; }

        [Property(NotNull = false)]
        public int Item3Chance { get; set; }

        [Property(NotNull = false)]
        public int Item4 { get; set; }

        [Property(NotNull = false)]
        public int Item4Amount { get; set; }

        [Property(NotNull = false)]
        public int Item4Chance { get; set; }

        [Property(NotNull = false)]
        public int Item5 { get; set; }

        [Property(NotNull = false)]
        public int Item5Amount { get; set; }

        [Property(NotNull = false)]
        public int Item5Chance { get; set; }

        [Property(NotNull = false)]
        public int Reward1 { get; set; }

        [Property(NotNull = false)]
        public int Reward1Amount { get; set; }

        [Property(NotNull = false)]
        public int Reward1OP { get; set; }

        [Property(NotNull = false)]
        public int Reward2 { get; set; }

        [Property(NotNull = false)]
        public int Reward2Amount { get; set; }

        [Property(NotNull = false)]
        public int Reward2OP { get; set; }

        [Property(NotNull = false)]
        public int Reward3 { get; set; }

        [Property(NotNull = false)]
        public int Reward3Amount { get; set; }

        [Property(NotNull = false)]
        public int Reward3OP { get; set; }

        [Property(NotNull = false)]
        public int Reward4 { get; set; }

        [Property(NotNull = false)]
        public int Reward4Amount { get; set; }

        [Property(NotNull = false)]
        public int Reward4OP { get; set; }

        [Property(NotNull = false)]
        public int Reward5 { get; set; }

        [Property(NotNull = false)]
        public int Reward5Amount { get; set; }

        [Property(NotNull = false)]
        public int Reward5OP { get; set; }

        [Property(NotNull = true)]
        public int RepeatCount { get; set; }

        [Property(NotNull = false)]
        public int HiddenItem1 { get; set; }

        [Property(NotNull = false)]
        public int HiddenItem1Amount { get; set; }

        [Property(NotNull = false)]
        public int HiddenItem1Giver { get; set; }

        [Property(NotNull = false)]
        public int HiddenItem2 { get; set; }

        [Property(NotNull = false)]
        public int HiddenItem2Amount { get; set; }

        [Property(NotNull = false)]
        public int HiddenItem2Giver { get; set; }

        [Property(NotNull = false)]
        public int HiddenItem3 { get; set; }

        [Property(NotNull = false)]
        public int HiddenItem3Amount { get; set; }

        [Property(NotNull = false)]
        public int HiddenItem3Giver { get; set; }

        [Property(NotNull = false)]
        public int StartItem1 { get; set; }

        [Property(NotNull = false)]
        public int StartItem2 { get; set; }

        [Property(NotNull = false)]
        public int StartItem3 { get; set; }

        [Property(NotNull = false)]
        public int XpPerItem { get; set; }

        [Property(NotNull = false)]
        public int AfterStage { get; set; }

        [Property(NotNull = false)]
        public int AfterComplete { get; set; }

        [Property(NotNull = false)]
        public int DoSort { get; set; }

        [Property(NotNull = false)]
        public int SeqId { get; set; }

        [Property(NotNull = false)]
        public int InitState { get; set; }

        public static Asda2QuestTemplateRecord GetByQuestId(int questId)
        {
            return FindOne(Restrictions.Eq("QuestId", questId));
        }

        public static Asda2QuestTemplateRecord GetByStarter(int npcId, int questNum)
        {
            return FindOne(Restrictions.And(Restrictions.Eq("NpcId", npcId),
                Restrictions.Eq("QuestNum", questNum)));
        }

        public static Asda2QuestTemplateRecord[] GetAllByStarter(int npcId)
        {
            return FindAll(Restrictions.Eq("NpcId", npcId));
        }

        public static Asda2QuestTemplateRecord GetByCompleter(int npcId, int questNum)
        {
            return FindOne(Restrictions.And(Restrictions.Eq("CompleteNpcId", npcId),
                Restrictions.Eq("CompleteQuestNum", questNum)));
        }

        public static Asda2QuestTemplateRecord[] GetAllByCompleter(int npcId)
        {
            return FindAll(Restrictions.Eq("CompleteNpcId", npcId));
        }

        public static bool HasAvailableStarter(int npcId, uint ownerId, int characterLevel)
        {
            if (!_useSqlLookups)
                return false;

            try
            {
                var sql = string.Format(
                    "SELECT COUNT(*) FROM {0} q WHERE q.{1} = {2} AND q.{3} <= {4} AND NOT EXISTS " +
                    "(SELECT 1 FROM {5} r WHERE r.{6} = {7} AND r.{8} = q.{9} AND " +
                    "((r.{10} >= 1 AND r.{10} <= 12 AND r.{11} < 2) OR r.{11} >= 2))",
                    QuoteTable("asda2questtemplate"),
                    QuoteColumn("NpcId"),
                    npcId,
                    QuoteColumn("Level"),
                    characterLevel,
                    QuoteTable("asda2questrecords"),
                    QuoteColumn("OwnerId"),
                    ownerId,
                    QuoteColumn("QuestId"),
                    QuoteColumn("QuestId"),
                    QuoteColumn("Slot"),
                    QuoteColumn("Completed"));

                return ExecuteCount(sql) > 0;
            }
            catch
            {
                _useSqlLookups = false;
                return false;
            }
        }

        public static bool HasCompleter(int npcId, int questId)
        {
            if (!_useSqlLookups)
                return false;

            try
            {
                var sql = string.Format(
                    "SELECT COUNT(*) FROM {0} q WHERE q.{1} = {2} AND q.{3} = {4}",
                    QuoteTable("asda2questtemplate"),
                    QuoteColumn("CompleteNpcId"),
                    npcId,
                    QuoteColumn("QuestId"),
                    questId);

                return ExecuteCount(sql) > 0;
            }
            catch
            {
                _useSqlLookups = false;
                return false;
            }
        }

        private static long ExecuteCount(string sql)
        {
            return new ScalarQuery<long>(typeof(Asda2QuestNpc), QueryLanguage.Sql, sql).Execute();
        }

        private static string QuoteTable(string name)
        {
            return DatabaseUtil.Dialect.QuoteForTableName(name);
        }

        private static string QuoteColumn(string name)
        {
            return DatabaseUtil.Dialect.QuoteForColumnName(name);
        }
    }
}
