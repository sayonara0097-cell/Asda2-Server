using System.Collections.Generic;
using WCell.Core.Initialization;
using WCell.RealmServer.Content;
using WCell.Util.Variables;

namespace WCell.RealmServer.Asda2PetSystem
{
  public static class Asda2PetMgr
  {
    [NotVariable]public static PetTemplate[] PetTemplates = new PetTemplate[10000];

    [NotVariable]
    public static Dictionary<int, Dictionary<int, int[]>> ExpTable = new Dictionary<int, Dictionary<int, int[]>>();

    [NotVariable]public static Dictionary<int, Dictionary<int, Dictionary<int, Dictionary<int, int>>>> PetOptionValues =
      new Dictionary<int, Dictionary<int, Dictionary<int, Dictionary<int, int>>>>();

    [NotVariable]
    public static Dictionary<int, Dictionary<int, Dictionary<string, PetTemplate>>> PetTemplatesByRankAndRarity =
      new Dictionary<int, Dictionary<int, Dictionary<string, PetTemplate>>>();

    public static Dictionary<(int petId, int proff, int rank, int rarity), int> PetTemplateMap = new Dictionary<(int petId, int proff, int rank, int rarity), int>
    {
        // Pet ID 2411
        {(2411, 11, 0, 2), 3039},
        {(2411, 11, 0, 3), 1934},
        {(2411, 11, 1, 0), 1962},
        {(2411, 11, 2, 0), 1990},
        {(2411, 11, 3, 0), 2018},
        {(2411, 11, 4, 0), 2046},
        {(2411, 12, 0, 2), 3041},
        {(2411, 12, 0, 3), 1936},
        {(2411, 12, 1, 0), 1964},
        {(2411, 12, 2, 0), 1992},
        {(2411, 12, 3, 0), 2020},
        {(2411, 12, 4, 0), 2048},
        {(2411, 13, 0, 2), 3043},
        {(2411, 13, 0, 3), 1935},
        {(2411, 13, 1, 0), 1963},
        {(2411, 13, 2, 0), 1991},
        {(2411, 13, 3, 0), 2019},
        {(2411, 13, 4, 0), 2047},

        // Pet ID 2414
        {(2414, 14, 0, 2), 2050},
        {(2414, 14, 0, 3), 1938},
        {(2414, 14, 1, 0), 1966},
        {(2414, 14, 2, 0), 1994},
        {(2414, 14, 3, 0), 2022},
        {(2414, 14, 4, 0), 2050},
        {(2414, 15, 0, 2), 3047},
        {(2414, 15, 0, 3), 1937},
        {(2414, 15, 1, 0), 1965},
        {(2414, 15, 2, 0), 1993},
        {(2414, 15, 3, 0), 2021},
        {(2414, 15, 4, 0), 2049},

        // Pet ID 2417
        {(2417, 17, 0, 2), 3049},
        {(2417, 17, 0, 3), 1939},
        {(2417, 17, 1, 0), 1967},
        {(2417, 17, 2, 0), 1995},
        {(2417, 17, 3, 0), 2023},
        {(2417, 17, 4, 0), 2051},
        {(2417, 18, 0, 2), 3051},
        {(2417, 18, 0, 3), 1940},
        {(2417, 18, 1, 0), 1968},
        {(2417, 18, 2, 0), 1996},
        {(2417, 18, 3, 0), 2024},
        {(2417, 18, 4, 0), 2052},
        {(2417, 19, 0, 2), 3053},
        {(2417, 19, 0, 3), 1941},
        {(2417, 19, 1, 0), 1969},
        {(2417, 19, 2, 0), 1997},
        {(2417, 19, 3, 0), 2025},
        {(2417, 19, 4, 0), 2053},
    };

        [WCell.Core.Initialization.Initialization(InitializationPass.Third, Name = "Pet system")]
    public static void InitEntries()
    {
      ContentMgr.Load<PetTemplate>();
      ContentMgr.Load<PetExpTableRecord>();
      ContentMgr.Load<PetOptionValueRecord>();
    }
  }
}