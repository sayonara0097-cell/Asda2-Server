using System;
using WCell.Constants.Achievements;
using WCell.RealmServer.Asda2Titles;
using WCell.RealmServer.Entities;
using WCell.Util.Commands;

namespace WCell.RealmServer.Commands
{
  public class Asda2TitlesCommands : RealmServerCommand
  {
    protected override void Initialize()
    {
      Init("Title");
      EnglishDescription = "Provides commands for managing achivements";
    }

    public class AddTitleCommand : SubCommand
    {
      protected override void Initialize()
      {
        Init("Add", "Create");
        EnglishParamInfo = "<achievement>";
        EnglishDescription =
          "Adds the given achievement entry id to the player completed achievement list.";
      }

      public override void Process(CmdTrigger<RealmServerCmdArgs> trigger)
      {
        if(!(trigger.Args.Target is Character))
        {
          trigger.Reply("Wrong target.");
        }
        else
        {
          if(trigger.Text.String.Contains("all"))
          {
            for(int index = 0; index < Asda2TitleTemplate.MaxTitleCount; ++index)
            {
              (trigger.Args.Target as Character).DiscoverTitle((Asda2TitleId) index);
              (trigger.Args.Target as Character).GetTitle((Asda2TitleId) index);
            }
          }

          uint num = trigger.Text.NextUInt(0U);
          if(num >= (uint) Asda2TitleTemplate.MaxTitleCount)
          {
            trigger.Reply("Wrong title id.");
          }
          else
          {
            (trigger.Args.Target as Character).DiscoverTitle((Asda2TitleId) num);
            (trigger.Args.Target as Character).GetTitle((Asda2TitleId) num);
            trigger.Reply("Done.");
          }
        }
      }

      public static bool AddAchievement(Character character, uint achievementEntryId)
      {
        character.Achievements.EarnAchievement(achievementEntryId);
        return true;
      }
    }

    public class DiscoverTitleCommand : SubCommand
    {
      protected override void Initialize()
      {
        Init("Discover", "d");
        EnglishParamInfo = "<achievement>";
        EnglishDescription =
          "Adds the given achievement entry id to the player completed achievement list.";
      }

      public override void Process(CmdTrigger<RealmServerCmdArgs> trigger)
      {
        uint num = trigger.Text.NextUInt(0U);
        if(num >= (uint) Asda2TitleTemplate.MaxTitleCount)
          trigger.Reply("Wrong title id.");
        else if(!(trigger.Args.Target is Character))
        {
          trigger.Reply("Wrong target.");
        }
        else
        {
          (trigger.Args.Target as Character).DiscoverTitle((Asda2TitleId) num);
          trigger.Reply("Done.");
        }
      }

      public static bool AddAchievement(Character character, uint achievementEntryId)
      {
        character.Achievements.EarnAchievement(achievementEntryId);
        return true;
      }
    }
  }
}
