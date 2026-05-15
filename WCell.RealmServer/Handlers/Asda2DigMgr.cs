using System.Collections.Generic;
using System;
using System.IO;
using WCell.Constants.Achievements;
using WCell.Constants.Items;
using WCell.Constants.World;
using WCell.Core.Initialization;
using WCell.RealmServer.Achievements;
using WCell.RealmServer.Asda2Looting;
using WCell.RealmServer.Content;
using WCell.RealmServer.Entities;
using WCell.RealmServer.Items;
using WCell.RealmServer.Looting;
using WCell.RealmServer.Network;
using WCell.Util;

namespace WCell.RealmServer.Handlers
{
  public class Asda2DigMgr
  {
    public static Dictionary<byte, MineTableRecord> MapDiggingTemplates = new Dictionary<byte, MineTableRecord>();

    public static Dictionary<byte, MineTableRecord> PremiumMapDiggingTemplates =
      new Dictionary<byte, MineTableRecord>();

    private static readonly object SafeZoneSync = new object();

    private static readonly Dictionary<MapId, SafeZoneDefinition> SafeZoneDefinitions =
      new Dictionary<MapId, SafeZoneDefinition>
      {
        { MapId.Alpia, new SafeZoneDefinition("alpen", "alpen.SIM", 117, 389) },
        { MapId.Silaris, new SafeZoneDefinition("silalis", "silalis.SIM", 393, 397) },
        { MapId.Flamio, new SafeZoneDefinition("flammio", "flammio.SIM", 135, 188) },
        { MapId.Aquaton, new SafeZoneDefinition("aquaton", "aquaton.SIM", 394, 342) }
      };

    private static readonly Dictionary<MapId, SafeZoneMask> SafeZoneMasks =
      new Dictionary<MapId, SafeZoneMask>();

    public static bool IsInTown(Character character)
    {
      if(character == null)
        return false;
      return IsInTown(character.MapId, character.Asda2X, character.Asda2Y);
    }

    public static bool IsInTown(MapId mapId, float x, float y)
    {
      SafeZoneMask safeZoneMask = GetSafeZoneMask(mapId);
      if(safeZoneMask != null)
        return safeZoneMask.Contains(x, y);

      return IsInTownRect(mapId, x, y);
    }

    private static bool IsInTownRect(MapId mapId, float x, float y)
    {
      switch(mapId)
      {
        case MapId.Alpia:
          return IsInRect(x, y, 35f, 200f, 300f, 440f);
        case MapId.Silaris:
          return IsInRect(x, y, 330f, 460f, 330f, 455f);
        case MapId.Flamio:
          return IsInRect(x, y, 80f, 205f, 130f, 255f);
        case MapId.Aquaton:
          return IsInRect(x, y, 330f, 465f, 285f, 390f);
        default:
          return false;
      }
    }

    private static bool IsInRect(float x, float y, float minX, float maxX, float minY, float maxY)
    {
      return x >= minX && x <= maxX && y >= minY && y <= maxY;
    }

    private static SafeZoneMask GetSafeZoneMask(MapId mapId)
    {
      lock(SafeZoneSync)
      {
        SafeZoneMask safeZoneMask;
        if(SafeZoneMasks.TryGetValue(mapId, out safeZoneMask))
          return safeZoneMask;

        SafeZoneDefinition definition;
        if(!SafeZoneDefinitions.TryGetValue(mapId, out definition))
          return null;

        safeZoneMask = LoadSafeZoneMask(definition);
        SafeZoneMasks.Add(mapId, safeZoneMask);
        return safeZoneMask;
      }
    }

    private static SafeZoneMask LoadSafeZoneMask(SafeZoneDefinition definition)
    {
      string path;
      if(!TryGetSafeZoneFilePath(definition, out path))
        return null;

      try
      {
        byte[] data = File.ReadAllBytes(path);
        if(data.Length < 8)
          return null;

        int width = ReadInt32(data, 0);
        int height = ReadInt32(data, 4);
        int tileCount = width * height;
        if(width <= 0 || height <= 0 || data.Length < 8 + tileCount * 4)
          return null;
        if(definition.SeedX < 0 || definition.SeedY < 0 ||
           definition.SeedX >= width || definition.SeedY >= height)
          return null;

        int seedIndex = definition.SeedY * width + definition.SeedX;
        int safeZoneValue = ReadSimValue(data, seedIndex);
        bool[] safeTiles = new bool[tileCount];
        bool[] seenTiles = new bool[tileCount];
        Queue<int> pendingTiles = new Queue<int>();
        pendingTiles.Enqueue(seedIndex);
        seenTiles[seedIndex] = true;

        while(pendingTiles.Count > 0)
        {
          int tile = pendingTiles.Dequeue();
          if(ReadSimValue(data, tile) != safeZoneValue)
            continue;

          safeTiles[tile] = true;
          int x = tile % width;
          int y = tile / width;
          AddSafeZoneTile(data, pendingTiles, seenTiles, safeZoneValue, width, height, x - 1, y);
          AddSafeZoneTile(data, pendingTiles, seenTiles, safeZoneValue, width, height, x + 1, y);
          AddSafeZoneTile(data, pendingTiles, seenTiles, safeZoneValue, width, height, x, y - 1);
          AddSafeZoneTile(data, pendingTiles, seenTiles, safeZoneValue, width, height, x, y + 1);
        }

        return new SafeZoneMask(width, height, safeTiles);
      }
      catch
      {
        return null;
      }
    }

    private static void AddSafeZoneTile(byte[] data, Queue<int> pendingTiles, bool[] seenTiles,
      int safeZoneValue, int width, int height, int x, int y)
    {
      if(x < 0 || y < 0 || x >= width || y >= height)
        return;

      int tile = y * width + x;
      if(seenTiles[tile] || ReadSimValue(data, tile) != safeZoneValue)
        return;

      seenTiles[tile] = true;
      pendingTiles.Enqueue(tile);
    }

    private static bool TryGetSafeZoneFilePath(SafeZoneDefinition definition, out string path)
    {
      foreach(string basePath in GetSafeZoneBasePaths())
      {
        if(string.IsNullOrEmpty(basePath))
          continue;

        string candidate = Path.Combine(basePath, definition.DirectoryName, definition.FileName);
        if(File.Exists(candidate))
        {
          path = candidate;
          return true;
        }
      }

      path = null;
      return false;
    }

    private static string[] GetSafeZoneBasePaths()
    {
      string appBase = AppDomain.CurrentDomain.BaseDirectory;
      string current = Environment.CurrentDirectory;
      return new[]
      {
        Path.Combine(appBase, "map"),
        Path.Combine(appBase, "Content", "map"),
        Path.Combine(appBase, "..", "..", "..", "Asda2 - Client", "map"),
        Path.Combine(current, "map"),
        Path.Combine(current, "Content", "map"),
        Path.Combine(current, "..", "..", "..", "Asda2 - Client", "map"),
        Path.Combine(current, "..", "Asda2 - Client", "map")
      };
    }

    private static int ReadSimValue(byte[] data, int tileIndex)
    {
      return ReadInt32(data, 8 + tileIndex * 4);
    }

    private static int ReadInt32(byte[] data, int offset)
    {
      return data[offset] |
             data[offset + 1] << 8 |
             data[offset + 2] << 16 |
             data[offset + 3] << 24;
    }

    public static MineTableRecord GetDiggingTemplate(MapId mapId, bool premium)
    {
      Dictionary<byte, MineTableRecord> templates =
        premium ? PremiumMapDiggingTemplates : MapDiggingTemplates;
      MineTableRecord template;
      return templates.TryGetValue((byte) mapId, out template) ? template : null;
    }

    public static void ProcessDig(IRealmClient client)
    {
      if(client.ActiveCharacter == null)
        return;
      Asda2Item mainWeapon = client.ActiveCharacter.MainWeapon as Asda2Item;
      if(mainWeapon == null)
      {
        client.ActiveCharacter.IsDigging = false;
        --client.ActiveCharacter.Stunned;
        return;
      }
      Asda2Item asda2Item = client.ActiveCharacter.Asda2Inventory.Equipment[10];
      bool flag = asda2Item != null && asda2Item.Category == Asda2ItemCategory.DigOil;
      if(flag)
        --asda2Item.Amount;
      if(flag)
      {
        AchievementProgressRecord progressRecord1 =
          client.ActiveCharacter.Achievements.GetOrCreateProgressRecord(92U);
        AchievementProgressRecord progressRecord2 =
          client.ActiveCharacter.Achievements.GetOrCreateProgressRecord(91U);
        ++progressRecord1.Counter;
        if(progressRecord1.Counter >= 1000U || progressRecord2.Counter >= 1000U)
          client.ActiveCharacter.GetTitle(Asda2TitleId.Automatic225);
        progressRecord1.SaveAndFlush();
      }

      if(Utility.Random(0, 100000) > CharacterFormulas.CalculateDiggingChance(mainWeapon.Template.ValueOnUse,
           client.ActiveCharacter.SoulmateRecord == null
             ? (byte) 0
             : client.ActiveCharacter.SoulmateRecord.FriendShipPoints, client.ActiveCharacter.Asda2Luck))
      {
        MineTableRecord mineTableRecord =
          GetDiggingTemplate(client.ActiveCharacter.MapId, flag);
        if(mineTableRecord == null)
        {
          Asda2DiggingHandler.SendDigEndedResponse(client, false, asda2Item);
          client.ActiveCharacter.IsDigging = false;
          --client.ActiveCharacter.Stunned;
          return;
        }

        Asda2DiggingHandler.SendDigEndedResponse(client, true, asda2Item);
        int randomItem = mineTableRecord.GetRandomItem();
        Asda2NPCLoot asda2NpcLoot = new Asda2NPCLoot();
        Asda2ItemTemplate templ = Asda2ItemMgr.GetTemplate(randomItem) ?? Asda2ItemMgr.GetTemplate(20622);
        asda2NpcLoot.Items = new Asda2LootItem[1]
        {
          new Asda2LootItem(templ, 1, 0U)
          {
            Loot = asda2NpcLoot
          }
        };
        asda2NpcLoot.Lootable = client.ActiveCharacter;
        asda2NpcLoot.Looters.Add(new Asda2LooterEntry(client.ActiveCharacter));
        asda2NpcLoot.MonstrId = 22222;
        if((int) templ.ItemId >= 33542 && 33601 <= (int) templ.ItemId)
        {
          AchievementProgressRecord progressRecord =
            client.ActiveCharacter.Achievements.GetOrCreateProgressRecord(125U);
          switch(++progressRecord.Counter)
          {
            case 250:
              client.ActiveCharacter.DiscoverTitle(Asda2TitleId.Astrological292);
              break;
            case 500:
              client.ActiveCharacter.GetTitle(Asda2TitleId.Astrological292);
              break;
          }

          progressRecord.SaveAndFlush();
        }

        if(templ.ItemId == Asda2ItemId.TreasureBox31407 || templ.ItemId == Asda2ItemId.GoldenTreasureBox31408)
        {
          AchievementProgressRecord progressRecord1 =
            client.ActiveCharacter.Achievements.GetOrCreateProgressRecord(126U);
          switch(++progressRecord1.Counter)
          {
            case 25:
              client.ActiveCharacter.DiscoverTitle(Asda2TitleId.Treasure293);
              break;
            case 50:
              client.ActiveCharacter.GetTitle(Asda2TitleId.Treasure293);
              break;
          }

          progressRecord1.SaveAndFlush();
          if(templ.ItemId == Asda2ItemId.GoldenTreasureBox31408)
          {
            AchievementProgressRecord progressRecord2 =
              client.ActiveCharacter.Achievements.GetOrCreateProgressRecord((uint) sbyte.MaxValue);
            switch(++progressRecord2.Counter)
            {
              case 389:
                client.ActiveCharacter.DiscoverTitle(Asda2TitleId.Lucky295);
                break;
              case 777:
                client.ActiveCharacter.GetTitle(Asda2TitleId.Lucky295);
                break;
            }

            progressRecord2.SaveAndFlush();
          }
        }

        client.ActiveCharacter.Map.SpawnLoot(asda2NpcLoot);
        client.ActiveCharacter.GainXp(
          CharacterFormulas.CalcDiggingExp(client.ActiveCharacter.Level, mineTableRecord.MinLevel), "digging",
          false);
        client.ActiveCharacter.GuildPoints += CharacterFormulas.DiggingGuildPoints;
      }
      else
        Asda2DiggingHandler.SendDigEndedResponse(client, false, asda2Item);

      client.ActiveCharacter.IsDigging = false;
      --client.ActiveCharacter.Stunned;
    }

    [Initialization(InitializationPass.Tenth, "Digging system.")]
    public static void Init()
    {
      ContentMgr.Load<MineTableRecord>();
    }

    private class SafeZoneDefinition
    {
      public readonly string DirectoryName;
      public readonly string FileName;
      public readonly int SeedX;
      public readonly int SeedY;

      public SafeZoneDefinition(string directoryName, string fileName, int seedX, int seedY)
      {
        DirectoryName = directoryName;
        FileName = fileName;
        SeedX = seedX;
        SeedY = seedY;
      }
    }

    private class SafeZoneMask
    {
      private readonly int m_width;
      private readonly int m_height;
      private readonly bool[] m_safeTiles;

      public SafeZoneMask(int width, int height, bool[] safeTiles)
      {
        m_width = width;
        m_height = height;
        m_safeTiles = safeTiles;
      }

      public bool Contains(float x, float y)
      {
        if(float.IsNaN(x) || float.IsNaN(y) || float.IsInfinity(x) || float.IsInfinity(y))
          return false;

        int tileX = (int) Math.Floor(x);
        int tileY = (int) Math.Floor(y);
        if(tileX < 0 || tileY < 0 || tileX >= m_width || tileY >= m_height)
          return false;

        return m_safeTiles[tileY * m_width + tileX];
      }
    }
  }
}
