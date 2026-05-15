using System;
using WCell.Constants;
using WCell.Constants.Achievements;
using WCell.Constants.Items;
using WCell.Constants.World;
using WCell.Core.Network;
using WCell.RealmServer.Achievements;
using WCell.RealmServer.Entities;
using WCell.RealmServer.Global;
using WCell.RealmServer.Network;
using WCell.Util.Graphics;

namespace WCell.RealmServer.Handlers
{
  public static class Asda2DiggingHandler
  {
    [PacketHandler(RealmServerOpCode.StartDig)]
    public static void StartDigRequest(IRealmClient client, RealmPacketIn packet)
    {
      Character character = client.ActiveCharacter;
      if(character == null)
        return;
      if(character.IsDigging)
      {
        SendStartDigResponseResponse(client, Asda2DigResult.Ok);
      }
      else if(Asda2DigMgr.IsInTown(character))
      {
        SendStartDigResponseResponse(client, Asda2DigResult.YouCantDoItInTown);
      }
      else
      {
        Asda2Item mainWeapon = character.MainWeapon as Asda2Item;
        if(mainWeapon == null || mainWeapon.Category != Asda2ItemCategory.Showel)
        {
          SendStartDigResponseResponse(client, Asda2DigResult.YouHaveNoShowel);
        }
        else
        {
          Asda2Item asda2Item = character.Asda2Inventory.Equipment[10];
          bool flag = asda2Item != null && asda2Item.Category == Asda2ItemCategory.DigOil;
          MineTableRecord mineTableRecord = Asda2DigMgr.GetDiggingTemplate(character.MapId, flag);
          if(mineTableRecord == null)
          {
            character.YouAreFuckingCheater(
              "Trying to dig in unknown location : " + character.MapId, 10);
            SendStartDigResponseResponse(client, Asda2DigResult.DiggingFail);
          }
          else if(mineTableRecord.MinLevel > character.Level)
          {
            SendStartDigResponseResponse(client,
              Asda2DigResult.YouUnableToDigInThisLocationDueLowLevel);
          }
          else
          {
            character.CancelMovement();
            character.IsMoving = false;
            character.IsFighting = false;
            character.CurrentMovingVector = Vector2.Zero;
            character.LastMoveEnded = DateTime.Now;
            character.CancelAllActions();
            character.IsDigging = true;
            ++character.Stunned;
            character.Map.CallDelayed(6000, () => Asda2DigMgr.ProcessDig(client));
            Asda2CharacterHandler.SendEmoteResponse(character, 110, 0, 0.0f,
              0.0f);
            SendStartDigResponseResponse(client, Asda2DigResult.Ok);
          }
        }
      }
    }

    public static void SendStartDigResponseResponse(IRealmClient client, Asda2DigResult status)
    {
      using(RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.StartDigResponse))
      {
        packet.WriteByte((byte) status);
        client.Send(packet, false);
      }
    }

    public static void SendDigEndedResponse(IRealmClient client, bool success, Asda2Item item = null)
    {
      if(client == null || client.ActiveCharacter == null)
        return;
      AchievementProgressRecord progressRecord1 =
        client.ActiveCharacter.Achievements.GetOrCreateProgressRecord(116U);
      switch(++progressRecord1.Counter)
      {
        case 50:
          client.ActiveCharacter.DiscoverTitle(Asda2TitleId.Shovel285);
          break;
        case 100:
          client.ActiveCharacter.GetTitle(Asda2TitleId.Shovel285);
          break;
        case 2500:
          client.ActiveCharacter.DiscoverTitle(Asda2TitleId.Excavator286);
          break;
        case 5000:
          client.ActiveCharacter.GetTitle(Asda2TitleId.Excavator286);
          break;
      }

      progressRecord1.SaveAndFlush();
      Map nonInstancedMap1 = World.GetNonInstancedMap(MapId.Alpia);
      Map nonInstancedMap2 = World.GetNonInstancedMap(MapId.Silaris);
      Map nonInstancedMap3 = World.GetNonInstancedMap(MapId.SunnyCoast);
      Map nonInstancedMap4 = World.GetNonInstancedMap(MapId.Flabis);
      Vector3 point1 = new Vector3(131f + nonInstancedMap1.Offset, 265f + nonInstancedMap1.Offset, 0.0f);
      Vector3 point2 = new Vector3(110f + nonInstancedMap2.Offset, 144f + nonInstancedMap2.Offset, 0.0f);
      Vector3 point3 = new Vector3(226f + nonInstancedMap3.Offset, 353f + nonInstancedMap3.Offset, 0.0f);
      Vector3 point4 = new Vector3(270f + nonInstancedMap4.Offset, 263f + nonInstancedMap4.Offset, 0.0f);
      if(client.ActiveCharacter.Position.GetDistance(point1) < 10.0)
      {
        AchievementProgressRecord progressRecord2 =
          client.ActiveCharacter.Achievements.GetOrCreateProgressRecord(117U);
        switch(++progressRecord2.Counter)
        {
          case 500:
            client.ActiveCharacter.DiscoverTitle(Asda2TitleId.Burial287);
            break;
          case 1000:
            client.ActiveCharacter.GetTitle(Asda2TitleId.Burial287);
            break;
        }

        progressRecord2.SaveAndFlush();
      }

      if(client.ActiveCharacter.Position.GetDistance(point2) < 50.0)
      {
        AchievementProgressRecord progressRecord2 =
          client.ActiveCharacter.Achievements.GetOrCreateProgressRecord(118U);
        switch(++progressRecord2.Counter)
        {
          case 500:
            client.ActiveCharacter.DiscoverTitle(Asda2TitleId.Archaeologist288);
            break;
          case 1000:
            client.ActiveCharacter.GetTitle(Asda2TitleId.Archaeologist288);
            break;
        }

        progressRecord2.SaveAndFlush();
      }

      if(client.ActiveCharacter.Position.GetDistance(point3) < 25.0)
      {
        AchievementProgressRecord progressRecord2 =
          client.ActiveCharacter.Achievements.GetOrCreateProgressRecord(119U);
        switch(++progressRecord2.Counter)
        {
          case 500:
            client.ActiveCharacter.DiscoverTitle(Asda2TitleId.Shipwreck289);
            break;
          case 1000:
            client.ActiveCharacter.GetTitle(Asda2TitleId.Shipwreck289);
            break;
        }

        progressRecord2.SaveAndFlush();
      }

      if(client.ActiveCharacter.Position.GetDistance(point4) < 50.0)
      {
        AchievementProgressRecord progressRecord2 =
          client.ActiveCharacter.Achievements.GetOrCreateProgressRecord(120U);
        switch(++progressRecord2.Counter)
        {
          case 500:
            client.ActiveCharacter.DiscoverTitle(Asda2TitleId.Oasis290);
            break;
          case 1000:
            client.ActiveCharacter.GetTitle(Asda2TitleId.Oasis290);
            break;
        }

        progressRecord2.SaveAndFlush();
      }

      AchievementProgressRecord progressRecord3 =
        client.ActiveCharacter.Achievements.GetOrCreateProgressRecord(121U);
      AchievementProgressRecord progressRecord4 =
        client.ActiveCharacter.Achievements.GetOrCreateProgressRecord(122U);
      AchievementProgressRecord progressRecord5 =
        client.ActiveCharacter.Achievements.GetOrCreateProgressRecord(123U);
      AchievementProgressRecord progressRecord6 =
        client.ActiveCharacter.Achievements.GetOrCreateProgressRecord(124U);
      switch(client.ActiveCharacter.MapId)
      {
        case MapId.Silaris:
          ++progressRecord4.Counter;
          progressRecord4.SaveAndFlush();
          break;
        case MapId.Alpia:
          ++progressRecord3.Counter;
          progressRecord3.SaveAndFlush();
          break;
        case MapId.Aquaton:
          ++progressRecord6.Counter;
          progressRecord6.SaveAndFlush();
          break;
        case MapId.Flamio:
          ++progressRecord5.Counter;
          progressRecord5.SaveAndFlush();
          break;
      }

      if(progressRecord3.Counter >= 1000U && progressRecord4.Counter >= 1000U &&
         (progressRecord5.Counter >= 1000U && progressRecord6.Counter >= 1000U))
        client.ActiveCharacter.GetTitle(Asda2TitleId.Explorer291);
      if(client.ActiveCharacter.isTitleGetted(Asda2TitleId.Shovel285) &&
         client.ActiveCharacter.isTitleGetted(Asda2TitleId.Excavator286) &&
         (client.ActiveCharacter.isTitleGetted(Asda2TitleId.Burial287) &&
          client.ActiveCharacter.isTitleGetted(Asda2TitleId.Archaeologist288)) &&
         (client.ActiveCharacter.isTitleGetted(Asda2TitleId.Shipwreck289) &&
          client.ActiveCharacter.isTitleGetted(Asda2TitleId.Oasis290) &&
          (client.ActiveCharacter.isTitleGetted(Asda2TitleId.Explorer291) &&
           client.ActiveCharacter.isTitleGetted(Asda2TitleId.Astrological292))) &&
         client.ActiveCharacter.isTitleGetted(Asda2TitleId.Treasure293))
        client.ActiveCharacter.GetTitle(Asda2TitleId.Geologist294);
      using(RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.DigEnded))
      {
        packet.WriteByte(success ? 1 : 0);
        packet.WriteInt16(client.ActiveCharacter.SessionId);
        Asda2InventoryHandler.WriteItemInfoToPacket(packet, item, false);
        client.ActiveCharacter.SendPacketToArea(packet, true, true, Locale.Any, new float?());
      }
    }
  }
}
