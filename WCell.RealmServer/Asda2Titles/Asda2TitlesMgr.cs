using Castle.ActiveRecord;
using System;
using System.Collections.Generic;
using WCell.Core.Initialization;
using WCell.Core.Timers;
using WCell.RealmServer.Database;
using WCell.RealmServer.Global;

namespace WCell.RealmServer.Asda2Titles
{
  internal class Asda2TitlesMgr : IUpdatable
  {
    private static DateTime m_lastRatingUpdateDate = DateTime.MinValue;

    [WCell.Core.Initialization.Initialization(InitializationPass.Tenth, "Asda2 title system.")]
    public static void InitTitles()
    {
      Asda2TitlesMgr.TopRating();
      m_lastRatingUpdateDate = DateTime.Today;
      World.TaskQueue.RegisterUpdatableLater((IUpdatable) new Asda2TitlesMgr());
    }

    public static void TopRating()
    {
      List<CharacterRecord> characterRecordList =
        new List<CharacterRecord>((IEnumerable<CharacterRecord>) ActiveRecordBase<CharacterRecord>.FindAll());
      characterRecordList.Sort((Comparison<CharacterRecord>) ((a, b) => b.TitlePoints.CompareTo(a.TitlePoints)));
      for(int index = 0; index < characterRecordList.Count; ++index)
      {
        int rank = index + 1;
        if(characterRecordList[index].Rank == rank)
          continue;
        characterRecordList[index].Rank = rank;
        characterRecordList[index].SaveAndFlush();
      }
    }

    public void Update(int dt)
    {
      DateTime now = DateTime.Now;
      if(now.Hour != 3 || m_lastRatingUpdateDate.Date == now.Date)
        return;
      Asda2TitlesMgr.TopRating();
      m_lastRatingUpdateDate = now.Date;
    }
  }
}
