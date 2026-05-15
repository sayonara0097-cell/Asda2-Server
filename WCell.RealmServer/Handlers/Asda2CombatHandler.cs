using System;
using System.Collections.Generic;
using System.Linq;
using WCell.Constants;
using WCell.Constants.Items;
using WCell.Core.Network;
using WCell.RealmServer.Chat;
using WCell.RealmServer.Entities;
using WCell.RealmServer.Global;
using WCell.RealmServer.Items;
using WCell.RealmServer.Misc;
using WCell.RealmServer.Network;
using WCell.RealmServer.NPCs;
using WCell.RealmServer.Spells.Auras;

namespace WCell.RealmServer.Handlers
{
  internal class Asda2CombatHandler
  {
    public static int CrossbowInitialPvPImpactDelayPct = 60;
    public static int CrossbowInitialPvPMinImpactDelayMillis = 650;
    public static int CrossbowMovingInitialPvPImpactDelayMillis = 350;
    public static int BallistaInitialNpcPreStrikeImpactDelayMillis = 0;
    public static int BallistaInitialNpcDuplicateSkipMillis = 1200;
    public static int BallistaNpcInstantStrikeCount = 5;
    public static int BallistaInitialNpcAnimationDelayMillis = 120;
		public static int BallistaAttackSpeedupPct = 30;
    private const float ClosePvpAttackVisualSyncRange = 3.0f;

    private static readonly byte[] unk8 = new byte[21]
    {
      0,
      byte.MaxValue,
      byte.MaxValue,
      0,
      0,
      0,
      0,
      62,
      239,
      246,
      57,
      0,
      0,
      0,
      0,
      0,
      0,
      0,
      0,
      0,
      0
    };

    private static readonly byte[] stab6 = new byte[2]
    {
      14,
      0
    };

    private static readonly byte[] stab10 = new byte[92]
    {
      0,
      0,
      0,
      0,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      byte.MaxValue,
      0,
      0,
      0,
      0,
      0,
      0,
      0,
      0,
      0,
      0,
      0,
      0,
      0,
      0,
      0,
      0,
      0,
      0,
      0,
      0,
      0,
      0,
      0,
      0,
      0,
      0,
      0,
      0,
      0,
      0,
      0,
      0
    };

    [PacketHandler(RealmServerOpCode.StartAtack)]
    public static void StartAtackRequest(IRealmClient client, RealmPacketIn packet)
    {
      Character activeCharacter = client.ActiveCharacter;
      ushort id = packet.ReadUInt16();
      NPC npcByUniqMapId = activeCharacter.Map.GetNpcByUniqMapId(id, client.Channel);
      RefreshCombatPosition(activeCharacter);
      RefreshCombatPosition(npcByUniqMapId);
      byte status = 0;
      if(npcByUniqMapId == null)
        status = 2;
      if(npcByUniqMapId != null && activeCharacter.CanHarm(npcByUniqMapId) &&
         activeCharacter.CanSee(npcByUniqMapId))
        status = 1;
      if(activeCharacter.Asda2Inventory.WeightPrc >= 90)
        status = 2;
      if(!HasRequiredAmmo(client.ActiveCharacter))
        {
            status = 7;
        }
      if(client.ActiveCharacter.IsOnTransport)
        status = 8;
      if(UsesShovel(activeCharacter))
        status = (byte) Asda2CharacterAtackStatus.YouCannotAtackWithShovel;
      if(status == 1)
      {
        activeCharacter.Target = npcByUniqMapId;
        activeCharacter.IsWaitingForAtackAnimation = true;
        activeCharacter.PrepareClientAttackSync();
        if(UsesBallista(activeCharacter))
          activeCharacter.SetClientAttackInstantStrikesRemaining(BallistaNpcInstantStrikeCount);
      }
      else
      {
        activeCharacter.Target = null;
        activeCharacter.IsWaitingForAtackAnimation = false;
        activeCharacter.IsFighting = false;
      }
      ScheduleInitialBallistaNpcAttack(client, activeCharacter, npcByUniqMapId, status);
      SendStartAtackResponseWithInitialBallistaDelay(activeCharacter, npcByUniqMapId, status);
    }

    [PacketHandler(RealmServerOpCode.StartAtackCharacter)]
    public static void StartAtackCharacterRequest(IRealmClient client, RealmPacketIn packet)
    {
      Character attacker = client.ActiveCharacter;
      Character characterBySessionId = World.GetCharacterBySessionId(packet.ReadUInt16());
      bool useMovingCrossbowFallback = UsesCrossbow(attacker) && attacker.IsMoving;
      RefreshCombatPosition(attacker);
      RefreshCombatPosition(characterBySessionId);
      Asda2CharacterAtackStatus status;
      if(TryGetCharacterAttackStartError(attacker, characterBySessionId, out status))
      {
        StopClientAttack(attacker);
        if(characterBySessionId != null)
          SendStartAtackCharacterError(attacker, characterBySessionId, status);
        return;
      }

      attacker.Target = characterBySessionId;
      attacker.IsWaitingForAtackAnimation = true;
      attacker.PrepareClientAttackSync();
      SendStartAtackCharacterResponseResponse(attacker, characterBySessionId);
      Asda2SpellHandler.SendSetAtackStateGuiResponse(attacker);
      ScheduleInitialCrossbowCharacterAttack(client, attacker, characterBySessionId,
        useMovingCrossbowFallback);
    }

    public static void SendStartAtackCharacterResponseResponse(Character atacker, Character victim)
    {
      using(RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.StartAtackCharacterResponse))
      {
        packet.WriteByte(1);
        packet.WriteInt16(atacker.SessionId);
        packet.WriteInt16(victim.SessionId);
        packet.WriteFloat(victim.Asda2X);
        packet.WriteFloat(victim.Asda2Y);
        SendStartAttackAnimationToAttackerAndObservers(packet, atacker, victim);
      }
    }

    private static void SendStartAttackAnimationToAttackerAndObservers(RealmPacketOut packet,
      Character attacker, Character victim)
    {
      if(attacker == null)
        return;

      SyncPvpAttackPositionToVictim(attacker, victim);

      if(attacker.Client != null)
        attacker.Send(packet, true);

      if(attacker.Map == null || !attacker.IsAreaActive)
        return;

      byte? channel = attacker.Client == null ? new byte?() : attacker.Client.Channel;
      foreach(Character observer in attacker.GetNearbyCharacters(WorldObject.BroadcastRange, false))
      {
        if(observer == null || observer.Client == null)
          continue;
        if(channel.HasValue && observer.Client.Channel != channel.Value)
          continue;
        observer.Send(packet, true);
      }
    }

    private static void SyncPvpAttackPositionToVictim(Character attacker, Character victim)
    {
      if(attacker == null || victim == null || victim.Client == null)
        return;

      RefreshCombatPosition(attacker);
      if(attacker.IsMoving)
      {
        Asda2MovmentHandler.SendStartMoveCommonToOneClienResponset(attacker, victim.Client,
          false);
        return;
      }

      if(IsClosePvpAttack(attacker, victim))
        return;

      Asda2MovmentHandler.SendStartMoveCommonToOneClienResponset(attacker, victim.Client,
        true);
    }

    private static bool IsClosePvpAttack(Character attacker, Character victim)
    {
      float deltaX = victim.Asda2X - attacker.Asda2X;
      float deltaY = victim.Asda2Y - attacker.Asda2Y;
      float closeRangeSq = ClosePvpAttackVisualSyncRange * ClosePvpAttackVisualSyncRange;
      return deltaX * deltaX + deltaY * deltaY <= closeRangeSq;
    }

    private static void SendStartAttackAnimationToClient(Character attacker, Character victim,
      IRealmClient client)
    {
      if(attacker == null || victim == null || client == null)
        return;

      using(RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.StartAtackCharacterResponse))
      {
        packet.WriteByte(1);
        packet.WriteInt16(attacker.SessionId);
        packet.WriteInt16(victim.SessionId);
        packet.WriteFloat(victim.Asda2X);
        packet.WriteFloat(victim.Asda2Y);
        client.Send(packet, true);
      }
    }

    private static void SendCharacterAttackAnimationPulseToVictim(Character attacker, Character victim)
    {
      if(attacker == null || victim == null || victim.Client == null)
        return;

      SyncPvpAttackPositionToVictim(attacker, victim);
      SendStartAttackAnimationToClient(attacker, victim, victim.Client);
    }

    private static void CaptureClientAttackAnimationToken(Character attacker, RealmPacketIn packet)
    {
      if(attacker == null)
        return;

      attacker.AdvanceClientAttackAnimationToken();
    }

    public static void SendStartAtackCharacterError(Character atacker, Character victim,
      Asda2CharacterAtackStatus status)
    {
      using(RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.StartAtackCharacterResponse))
      {
        packet.WriteByte((byte) status);
        packet.WriteInt16(atacker.SessionId);
        packet.WriteInt16(victim.SessionId);
        packet.WriteFloat(victim.Asda2X);
        packet.WriteFloat(victim.Asda2Y);
        atacker.Send(packet, true);
      }
    }

    [PacketHandler(RealmServerOpCode.AtackCharacter)]
    public static void AtackCharacterRequest(IRealmClient client, RealmPacketIn packet)
    {
      Character attacker = client.ActiveCharacter;
      Character characterBySessionId = World.GetCharacterBySessionId(packet.ReadUInt16());
      RefreshCombatPosition(attacker);
      RefreshCombatPosition(characterBySessionId);
      Asda2CharacterAtackStatus status;
      if(TryGetCharacterAttackStartError(attacker, characterBySessionId, out status))
      {
        StopClientAttack(attacker);
        if(characterBySessionId != null)
          SendStartAtackCharacterError(attacker, characterBySessionId, status);
        return;
      }

      if(attacker.Target != characterBySessionId)
        attacker.Target = characterBySessionId;
      bool wasInitialPulse = attacker.IsProcessingInitialClientAttackPulse;
      if(!wasInitialPulse)
        CaptureClientAttackAnimationToken(attacker, packet);
      Action beforeStrike = wasInitialPulse
        ? null
        : (Action) (() =>
        {
          SendCharacterAttackAnimationPulseToVictim(attacker, characterBySessionId);
        });
      bool processed = UsesCrossbow(attacker)
        ? attacker.ProcessClientAttackStrike(characterBySessionId, GetEquippedAttackWeapon(attacker),
          beforeStrike)
        : attacker.ProcessClientAttackPulse(beforeStrike);
      if(!processed)
      {
        attacker.IsWaitingForAtackAnimation = true;
        return;
      }

      ConsumeAmmoIfRequired(client, attacker);
      attacker.IsWaitingForAtackAnimation = true;
    }

    [PacketHandler(RealmServerOpCode.ContinueAtack)]
    public static void ContinueAtackRequest(IRealmClient client, RealmPacketIn packet)
    {
      Character attacker = client.ActiveCharacter;
      Unit target = attacker.Target;
      RefreshCombatPosition(attacker);
      RefreshCombatPosition(target);
      Character targetCharacter = target as Character;
      if(target == null || target.IsDead)
      {
        attacker.IsWaitingForAtackAnimation = false;
        SendAttackStartError(attacker, target, Asda2CharacterAtackStatus.Fail);
        return;
      }
      if(!attacker.CanHarm(target) || !attacker.CanSee(target))
      {
        attacker.IsWaitingForAtackAnimation = false;
        SendAttackStartError(attacker, target, Asda2CharacterAtackStatus.Fail);
        return;
      }
      if(UsesShovel(attacker))
      {
        attacker.IsWaitingForAtackAnimation = false;
        SendAttackStartError(attacker, target, Asda2CharacterAtackStatus.YouCannotAtackWithShovel);
        StopClientAttack(attacker);
        return;
      }
      if(attacker.Asda2Inventory.WeightPrc >= 90)
      {
        attacker.IsWaitingForAtackAnimation = false;
        SendAttackStartError(attacker, target, Asda2CharacterAtackStatus.Inv90Proc);
        return;
      }
      if(!HasRequiredAmmo(attacker))
      {
        attacker.IsWaitingForAtackAnimation = false;
        SendAttackStartError(attacker, target, Asda2CharacterAtackStatus.DontHaveEnoughArrows);
        return;
      }
            NPC targetNpc = target as NPC;
            bool wasInitialPulse = attacker.IsProcessingInitialClientAttackPulse;
            if(!wasInitialPulse && targetCharacter != null)
                CaptureClientAttackAnimationToken(attacker, packet);
            Action beforeStrike = wasInitialPulse || targetCharacter == null
                ? null
                : (Action) (() =>
                {
                    SendCharacterAttackAnimationPulseToVictim(attacker, targetCharacter);
                });
            bool processed;
            if(UsesBallista(attacker) && targetNpc != null)
            {
                processed = attacker.HasClientAttackInstantStrikesRemaining
                    ? attacker.ProcessClientAttackInstantStrike(target, GetEquippedAttackWeapon(attacker), null)
                    : attacker.ProcessClientAttackStrike(target, GetEquippedAttackWeapon(attacker), null);
            }
            else
            {
                processed = UsesCrossbow(attacker) && targetCharacter != null
                ? attacker.ProcessClientAttackStrike(target, GetEquippedAttackWeapon(attacker), beforeStrike)
                : attacker.ProcessClientAttackPulse(beforeStrike);
            }
            if(!processed)
            {
                attacker.IsWaitingForAtackAnimation = true;
                return;
            }
            AlertNpcAboutIncomingAttack(attacker, target as NPC);

            ConsumeAmmoIfRequired(client, attacker);
            attacker.IsWaitingForAtackAnimation = true;
    }

    private static bool TryGetCharacterAttackStartError(Character attacker, Character victim,
      out Asda2CharacterAtackStatus status)
    {
      status = Asda2CharacterAtackStatus.Fail;
      if(attacker == null || victim == null || victim == attacker || !victim.IsAlive)
        return true;
      if(!attacker.CanHarm(victim))
        return true;
      if(attacker.Asda2FactionId == victim.Asda2FactionId &&
         victim.IsAsda2BattlegroundInProgress &&
         attacker.IsAsda2BattlegroundInProgress)
        return true;
      if(UsesShovel(attacker))
      {
        status = Asda2CharacterAtackStatus.YouCannotAtackWithShovel;
        return true;
      }
      if(attacker.Asda2Inventory.WeightPrc >= 90)
      {
        status = Asda2CharacterAtackStatus.Inv90Proc;
        return true;
      }
      if(!HasRequiredAmmo(attacker))
      {
        status = Asda2CharacterAtackStatus.DontHaveEnoughArrows;
        return true;
      }
      if(attacker.IsOnTransport)
      {
        status = Asda2CharacterAtackStatus.YouOnVehicle;
        return true;
      }
      if(!attacker.CanSee(victim))
      {
        status = Asda2CharacterAtackStatus.YouCannotAtackFromHere;
        return true;
      }

      return false;
    }

    private static void StopClientAttack(Character attacker)
    {
      if(attacker == null)
        return;
      attacker.Target = null;
      attacker.IsWaitingForAtackAnimation = false;
      attacker.IsFighting = false;
    }

    private static void SendAttackStartError(Character attacker, Unit target,
      Asda2CharacterAtackStatus status)
    {
      Character character = target as Character;
      if(character != null)
        SendStartAtackCharacterError(attacker, character, status);
      else
        StartAtackResponse(attacker, target, (byte) status);
    }

    private static bool UsesAmmo(Character character)
    {
      if(character == null || character.Asda2Inventory == null ||
         character.Asda2Inventory.Equipment[9] == null)
        return false;
      Asda2ItemCategory category = character.Asda2Inventory.Equipment[9].Category;
      return category == Asda2ItemCategory.Bow || category == Asda2ItemCategory.Crossbow ||
             category == Asda2ItemCategory.Ballista;
    }

    private static bool UsesCrossbow(Character character)
    {
      return character != null && character.Asda2Inventory != null &&
             character.Asda2Inventory.Equipment[9] != null &&
             character.Asda2Inventory.Equipment[9].Category == Asda2ItemCategory.Crossbow;
    }

    private static bool UsesBallista(Character character)
    {
      return character != null && character.Asda2Inventory != null &&
             character.Asda2Inventory.Equipment[9] != null &&
             character.Asda2Inventory.Equipment[9].Category == Asda2ItemCategory.Ballista;
    }

    private static bool UsesShovel(Character character)
    {
      return character != null && character.Asda2Inventory != null &&
             character.Asda2Inventory.Equipment[9] != null &&
             character.Asda2Inventory.Equipment[9].Category == Asda2ItemCategory.Showel;
    }

    private static bool IsBallistaAmmo(Asda2Item ammo)
    {
      return ammo != null &&
             (ammo.ItemId == 20569 || ammo.ItemId == 20570 || ammo.ItemId == 20571);
    }

    private static IAsda2Weapon GetEquippedAttackWeapon(Character character)
    {
      if(character == null)
        return null;
      if(character.Asda2Inventory != null && character.Asda2Inventory.Equipment[9] != null)
        return character.Asda2Inventory.Equipment[9];
      return character.MainWeapon;
    }

    private static void ScheduleInitialCrossbowCharacterAttack(IRealmClient client, Character attacker,
      Character victim, bool useMovingFallback)
    {
      if(!UsesCrossbow(attacker) || victim == null || attacker.Map == null)
        return;

      int syncId = attacker.ClientAttackSyncId;
      int impactDelay = GetInitialCrossbowCharacterImpactDelay(attacker);
      if(useMovingFallback)
      {
        int movingImpactDelay = GetMovingCrossbowCharacterImpactDelay(attacker);
        attacker.Map.CallDelayed(movingImpactDelay,
          () => ProcessScheduledInitialCrossbowCharacterAttack(client, attacker, victim, syncId,
            movingImpactDelay));
      }
      attacker.Map.CallDelayed(impactDelay,
        () => ProcessScheduledInitialCrossbowCharacterAttack(client, attacker, victim, syncId, impactDelay));
    }

    private static int GetInitialCrossbowCharacterImpactDelay(Character attacker)
    {
      int attackDelay = attacker == null || attacker.MainHandAttackTime <= 0
        ? 1000
        : attacker.MainHandAttackTime;
      return Math.Min(attackDelay, Math.Max(CrossbowInitialPvPMinImpactDelayMillis,
        attackDelay * CrossbowInitialPvPImpactDelayPct / 100));
    }

    private static int GetMovingCrossbowCharacterImpactDelay(Character attacker)
    {
      int attackDelay = attacker == null || attacker.MainHandAttackTime <= 0
        ? 1000
        : attacker.MainHandAttackTime;
      return Math.Min(attackDelay, Math.Max(1, CrossbowMovingInitialPvPImpactDelayMillis));
    }

    private static void RefreshCombatPosition(Unit unit)
    {
      if(unit != null)
        unit.RefreshCurrentPosition();
    }

    private static void ProcessScheduledInitialCrossbowCharacterAttack(IRealmClient client,
      Character attacker, Character victim, int syncId, int impactDelay)
    {
      RefreshCombatPosition(attacker);
      RefreshCombatPosition(victim);
      if(attacker == null || victim == null || attacker.Target != victim || !attacker.IsAlive ||
         !victim.IsAlive || !attacker.CanHarm(victim) || !attacker.CanSee(victim) ||
         UsesShovel(attacker) || !HasRequiredAmmo(attacker))
        return;
      if(!attacker.TryProcessScheduledClientAttackStrike(syncId, victim, GetEquippedAttackWeapon(attacker)))
        return;

      ConsumeAmmoIfRequired(client ?? attacker.Client, attacker);

      int duplicateGuard = Math.Max(1, Unit.ClientAttackSyncDuplicateGuardMillis);
      int skipDelay = Math.Max(duplicateGuard, attacker.GetClientAttackSyncDelay() - impactDelay + duplicateGuard);
      attacker.SkipNextClientAttackPulseForMillis(skipDelay);
      attacker.IsWaitingForAtackAnimation = true;
    }

    private static void ScheduleInitialBallistaNpcAttack(IRealmClient client, Character attacker,
      NPC victim, byte status)
    {
      if(status != 1 || !UsesBallista(attacker) || victim == null || attacker.Map == null)
        return;

      int syncId = attacker.ClientAttackSyncId;
      int impactDelay = GetInitialBallistaNpcPreStrikeImpactDelay();
      if(impactDelay <= 0)
      {
        ProcessScheduledInitialBallistaNpcAttack(client, attacker, victim, syncId);
        return;
      }
      attacker.Map.CallDelayed(impactDelay,
        () => ProcessScheduledInitialBallistaNpcAttack(client, attacker, victim, syncId));
    }

    private static void SendStartAtackResponseWithInitialBallistaDelay(Character attacker,
      NPC victim, byte status)
    {
      int animationDelay = Math.Max(0, BallistaInitialNpcAnimationDelayMillis);
      if(attacker == null || status != 1 || !UsesBallista(attacker) || victim == null ||
         attacker.Map == null || animationDelay <= 0)
      {
        StartAtackResponse(attacker, victim, status);
        return;
      }

      int syncId = attacker.ClientAttackSyncId;
      attacker.Map.CallDelayed(animationDelay,
        () =>
        {
          if(attacker == null || victim == null || attacker.ClientAttackSyncId != syncId ||
             attacker.Target != victim || !attacker.IsAlive || !victim.IsAlive ||
             !UsesBallista(attacker))
            return;
          StartAtackResponse(attacker, victim, status);
        });
    }

    private static int GetInitialBallistaNpcPreStrikeImpactDelay()
    {
      return Math.Max(0, BallistaInitialNpcPreStrikeImpactDelayMillis);
    }

    private static void ProcessScheduledInitialBallistaNpcAttack(IRealmClient client,
      Character attacker, NPC victim, int syncId)
    {
      RefreshCombatPosition(attacker);
      RefreshCombatPosition(victim);
      if(attacker == null || victim == null || attacker.Target != victim || !attacker.IsAlive ||
         !victim.IsAlive || !attacker.CanHarm(victim) || !attacker.CanSee(victim) ||
         UsesShovel(attacker) || !HasRequiredAmmo(attacker))
        return;

      if(!attacker.TryProcessScheduledClientAttackStrike(syncId, victim, GetEquippedAttackWeapon(attacker)))
        return;

      attacker.ConsumeClientAttackInstantStrike();
      AlertNpcAboutIncomingAttack(attacker, victim);
      ConsumeAmmoIfRequired(client ?? attacker.Client, attacker);
      int duplicateSkipDelay = Math.Max(BallistaInitialNpcDuplicateSkipMillis,
        attacker.GetClientAttackSyncDelay() / 2);
      attacker.SkipNextClientAttackPulseForMillis(duplicateSkipDelay);
      attacker.IsWaitingForAtackAnimation = true;
    }

    private static bool HasRequiredAmmo(Character character)
    {
      if(!UsesAmmo(character))
        return true;
      Asda2Item ammo = character.Asda2Inventory.Equipment[10];
      if(ammo == null || ammo.Amount <= 0)
        return false;
      Asda2ItemCategory weaponCategory = character.Asda2Inventory.Equipment[9].Category;
      if(weaponCategory == Asda2ItemCategory.Bow)
        return ammo.Category == Asda2ItemCategory.BowAmmo;
      if(weaponCategory == Asda2ItemCategory.Crossbow)
        return ammo.Category == Asda2ItemCategory.CrossbowAmmo ||
               ammo.ItemId == 20566 || ammo.ItemId == 20567 || ammo.ItemId == 20568;
      if(weaponCategory == Asda2ItemCategory.Ballista)
        return IsBallistaAmmo(ammo);
      return true;
    }

    private static void ConsumeAmmoIfRequired(IRealmClient client, Character attacker)
    {
      if(!UsesAmmo(attacker))
        return;
      Asda2Item ammo = attacker.Asda2Inventory.Equipment[10];
      if(ammo == null || ammo.Amount <= 0)
        return;
      ammo.ModAmount(-1);
      if(client != null)
        SendUpdateAmmoResponse(client, ammo);
    }

    private static void AlertNpcAboutIncomingAttack(Character attacker, NPC npc)
    {
      if(attacker == null || npc == null || !attacker.IsAlive || !npc.IsAlive ||
         !npc.CanBeAggroedBy(attacker))
        return;

      bool wasEngagedWithAttacker = npc.IsInCombat && npc.Target == attacker &&
        npc.ThreatCollection.HasAggressor(attacker);
      int currentThreat = npc.ThreatCollection[attacker];

      npc.Target = attacker;
      npc.ThreatCollection[attacker] = Math.Max(1, currentThreat + 1);
      npc.IsInCombat = true;

      if(!wasEngagedWithAttacker)
        npc.GotAttacked();

      if(npc.Brain == null)
        return;

      if(!npc.Brain.IsRunning)
        npc.Brain.IsRunning = true;

      bool canStartCombat = npc.Brain.CheckCombat();
      npc.Brain.Perform();
      if(canStartCombat && !npc.IsFighting)
        npc.IsFighting = true;
    }

    public static void SendUpdateAmmoResponse(IRealmClient client, Asda2Item ammo)
    {
        using (RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.UpdateAmmoResponse))
        {
            packet.WriteByte(ammo == null ? 0 : 1);
            packet.WriteInt32(ammo == null ? 0 : ammo.Amount);
            client.Send(packet, true);
        }
    }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chr"></param>
        /// <param name="target"></param>
        /// <param name="status">0 - stop;1-start;2 90% weight;3 cannot see target</param>
        public static void StartAtackResponse(Character chr, Unit target, byte status)
    {
      NPC npc = target as NPC;
      using(RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.StartAtackResponse))
      {
        packet.WriteByte(status);
        packet.WriteInt16(chr.SessionId);
        packet.WriteInt16(npc == null ? -1 : npc.UniqIdOnMap);
        packet.WriteInt16(0);
        packet.WriteInt16(0);
        packet.WriteInt32(npc == null ? -1 : npc.UniqWorldEntityId);
        chr.SendPacketToArea(packet, true, false, Locale.Any, new float?(), chr.Client.Channel);
      }
    }

    public static void SendAttackerStateUpdate(DamageAction action)
    {
      if(action.Attacker is Character && action.Victim is NPC)
      {
        Character attacker = (Character) action.Attacker;
        NPC victim = (NPC) action.Victim;

        using (RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.MonstrTakeDmg))
        {
          packet.WriteInt16(attacker.SessionId);
          packet.WriteInt16(victim.UniqIdOnMap);
          packet.WriteInt32(victim.UniqWorldEntityId);
          packet.WriteInt32(GetAsda2DamageValue(action));
          packet.WriteSkip(unk8);
          victim.SendPacketToArea(packet, true, true, Locale.Any, new float?(), attacker.Client.Channel);
        }
      }
      else if(action.Attacker is NPC && action.Victim is Character)
      {
        NPC attacker = (NPC) action.Attacker;
        Character victim = (Character) action.Victim;
        int dmg = GetAsda2DamageValue(action);
        Asda2MovmentHandler.SendMonstMoveOrAtackResponse(victim.SessionId, attacker, dmg,
          attacker.Asda2Position, true);
      }
      else
      {
        if(!(action.Attacker is Character) || !(action.Victim is Character))
          return;
        int val = GetAsda2DamageValue(action);
        Character attacker = action.Attacker.CharacterMaster;
        Character victim = action.Victim.CharacterMaster;
        if(attacker == null || victim == null)
          return;
        int animationToken = GetCharacterAttackAnimationToken(attacker);
        using(RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.AtackCharacterRes))
        {
          packet.WriteInt16(attacker.SessionId);
          packet.WriteInt16(victim.SessionId);
          packet.WriteInt32(animationToken);
          packet.WriteInt32(val);
          packet.WriteByte(0);
          packet.WriteInt16(-1);
          packet.WriteInt32(animationToken);
          byte? channel = attacker.Client == null ? new byte?() : attacker.Client.Channel;
          if(UsesAmmo(attacker) && attacker.IsProcessingInitialClientAttackPulse)
          {
            if(attacker.Client != null)
              attacker.Send(packet, true);
            if(victim.Client != null)
              victim.Send(packet, true);
          }
          else if(UsesAmmo(attacker))
          {
            attacker.SendPacketToArea(packet, true, true, Locale.Any, new float?(), channel);
            if(victim.Client != null && !IsCharacterIncludedInAreaPacket(attacker, victim, channel))
              victim.Send(packet, true);
          }
          else
          {
            victim.SendPacketToArea(packet, true, true, Locale.Any, new float?(), channel);
            if(attacker.Client != null && !IsCharacterIncludedInAreaPacket(victim, attacker, channel))
              attacker.Send(packet, true);
          }
        }
      }
    }

    private static bool IsCharacterIncludedInAreaPacket(WorldObject source, Character receiver,
      byte? channel)
    {
      return source != null && receiver != null && receiver.Client != null &&
             source.Map == receiver.Map &&
             WorldObject.BroadcastRange > source.GetDistance(receiver) &&
             (!channel.HasValue || receiver.Client.Channel == channel.Value);
    }

    private static int GetCharacterAttackAnimationToken(Character attacker)
    {
      if(attacker == null || attacker.ClientAttackAnimationToken <= 0)
        return 1;
      return attacker.ClientAttackAnimationToken;
    }

    private static int GetAsda2DamageValue(DamageAction action)
    {
      if(action.VictimState == VictimState.Miss)
        return 0;
      if(action.VictimState == VictimState.Evade)
        return -1;
      if(action.VictimState == VictimState.Block)
        return -2;
      if(action.VictimState == VictimState.Immune)
        return -3;
      if(action.VictimState == VictimState.Deflect || action.VictimState == VictimState.Dodge ||
         action.VictimState == VictimState.Interrupt || action.VictimState == VictimState.Parry)
        return -1;
      return unchecked((int) (action.ActualDamage + (action.IsCritical ? 2147483648L : 0L)));
    }

    public static void SendMostrDeadToAreaResponse(ICollection<IRealmClient> clients, short npcId, short x, short y)
    {
      using(RealmPacketOut monstrDeadPacket = CreateMonstrDeadPacket(npcId, x, y))
      {
        foreach(IPacketReceiver client in clients)
          client.Send(monstrDeadPacket, true);
      }
    }

    private static RealmPacketOut CreateMonstrDeadPacket(short npc, short x, short y)
    {
      RealmPacketOut realmPacketOut = new RealmPacketOut(RealmServerOpCode.MonstrStateChanged);
      realmPacketOut.WriteSkip(stab6);
      realmPacketOut.WriteInt16(npc);
      realmPacketOut.WriteSkip(stab10);
      realmPacketOut.WriteInt16(x);
      realmPacketOut.WriteInt16(y);
      realmPacketOut.WriteInt16(8557);
      return realmPacketOut;
    }

    public static void SendMonstrStateChangedResponse(NPC npc, Asda2NpcState state)
    {
      if(npc == null)
        return;
      using(RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.MonstrStateChanged))
      {
        packet.WriteSkip(stab6);
        packet.WriteInt16(npc.UniqIdOnMap);
        packet.WriteInt32((int) state);
        for(int index = 0; index < 28; ++index)
        {
          Aura aura = null;
          if(npc.Auras.ActiveAuras.Length > index)
            aura = npc.Auras.ActiveAuras[index];
          packet.WriteInt16(aura == null ? -1 : aura.Spell.RealId);
        }

        for(int index = 0; index < 28; ++index)
        {
          Aura aura = null;
          if(npc.Auras.ActiveAuras.Length > index)
            aura = npc.Auras.ActiveAuras[index];
          packet.WriteByte(aura == null ? 0 : 1);
        }

        packet.WriteInt32(npc.Health);
        packet.WriteInt16((short) npc.Position.X);
        packet.WriteInt16((short) npc.Position.Y);
        npc.SendPacketToArea(packet, false, true, Locale.Any, new float?(), npc.Channel);
      }
    }

    public static void SendNpcBuffedResponse(NPC target, Aura aura)
    {
      if(target == null || aura == null || aura.Spell == null)
        return;
      using(RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.NpcBuffed))
      {
        packet.WriteByte(1);
        packet.WriteInt16(0);
        packet.WriteByte(0);
        packet.WriteInt16(target.UniqIdOnMap);
        packet.WriteInt16(aura.Spell.RealId);
        packet.WriteInt16(aura.Spell.RealId);
        packet.WriteByte(aura.Spell.Level);
        packet.WriteInt32(aura.Duration);
        packet.WriteInt16(aura.Amplitude);
        packet.WriteInt32(0);
        target.SendPacketToArea(packet, false, true, Locale.Any, new float?(), target.Channel);
      }
      SendMonstrStateChangedResponse(target, Asda2NpcState.Ok);
    }
  }
}
