using Cell.Core;
using System;
using System.IO;
using WCell.Constants;
using WCell.Constants.NPCs;
using WCell.Core.Network;
using WCell.RealmServer.Asda2BattleGround;
using WCell.RealmServer.Entities;
using WCell.RealmServer.Logs;
using WCell.RealmServer.Network;
using WCell.Util.Graphics;
using WCell.Util.Variables;

namespace WCell.RealmServer.Handlers
{
  internal class Asda2MovmentHandler
  {
    private static readonly byte[] Stab8 = new byte[2]
    {
      1,
      0
    };

    private static readonly byte[] Stab35 = new byte[8]
    {
      2,
      0,
      byte.MaxValue,
      byte.MaxValue,
      0,
      0,
      0,
      0
    };

    [NotVariable]private const int DefaultMoveUpdateTime = 3000;

    public static void SendMonstMoveOrAtackResponse(short sessIdTarget, NPC movingNpc, int dmg, Vector3 toPos,
      bool isAtack)
    {
      if(movingNpc == null || !movingNpc.IsAreaActive)
        return;
      if(isAtack)
        movingNpc.RefreshCurrentPosition();
      using(RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.MonstMove))
      {
        packet.WriteInt16(sessIdTarget);
        packet.WriteInt16((short) movingNpc.Entry.NPCId);
        packet.WriteInt16(movingNpc.UniqIdOnMap);
        packet.WriteByte(isAtack ? 3 : (movingNpc.Movement.MoveType == AIMoveType.Walk ? 2 : 5));
        packet.WriteInt16((short) movingNpc.Asda2X);
        packet.WriteInt16((short) movingNpc.Asda2Y);
        packet.WriteInt16(isAtack ? 0 : (short) toPos.X);
        packet.WriteInt16(isAtack ? 0 : (short) toPos.Y);
        float num = movingNpc.Movement.MoveType == AIMoveType.Walk ? movingNpc.WalkSpeed : movingNpc.RunSpeed;
        packet.WriteInt16(isAtack ? 0 : (short) (1000.0 / num));
        packet.WriteInt16(isAtack ? 0 : 10000);
        packet.WriteInt16(10000);
        packet.WriteInt32(dmg);
        packet.WriteInt16(movingNpc.Health);
        packet.WriteInt16(movingNpc.Health <= 0 ? -1 : 0);
        packet.WriteSkip(Stab35);
        movingNpc.SendPacketToArea(packet, false, true, Locale.Any, new float?(), movingNpc.Channel);
      }
    }

    [PacketHandler(RealmServerOpCode.EndMove)]
    public static void EndMoveRequest(IRealmClient client, RealmPacketIn packet)
    {
      packet.Position -= 3;
      float x = packet.ReadFloat();
      float y = packet.ReadFloat();
      float x1 = packet.ReadFloat();
      float y1 = packet.ReadFloat();
      packet.Position += 4;
      short target = packet.ReadInt16();
      UpdateCharacterPositionFromEndMove(client.ActiveCharacter, x, y, x1, y1);
      client.ActiveCharacter.IsMoving = false;
      client.ActiveCharacter.LastMoveEnded = DateTime.Now;

      var dist = (client.ActiveCharacter.SpeedFactor * 3.89) * (client.ActiveCharacter.TimeToMove);
      
      SendEndMoveCommonResponse(client, x, y, x1, y1, target);
    }

    private static void UpdateCharacterPositionFromEndMove(Character chr, float x, float y, float x1, float y1)
    {
      if(chr == null || chr.Map == null)
        return;

      float finalX = NormalizeClientCoordinate(x1);
      float finalY = NormalizeClientCoordinate(y1);
      if(!IsValidClientCoordinate(finalX) || !IsValidClientCoordinate(finalY))
      {
        finalX = NormalizeClientCoordinate(x);
        finalY = NormalizeClientCoordinate(y);
      }

      if(!IsValidClientCoordinate(finalX) || !IsValidClientCoordinate(finalY))
        return;

      SetCharacterOrientationTowardsLocal(chr, finalX, finalY);
      chr.SetPosition(new Vector3(finalX + chr.Map.Offset, finalY + chr.Map.Offset));
      chr.LastNewPosition = new Vector3(finalX, finalY, 0.0f);
      chr.CurrentMovingVector = Vector2.Zero;
    }

    private static float NormalizeClientCoordinate(float coordinate)
    {
      if(float.IsNaN(coordinate) || float.IsInfinity(coordinate))
        return coordinate;
      return Math.Abs(coordinate) > 1000f ? coordinate / 100f : coordinate;
    }

    private static bool IsValidClientCoordinate(float coordinate)
    {
      return !float.IsNaN(coordinate) && !float.IsInfinity(coordinate) &&
             coordinate >= 0.0f && coordinate <= 512.0f;
    }

    public static void SendEndMoveCommonResponse(IRealmClient client, float x, float y, float x1, float y1,
      short target)
    {
      using(RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.EndMoveCommon))
      {
        packet.WriteByte(0);
        packet.WriteInt16(client.ActiveCharacter.SessionId);
        packet.WriteInt32(client.ActiveCharacter.AccId);
        packet.WriteInt16(0);
        packet.WriteFloat(x);
        packet.WriteFloat(y);
        packet.WriteFloat(x1);
        packet.WriteFloat(y1);
        packet.WriteInt32(0);
        packet.WriteInt16(target);
        client.ActiveCharacter.SendPacketToArea(
          packet,
          true,
          false,
          Locale.Any,
          new float?(),
          client.Channel);
      }
    }

    public static void SendEndMoveByFastInstantRegularMoveResponse(Character chr)
    {
      using(RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.StartMoveCommon))
      {
        packet.WriteByte(1);
        packet.WriteInt16(chr.SessionId);
        packet.WriteInt32(chr.Account.AccountId);
        packet.WriteInt16(2);
        packet.WriteFloat(chr.Asda2X * 100f);
        packet.WriteFloat(chr.Asda2Y * 100f);
        packet.WriteFloat(chr.Asda2X * 100f);
        packet.WriteFloat(chr.Asda2Y * 100f);
        packet.WriteFloat(5);
        packet.WriteInt16(-1);
        chr.SendPacketToArea(packet, true, true, Locale.Any, new float?(), chr.Client.Channel);
      }
    }

    [PacketHandler(RealmServerOpCode.StartMove)]
    public static void StartMoveRequest(IRealmClient client, RealmPacketIn packet)
    {

      if(!client.ActiveCharacter.CanMove)
      {
        SendStartComonMovePacketError(client.ActiveCharacter, false,
          Asda2StartMovementStatus.CantMoveInThisCondition);
      }
      else
      {
        if(client.ActiveCharacter.IsAggred)
        {
          if(client.ActiveCharacter.ArggredDateTime < DateTime.Now)
          {
            client.ActiveCharacter.IsAggred = false;
            client.ActiveCharacter.UpdateSpeedFactor();
          }
          else
          {
            client.ActiveCharacter.SendInfoMsg("You are aggred.");
            SendStartComonMovePacketError(client.ActiveCharacter, false,
              Asda2StartMovementStatus.CantMoveInThisCondition);
            return;
          }
        }

        packet.Position -= 24;
        float x;
        float y;
        short num1;
        try
        {
          packet.Position += 16;
          packet.Position += 5;
          double num2 = packet.ReadSingle() / 100.0;
          double num3 = packet.ReadSingle() / 100.0;
          x = packet.ReadSingle() / 100f;
          y = packet.ReadSingle() / 100f;
          packet.Position += 4;
          num1 = packet.ReadInt16();
          client.ActiveCharacter.LastMoveStarted = DateTime.Now;
          client.ActiveCharacter.MovedDistance = (float)(new Vector3(x, y, 0)).GetDistance(client.ActiveCharacter.Asda2Position);
        }
        catch(EndOfStreamException)
        {
          return;
        }

        Character activeCharacter = client.ActiveCharacter;
        if(activeCharacter.IsAsda2BattlegroundInProgress &&
           (!activeCharacter.CurrentBattleGround.IsStarted &&
            new Vector3(activeCharacter.Map.Offset + x, activeCharacter.Map.Offset + y, 0.0f)
              .GetDistance(activeCharacter.CurrentBattleGround.GetBasePosition(activeCharacter)) > 40.0 ||
            new Vector3(activeCharacter.Map.Offset + x, activeCharacter.Map.Offset + y, 0.0f)
              .GetDistance(activeCharacter.CurrentBattleGround.GetForeigLocation(activeCharacter)) < 40.0))
        {
          SendStartComonMovePacketError(client.ActiveCharacter, false,
            Asda2StartMovementStatus.YouCantMoveToOtherSideOfRevivalArea);
        }
        else
        {
          if(activeCharacter.IsFirstMoveAfterAtack)
            activeCharacter.IsFirstMoveAfterAtack = false;
          if(activeCharacter.Target is NPC && activeCharacter.Target.IsDead)
          {
            activeCharacter.Target = null;
            activeCharacter.IsFighting = false;
          }

          if(num1 == -1)
            activeCharacter.Target = null;
          else if(activeCharacter.Target == null || !(activeCharacter.Target is Character))
            activeCharacter.Target = activeCharacter.Map.GetNpcByUniqMapId((ushort) num1, client.Channel);
          if(activeCharacter.Target == null || !(activeCharacter.Target is NPC))
            activeCharacter.IsFighting = false;
          activeCharacter.EndMoveCount = 0;
          if(activeCharacter.IsFighting && activeCharacter.MainWeapon.IsRanged &&
             activeCharacter.Target != null &&
             new Vector3(x, y, 0.0f).GetDistance(activeCharacter.Target.Asda2Position) < 3.0)
            SendStartMoveCommonToAreaResponse(activeCharacter, false, true);
          else
            OnMoveRequest(client, y, x);

        }
      }


        
    }

    public static void OnMoveRequest(IRealmClient client, float y, float x)
    {
      Vector3 vector3 = new Vector3(x, y, 0.0f);
      Character activeCharacter = client.ActiveCharacter;
      if(activeCharacter.Asda2X == (double) x && activeCharacter.Asda2Y == (double) y)
        return;
      Unit mover = activeCharacter.MoveControl.Mover as Unit;
      if(mover == null || !mover.UnitFlags.HasFlag(UnitFlags.PlayerControlled) ||
         (mover.UnitFlags.HasFlag(UnitFlags.Influenced) || activeCharacter.IsDead))
        return;
      client.ActiveCharacter.m_lastMoveTimeT = DateTime.Now;
      if(activeCharacter.CurrentMovingVector != Vector2.Zero)
        CalculateAndSetRealPos(activeCharacter, 0);
      mover.CancelEmote();
      SetCharacterOrientationTowardsLocal(activeCharacter, x, y);
      activeCharacter.LastNewPosition = vector3;
      activeCharacter.CurrentMovingVector = vector3.XY - activeCharacter.Asda2Position.XY;
      activeCharacter.IsMoving = true;
      SendStartMoveCommonToAreaResponse(activeCharacter, false, true);
    }

    public static void MoveToSelectedTargetAndAttack(Character chr)
    {
      if(chr.Target == null)
        return;
      Vector2 xy = chr.Target.Asda2Position.XY;
      Vector3 vector3 = new Vector3(xy.X, xy.Y, 0.0f);
      SetCharacterOrientationTowardsLocal(chr, vector3.X, vector3.Y);
      chr.LastNewPosition = vector3;
      chr.CurrentMovingVector = vector3.XY - chr.Asda2Position.XY;
      chr.IsMoving = true;
      SendStartMoveCommonToAreaResponse(chr, false, true);
    }

    private static void SetCharacterOrientationTowardsLocal(Character chr, float x, float y)
    {
      if(chr == null || chr.Map == null)
        return;
      if(!IsValidClientCoordinate(x) || !IsValidClientCoordinate(y))
        return;
      if(Math.Abs(chr.Asda2X - x) < 0.001f && Math.Abs(chr.Asda2Y - y) < 0.001f)
        return;
      Vector3 target = new Vector3(x + chr.Map.Offset, y + chr.Map.Offset, chr.Position.Z);
      chr.SetOrientationTowards(ref target);
    }

    public static void CalculateAndSetRealPos(Character chr, int dt)
    {
      if(!chr.IsMoving)
      {
        chr.CurrentMovingVector = Vector2.Zero;
      }
      else
      {
        SetPosition(chr);
        chr.TimeFromLastPositionUpdate += dt;
        if(chr.TimeFromLastPositionUpdate <= 3000)
          return;
        chr.TimeFromLastPositionUpdate = 0;
        NotifyMoving(chr);
      }
    }

    private static void SetPosition(Character chr)
    {
      float num1 = chr.CurrentMovingVector.Length();
      int num2 = Environment.TickCount - chr.LastMoveTime;
      float distance = (float) (chr.RunSpeed * (double) num2 / 100.0);
      chr.OnMove();
      if(distance >= (double) num1)
      {
        chr.SetPosition(new Vector3(chr.LastNewPosition.X + chr.Map.Offset,
          chr.LastNewPosition.Y + chr.Map.Offset));
        chr.CurrentMovingVector = new Vector2(0.0f, 0.0f);
        chr.IsMoving = false;
      }
      else
      {
        Vector3 vector3 =
          new Vector3(
            FindRealPosition(chr.CurrentMovingVector, distance, chr.Asda2Position.XY),
            0.0f);
        chr.SetPosition(new Vector3(vector3.X + chr.Map.Offset, vector3.Y + chr.Map.Offset));
        chr.CurrentMovingVector = chr.LastNewPosition.XY - chr.Asda2Position.XY;
      }
    }

    private static void NotifyMoving(Character chr)
    {
      chr.Map.CallDelayed(3000, () =>
      {
        if(chr.IsInGroup)
          Asda2GroupHandler.SendPartyMemberPositionInfoResponse(chr);
        if(chr.IsSoulmated)
          Asda2SoulmateHandler.SendSoulmatePositionResponse(chr.Client);
        if(!chr.IsAsda2BattlegroundInProgress)
          return;
        Asda2BattlegroundHandler.SendCharacterPositionInfoOnWarResponse(chr);
      });
    }

    private static Vector2 FindRealPosition(Vector2 movingVector, float distance, Vector2 startPosition)
    {
      double num1 = Math.Acos(movingVector.X / movingVector.Length());
      double num2 = distance * Math.Cos(num1);
      double num3 = distance * Math.Sin(num1);
      return new Vector2(Convert.ToSingle(startPosition.X + num2),
        Convert.ToSingle(startPosition.Y + ((double) movingVector.Y < 0.0 ? -num3 : num3)));
    }

    public static void SendStartMoveCommonToAreaResponse(Character chr, bool instant, bool includeSelf = true)
    {
      using(RealmPacketOut startComonMovePacket = CreateStartComonMovePacket(chr, instant))
        chr.SendPacketToArea(startComonMovePacket, includeSelf, true, Locale.Any, new float?());
    }

    public static void SendStartMoveCommonToAreaResponse(Character chr, bool instant, bool includeSelf, int targetId)
    {
      using(RealmPacketOut startComonMovePacket = CreateStartComonMovePacket(chr, instant, targetId))
        chr.SendPacketToArea(startComonMovePacket, includeSelf, true, Locale.Any, new float?());
    }

    public static void SendStartMoveCommonToAreaResponse(Character chr, bool includeSelf, int targetId,
      Vector3 from, Vector3 to, float speed)
    {
      using(RealmPacketOut startComonMovePacket = CreateStartComonMovePacket(chr, from, to, speed, targetId))
        chr.SendPacketToArea(startComonMovePacket, includeSelf, true, Locale.Any, new float?());
    }

    public static void SendStartMoveCommonToOneClienResponset(Character movingChr, IRealmClient recievingClient,
      bool instant)
    {
      using(RealmPacketOut startComonMovePacket =
        CreateStartComonMovePacket(movingChr, instant))
        recievingClient.Send(startComonMovePacket, true);
    }

    public static RealmPacketOut CreateStartComonMovePacket(Character chr, bool instant)
    {
      NPC target = chr.Target as NPC;
      return CreateStartComonMovePacket(chr, instant, target == null ? -1 : target.UniqIdOnMap);
    }

    private static RealmPacketOut CreateStartComonMovePacket(Character chr, bool instant, int targetId)
    {
      RealmPacketOut realmPacketOut = new RealmPacketOut(RealmServerOpCode.StartMoveCommon);
      realmPacketOut.WriteByte(1);
      realmPacketOut.WriteInt16(chr.SessionId);
      realmPacketOut.WriteInt32(chr.Account.AccountId);
      realmPacketOut.WriteInt16(2);
      realmPacketOut.WriteFloat(chr.Asda2X * 100f);
      realmPacketOut.WriteFloat(chr.Asda2Y * 100f);
      realmPacketOut.WriteFloat(instant ? chr.Asda2X * 100f : chr.LastNewPosition.X * 100f);
      realmPacketOut.WriteFloat(instant ? chr.Asda2Y * 100f : chr.LastNewPosition.Y * 100f);
      realmPacketOut.WriteFloat(instant ? 5f : chr.RunSpeed);
      realmPacketOut.WriteInt16(targetId);
      return realmPacketOut;
    }

    private static RealmPacketOut CreateStartComonMovePacket(Character chr, Vector3 from, Vector3 to,
      float speed, int targetId)
    {
      RealmPacketOut realmPacketOut = new RealmPacketOut(RealmServerOpCode.StartMoveCommon);
      realmPacketOut.WriteByte(1);
      realmPacketOut.WriteInt16(chr.SessionId);
      realmPacketOut.WriteInt32(chr.Account.AccountId);
      realmPacketOut.WriteInt16(2);
      realmPacketOut.WriteFloat(from.X * 100f);
      realmPacketOut.WriteFloat(from.Y * 100f);
      realmPacketOut.WriteFloat(to.X * 100f);
      realmPacketOut.WriteFloat(to.Y * 100f);
      realmPacketOut.WriteFloat(speed);
      realmPacketOut.WriteInt16(targetId);
      return realmPacketOut;
    }

    public static void SendStartComonMovePacketError(Character chr, bool instant, Asda2StartMovementStatus status)
    {
      using(RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.StartMoveCommon))
      {
        packet.WriteByte((byte) status);
        packet.WriteInt16(chr.SessionId);
        packet.WriteInt32(chr.Account.AccountId);
        packet.WriteInt16(2);
        packet.WriteFloat(chr.Asda2X * 100f);
        packet.WriteFloat(chr.Asda2Y * 100f);
        packet.WriteFloat(instant ? chr.Asda2X * 100f : chr.LastNewPosition.X * 100f);
        packet.WriteFloat(instant ? chr.Asda2Y * 100f : chr.LastNewPosition.Y * 100f);
        packet.WriteFloat(instant ? 5f : chr.RunSpeed);
        NPC target = chr.Target as NPC;
        packet.WriteInt16(target == null ? 0 : target.UniqIdOnMap);
        chr.Send(packet, true);
      }
    }
  }
}
