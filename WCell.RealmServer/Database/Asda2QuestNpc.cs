using Castle.ActiveRecord;
using Castle.ActiveRecord.Queries;
using NHibernate.Criterion;
using NLog;
using WCell.Core.Database;
using WCell.RealmServer.Asda2Quests;

namespace WCell.RealmServer.Database
{
  [ActiveRecord(Access = PropertyAccess.Property, Table = "asda2questnpc")]
  public class Asda2QuestNpc : WCellRecord<Asda2QuestNpc>
  {
    private static readonly Logger log = LogManager.GetCurrentClassLogger();
    private static bool _useCurrentProgressSqlLookups = true;
    private static bool _useLegacyProgressSqlLookups = true;
    private static bool _useTemplateLookups = true;

    [PrimaryKey(PrimaryKeyType.Assigned, "id")]
    public int id { get; set; }

    [Property(NotNull = true)]
    public int npcid { get; set; }

    [Property(NotNull = true)]
    public int questnum { get; set; }

    [Property(NotNull = true)]
    public int questid { get; set; }

    [Property(NotNull = true)]
    public string questname { get; set; }

    public static Asda2QuestNpc GetQuestId(int npcid, int questnum)
    {
      return FindOne((ICriterion) ((AbstractCriterion) Restrictions.Eq(nameof(npcid), npcid) &&
                                   (AbstractCriterion) Restrictions.Eq(nameof(questnum), questnum)));
    }

    public static int GetQuest(int npcId, int questNum)
    {
      try
      {
        Asda2QuestNpc record = GetQuestId(npcId, questNum);
        if(record != null)
          return record.questid;
      }
      catch
      {
      }

      try
      {
        int questId = GetQuestSql(npcId, questNum);
        if(questId > 0)
          return questId;
      }
      catch
      {
      }

      if(_useTemplateLookups)
      {
        try
        {
          Asda2QuestTemplateRecord template = Asda2QuestTemplateRecord.GetByStarter(npcId, questNum);
          if(template != null)
            return template.QuestId;
        }
        catch
        {
          _useTemplateLookups = false;
        }
      }

      return Asda2QuestFallbackData.GetStarterQuestId(npcId, questNum);
    }

    private static int GetQuestSql(int npcId, int questNum)
    {
      string sql = string.Format("SELECT {0} FROM {1} WHERE {2} = {3} AND {4} = {5} LIMIT 1",
        QuoteColumn("questid"),
        QuoteTable("asda2questnpc"),
        QuoteColumn("npcid"),
        npcId,
        QuoteColumn("questnum"),
        questNum);

      return new ScalarQuery<int>(typeof(Asda2QuestNpc), QueryLanguage.Sql, sql).Execute();
    }

    public static bool HasAvailableStarter(int npcId, uint ownerId)
    {
      return HasAvailableStarter(npcId, ownerId, int.MaxValue);
    }

    public static bool HasAvailableStarter(int npcId, uint ownerId, int characterLevel)
    {
      if(npcId <= 0)
        return false;

      return HasAvailableStarterWithCurrentProgress(npcId, ownerId, characterLevel) ||
             HasAvailableStarterWithLegacyProgress(npcId, ownerId, characterLevel) ||
             Asda2QuestTemplateRecord.HasAvailableStarter(npcId, ownerId, characterLevel) ||
             HasSafeFallbackStarter(npcId, ownerId, characterLevel);
    }

    private static bool HasSafeFallbackStarter(int npcId, uint ownerId, int characterLevel)
    {
      for(int questNum = 0; questNum <= 31; questNum++)
      {
        int questId = Asda2QuestFallbackData.GetStarterQuestIdForCharacter(npcId, questNum, ownerId, characterLevel);
        if(questId <= 0)
          continue;

        Asda2QuestTemplateInfo template = Asda2QuestTemplateResolver.GetStandard(questId);
        if(Asda2QuestMgr.IsQuestClientSafe(template, template == null ? -1 : template.FileId))
          return true;
      }

      return false;
    }

    private static bool HasAvailableStarterWithCurrentProgress(int npcId, uint ownerId, int characterLevel)
    {
      if(!_useCurrentProgressSqlLookups)
        return false;

      try
      {
        string sql = string.Format(
          "SELECT COUNT(*) FROM {0} n JOIN {1} q ON q.{2} = n.{3} " +
          "WHERE n.{4} = {5} AND q.{6} <= {7} AND NOT EXISTS " +
          "(SELECT 1 FROM {8} r WHERE r.{9} = {10} AND r.{11} = n.{3} AND " +
          "((r.{12} >= 1 AND r.{12} <= 12 AND r.{13} < 2) OR r.{13} >= 2))",
          QuoteTable("asda2questnpc"),
          QuoteTable("asda2questrecord"),
          QuoteColumn("Id"),
          QuoteColumn("questid"),
          QuoteColumn("npcid"),
          npcId,
          QuoteColumn("Level"),
          characterLevel,
          QuoteTable("asda2questrecords"),
          QuoteColumn("OwnerId"),
          ownerId,
          QuoteColumn("QuestId"),
          QuoteColumn("Slot"),
          QuoteColumn("Completed"));

        return ExecuteCount(sql) > 0;
      }
      catch
      {
        _useCurrentProgressSqlLookups = false;
        return false;
      }
    }

    private static bool HasAvailableStarterWithLegacyProgress(int npcId, uint ownerId, int characterLevel)
    {
      if(!_useLegacyProgressSqlLookups)
        return false;

      try
      {
        string sql = string.Format(
          "SELECT COUNT(*) FROM {0} n JOIN {1} q ON q.{2} = n.{3} " +
          "WHERE n.{4} = {5} AND q.{6} <= {7} AND NOT EXISTS " +
          "(SELECT 1 FROM {8} r WHERE r.{9} = {10} AND r.{11} = n.{3} AND r.{12} < 2) " +
          "AND NOT EXISTS " +
          "(SELECT 1 FROM {13} cr WHERE cr.{9} = {10} AND cr.{14} = n.{3} AND " +
          "((cr.{15} >= 1 AND cr.{15} <= 12 AND cr.{16} < 2) OR cr.{16} >= 2)) " +
          "AND NOT EXISTS " +
          "(SELECT 1 FROM {17} c WHERE c.{9} = {10} AND c.{18} = n.{3})",
          QuoteTable("asda2questnpc"),
          QuoteTable("asda2questrecord"),
          QuoteColumn("Id"),
          QuoteColumn("questid"),
          QuoteColumn("npcid"),
          npcId,
          QuoteColumn("Level"),
          characterLevel,
          QuoteTable("questrecord"),
          QuoteColumn("OwnerId"),
          ownerId,
          QuoteColumn("QuestTemplateId"),
          QuoteColumn("CompleteStatus"),
          QuoteTable("asda2questrecords"),
          QuoteColumn("QuestId"),
          QuoteColumn("Slot"),
          QuoteColumn("Completed"),
          QuoteTable("asda2completedquests"),
          QuoteColumn("SpellId"));

        return ExecuteCount(sql) > 0;
      }
      catch
      {
        _useLegacyProgressSqlLookups = false;
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
