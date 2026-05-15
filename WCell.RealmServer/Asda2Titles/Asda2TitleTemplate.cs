using WCell.Util.Data;
using WCell.Util.Variables;

namespace WCell.RealmServer.Asda2Titles
{
  [DataHolder]
  public class Asda2TitleTemplate : IDataHolder
  {
    public const int MaxTitleCount = 512;
    public const int TitleBlockCount = MaxTitleCount / 32;

    [NotVariable]public static Asda2TitleTemplate[] Templates = new Asda2TitleTemplate[MaxTitleCount];

    public short Id { get; set; }

    public string Name { get; set; }

    public string LongDescr { get; set; }

    public string ShortDescr { get; set; }

    public byte Rarity { get; set; }

    public short Points { get; set; }

    public bool IsEnabled { get; set; }

    public byte Category { get; set; }

    public static bool IsValidTitleId(int titleId)
    {
      return titleId >= 0 && titleId < Templates.Length;
    }

    public static Asda2TitleTemplate GetTemplate(int titleId)
    {
      return IsValidTitleId(titleId) ? Templates[titleId] : null;
    }

    public void FinalizeDataHolder()
    {
      if(!IsValidTitleId((int) Id))
        return;

      Asda2TitleTemplate.Templates[(int) this.Id] = this;
    }
  }
}
