using System;
using System.Linq;
using System.Threading;
using WCell.Constants;
using WCell.Core.Network;
using WCell.RealmServer.Entities;
using WCell.RealmServer.Handlers;
using WCell.RealmServer.Network;

namespace WCell.RealmServer.Mounts
{
  internal class Asda2MountHandler
  {
    private const int MountCooldownSeconds = 30;
    private const int MountSummonCastTimeMillis = 3000;

    private static void SendMountPacketToArea(Character chr, RealmServerOpCode opCode, Action<RealmPacketOut> writePacket)
    {
      if(chr == null)
        return;
      var map = chr.Map;
      if(map != null && Thread.CurrentThread.ManagedThreadId == map.CurrentThreadId && chr.IsAreaActive)
      {
        using(RealmPacketOut packet = new RealmPacketOut(opCode))
        {
          writePacket(packet);
          chr.SendPacketToArea(packet, true, true, Locale.Any, new float?());
        }

        return;
      }

      if(chr.Client != null && !chr.Client.IsOffline && chr.Client.ActiveCharacter == chr)
      {
        using(RealmPacketOut packet = new RealmPacketOut(opCode))
        {
          writePacket(packet);
          chr.Send(packet, true);
        }
      }

      if(map == null)
        return;
      map.AddMessage(() =>
      {
        if(chr.Map != map || !chr.IsAreaActive)
          return;
        using(RealmPacketOut packet = new RealmPacketOut(opCode))
        {
          writePacket(packet);
          chr.SendPacketToArea(packet, false, true, Locale.Any, new float?());
        }
      });
    }

    internal static TimeSpan GetMountCooldownRemaining(Character chr)
    {
      if(chr == null)
        return TimeSpan.Zero;
      TimeSpan remaining = chr.LastTransportUsedTime.AddSeconds(MountCooldownSeconds) - DateTime.Now;
      return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    [PacketHandler(RealmServerOpCode.RegisterVeiche)]
    public static void RegisterVeicheRequest(IRealmClient client, RealmPacketIn packet)
    {
      packet.ReadInt32();
      int num = packet.ReadByte();
      short slotInq = packet.ReadInt16();
      Asda2Item shopShopItem = client.ActiveCharacter.Asda2Inventory.GetShopShopItem(slotInq);
      if(shopShopItem == null)
      {
        SendVeicheRegisteredResponse(client.ActiveCharacter, null,
          RegisterMountStatus.Fail, -1);
        client.ActiveCharacter.SendInfoMsg("Mount item not fount. Restart client please.");
      }
      else
      {
        MountTemplate mount = null;
        if(Asda2MountMgr.TemplatesByItemIDs.ContainsKey(shopShopItem.ItemId))
          mount = Asda2MountMgr.TemplatesByItemIDs[shopShopItem.ItemId];
        if(mount == null)
        {
          SendVeicheRegisteredResponse(client.ActiveCharacter, null,
            RegisterMountStatus.Fail, -1);
          client.ActiveCharacter.SendInfoMsg("Selected item is not mount.");
        }
        else if(client.ActiveCharacter.OwnedMounts.ContainsKey(mount.Id))
        {
          SendVeicheRegisteredResponse(client.ActiveCharacter, null,
            RegisterMountStatus.Fail, -1);
          client.ActiveCharacter.SendInfoMsg("Selected mount already registered.");
        }
        else if(client.ActiveCharacter.MountBoxSize <= client.ActiveCharacter.OwnedMounts.Count)
        {
          SendVeicheRegisteredResponse(client.ActiveCharacter, null,
            RegisterMountStatus.Fail, -1);
          client.ActiveCharacter.SendInfoMsg("Not enoght space in mount inventory.");
        }
        else
        {
          if(client.ActiveCharacter.OwnedMounts.ContainsKey(mount.Id))
            return;
          Asda2MountRecord asda2MountRecord = new Asda2MountRecord(mount, client.ActiveCharacter);
          client.ActiveCharacter.OwnedMounts.Add(mount.Id, asda2MountRecord);
          asda2MountRecord.Create();
          shopShopItem.Amount = 0;
          SendVeicheRegisteredResponse(client.ActiveCharacter, shopShopItem,
            RegisterMountStatus.Ok, mount.Id);
        }
      }
    }

    public static void SendVeicheRegisteredResponse(Character chr, Asda2Item veicheItem,
      RegisterMountStatus status, int veicheId = -1)
    {
      using(RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.VeicheRegistered))
      {
        packet.WriteByte((byte) status);
        packet.WriteInt32(chr.AccId);
        Asda2InventoryHandler.WriteItemInfoToPacket(packet, veicheItem, false);
        packet.WriteInt16(chr.Asda2Inventory.Weight);
        packet.WriteInt32(veicheId);
        chr.Send(packet, false);
      }
    }

    [PacketHandler(RealmServerOpCode.SummonMount)]
    public static void SummonMountRequest(IRealmClient client, RealmPacketIn packet)
    {
      if(packet.ReadBoolean())
      {
        if(GetMountCooldownRemaining(client.ActiveCharacter) > TimeSpan.Zero)
        {
          SendCharacterOnMountStatusChangedResponse(client.ActiveCharacter, UseMountStatus.Fail);
        }
        else
        {
          SendVeicheStatusChangedResponse(client.ActiveCharacter,
            MountStatusChanged.Summonig);
          SendMountSummoningResponse(client.ActiveCharacter);
        }
      }
      else
        client.ActiveCharacter.MountId = -1;
    }

    public static void SendMountSummoningResponse(Character chr)
    {
      SendMountPacketToArea(chr, RealmServerOpCode.MountSummoning, packet =>
      {
        packet.WriteByte(1);
        packet.WriteInt32(chr.AccId);
        packet.WriteInt16(MountSummonCastTimeMillis);
      });
    }

    [PacketHandler(RealmServerOpCode.GetOnMount)]
    public static void GetOnMountRequest(IRealmClient client, RealmPacketIn packet)
    {
      int key = packet.ReadInt32();
      if(client.ActiveCharacter.OwnedMounts.ContainsKey(key))
      {
        client.ActiveCharacter.MountId = key;
      }
      else
      {
        SendCharacterOnMountStatusChangedResponse(client.ActiveCharacter,
          UseMountStatus.Fail);
        client.ActiveCharacter.YouAreFuckingCheater("Trying to use not owned Mount.", 30);
      }
    }

    public static void SendCharacterOnMountStatusChangedResponse(Character chr,
      UseMountStatus status)
    {
      SendMountPacketToArea(chr, RealmServerOpCode.CHaracterOnMountStatusChanged, packet =>
      {
        packet.WriteByte((byte) status);
        packet.WriteByte(chr.IsOnMount);
        packet.WriteInt32(chr.AccId);
        packet.WriteInt32(chr.MountId);
      });
    }

    public static void SendCharacterOnMountStatusChangedToPneClientResponse(IRealmClient reciver, Character trigger)
    {
      using(RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.CHaracterOnMountStatusChanged))
      {
        packet.WriteByte((byte) 1);
        packet.WriteByte(trigger.IsOnMount);
        packet.WriteInt32(trigger.AccId);
        packet.WriteInt32(trigger.MountId);
        reciver.Send(packet, true);
      }
    }

    public static void SendVeicheStatusChangedResponse(Character chr, MountStatusChanged status)
    {
      SendMountPacketToArea(chr, RealmServerOpCode.VeicheStatusChanged, packet =>
      {
        packet.WriteByte((byte) status);
        packet.WriteInt32(chr.AccId);
        packet.WriteInt32(chr.MountId);
      });
    }

    public static void SendMountBoxSizeInitResponse(IRealmClient client)
    {
      using(RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.MountBoxSizeInit))
      {
        packet.WriteInt32(client.ActiveCharacter.AccId);
        packet.WriteByte(client.ActiveCharacter.MountBoxSize);
        client.Send(packet, true);
      }
    }

    public static void SendOwnedMountsListResponse(IRealmClient client)
    {
      using(RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.OwnedMountsList))
      {
        packet.WriteInt32(client.ActiveCharacter.AccId);
        int[] array = client.ActiveCharacter.OwnedMounts.Keys.ToArray();
        for(int index = 0; index < 90; ++index)
          packet.WriteInt32(array.Length > index ? array[index] : -1);
        client.Send(packet, true);
      }
    }

    internal enum MountStatusChanged
    {
      Unsumon,
      Summoned,
      Summonig
    }

    internal enum UseMountStatus
    {
      Fail,
      Ok
    }

    internal enum RegisterMountStatus
    {
      Fail,
      Ok
    }
  }
}
