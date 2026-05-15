using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using WCell.Constants;
using WCell.Constants.Achievements;
using WCell.Constants.Items;
using WCell.Constants.Spells;
using WCell.Core.Network;
using WCell.RealmServer.Achievements;
using WCell.RealmServer.Asda2_Items;
using WCell.RealmServer.Database;
using WCell.RealmServer.Entities;
using WCell.RealmServer.Global;
using WCell.RealmServer.Items;
using WCell.RealmServer.Misc;
using WCell.RealmServer.Network;
using WCell.RealmServer.Skills;
using WCell.RealmServer.Spells;
using WCell.RealmServer.Spells.Auras;

namespace WCell.RealmServer.Handlers
{
  internal class Asda2SpellHandler
  {
    private const short ChargeAnimationRealId = (short) SpellLineId.Charge;
    private const int ChargeImpactEffectId = 283;
    private const int GrandEarthquakeImpactEffectId = 797;
    private const float ChargeMoveSpeed = 5f;
    private const short FuriousChargeRealId = (short) SpellLineId.FuriousCharge;

    private static readonly byte[] unk12 = new byte[15]
    {
      0,
      0,
      0,
      0,
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
      0
    };

    private static readonly byte[] unk14 = new byte[21]
    {
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
      byte.MaxValue,
      byte.MaxValue,
      0,
      0,
      0,
      0,
      0,
      0,
      0,
      0
    };

    private static readonly byte[] stub14 = new byte[20]
    {
      0,
      0,
      198,
      112,
      211,
      37,
      0,
      0,
      0,
      0,
      1,
      0,
      0,
      0,
      0,
      0,
      0,
      0,
      1,
      0
    };

    private static readonly byte[] stab7 = new byte[2]
    {
      5,
      0
    };

    private static readonly byte[] stab16 = new byte[1]
    {
      1
    };

    private static readonly byte[] stub87 = new byte[28];
    private static readonly byte[] stab12 = new byte[2];

    private static readonly byte[] stab24 = new byte[16]
    {
      8,
      0,
      224,
      147,
      4,
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

    [PacketHandler(RealmServerOpCode.UseSkill)]
    public static void UseSkillRequest(IRealmClient client, RealmPacketIn packet)
    {
      client.ActiveCharacter.RefreshCurrentPosition();
      client.ActiveCharacter.IsFighting = false;
      client.ActiveCharacter.IsMoving = false;
      short skillId = packet.ReadInt16();
      ++packet.Position;
      int num1 = packet.ReadInt16();
      int num2 = packet.ReadInt16();
      byte targetType = packet.ReadByte();
      ushort targetId = packet.ReadUInt16();
      Spell spellByRealId = client.ActiveCharacter.Spells.GetSpellByRealId(skillId);
      if(spellByRealId == null)
        return;
      if(spellByRealId.SoulGuardProffLevel != 0)
        ProcessUseSoulGuardSkill(client, spellByRealId, targetType, skillId, targetId);
      else
        ProcessUseSkill(client, targetType, skillId, targetId);
    }

    [PacketHandler(RealmServerOpCode.CancelSkill)]
    public static void CancelSkillRequest(IRealmClient client, RealmPacketIn packet)
    {
      ++packet.Position;
      short skillId = packet.ReadInt16();
      client.ActiveCharacter.Auras.RemoveFirstVisibleAura(a =>
      {
        if(a.Spell.RealId == skillId)
          return a.IsBeneficial;
        return false;
      });
    }


    private static void ProcessUseSkill(IRealmClient client, byte targetType, short skillId, ushort targetId)
    {
        Character activeCharacter = client.ActiveCharacter;
        if (activeCharacter == null)
            return;

        Spell spell = activeCharacter.Spells.GetSpellByRealId(skillId);
        if (spell == null)
            return;

        Unit target = null;
        bool unknownTargetType = false;
        if (targetType == 0)
            target = activeCharacter.Map.GetNpcByUniqMapId(targetId, client.Channel);
        else if (targetType == 1)
        {
            Character targetCharacter = World.GetCharacterBySessionId(targetId);
            if (targetCharacter != null && targetCharacter.Client != null &&
                targetCharacter.Client.Channel == client.Channel)
                target = targetCharacter;
        }
        else
            unknownTargetType = true;

        if (target == null && CanUseSkillWithoutExplicitTarget(spell))
            target = activeCharacter;

        if (target == null)
        {
            if (unknownTargetType)
                activeCharacter.SendSystemMessage(
                    string.Format("Unknown skill target type {0}. SkillId {1}. Please report to developers.",
                                    targetType, skillId));
            activeCharacter.Target = null;
            activeCharacter.SendInfoMsg("Bad target.");
            SendUseSkillResultResponse(activeCharacter, skillId, Asda2UseSkillResult.YouCannotTargetThisObject);
            return;
        }
        activeCharacter.RefreshCurrentPosition();
        target.RefreshCurrentPosition();
        activeCharacter.Target = target;
        if (UsesShovel(activeCharacter))
        {
            SendUseSkillResultResponse(activeCharacter, skillId, Asda2UseSkillResult.WrongWeapon);
            return;
        }
        if (spell != null)
        {
            SendSetAtackStateGuiResponse(activeCharacter);
            SpellCast cast = activeCharacter.SpellCast;
            var reason = cast.Start(spell, target);
            if (reason == SpellFailedReason.Ok)
            {
                if (spell.LearnLevel < 10)
                {
                    if (activeCharacter.GreenCharges < 10)
                        activeCharacter.GreenCharges += 1;
                }
                else if (spell.LearnLevel < 30)
                {
                    if (activeCharacter.GreenCharges < 10)
                        activeCharacter.GreenCharges += 1;
                }
                else if (spell.LearnLevel < 50)
                {
                    if (activeCharacter.BlueCharges < 10)
                        activeCharacter.BlueCharges += 1;
                    if (activeCharacter.GreenCharges < 10)
                        activeCharacter.GreenCharges += 1;

                }
                else
                {
                    if (activeCharacter.RedCharges < 10)
                        activeCharacter.RedCharges += 1;
                    if (activeCharacter.BlueCharges < 10)
                        activeCharacter.BlueCharges += 1;
                    if (activeCharacter.GreenCharges < 10)
                        activeCharacter.GreenCharges += 1;

                }

                var npc = target as NPC;

                    if(npc != null)
                    {
                        npc.GotAttacked();
                    }

                AchievementProgressRecord progressRecord =
                    activeCharacter.Achievements.GetOrCreateProgressRecord(6U);
                switch (++progressRecord.Counter)
                {
                    case 50:
                        activeCharacter.DiscoverTitle(Asda2TitleId.Skilled44);
                        break;
                    case 100:
                        activeCharacter.GetTitle(Asda2TitleId.Skilled44);
                        break;
                }

                progressRecord.SaveAndFlush();
                SendSetSkiillPowersStatsResponse(activeCharacter, true, skillId);
            }
            else if (reason == SpellFailedReason.OutOfRange)
            {
                Asda2MovmentHandler.MoveToSelectedTargetAndAttack(activeCharacter);
            }
        }
      }

    private static bool CanUseSkillWithoutExplicitTarget(Spell spell)
    {
        return spell != null && !spell.TargetFlags.HasAnyFlag(SpellTargetFlags.Unit);
    }

    private static bool UsesShovel(Character character)
    {
        return character != null && character.Asda2Inventory != null &&
               character.Asda2Inventory.Equipment[9] != null &&
               character.Asda2Inventory.Equipment[9].Category == Asda2ItemCategory.Showel;
    }

    

    

    [PacketHandler(RealmServerOpCode.UseSoulGuardSkill)]
    public static void UseSoulGuardSkillRequest(IRealmClient client, RealmPacketIn packet)
    {
      client.ActiveCharacter.RefreshCurrentPosition();
      client.ActiveCharacter.IsFighting = false;
      client.ActiveCharacter.IsMoving = false;
      short skillId = packet.ReadInt16();
      ++packet.Position;
      int num1 = packet.ReadInt16();
      int num2 = packet.ReadInt16();
      byte targetType = packet.ReadByte();
      ushort targetId = packet.ReadUInt16();
      Spell spellByRealId = client.ActiveCharacter.Spells.GetSpellByRealId(skillId);
      if(spellByRealId == null)
        return;
      ProcessUseSoulGuardSkill(client, spellByRealId, targetType, skillId, targetId);
    }

    private static void ProcessUseSoulGuardSkill(IRealmClient client, Spell spell, byte targetType, short skillId,
      ushort targetId)
    {
      if(client == null || client.ActiveCharacter == null || spell == null)
        return;
      if(spell.SoulGuardProffLevel < 1 || spell.SoulGuardProffLevel > 3)
      {
        client.ActiveCharacter.YouAreFuckingCheater("Trying to use skill as SoulguardSkill.", 1);
        return;
      }

      if(!TryConsumeSoulGuardCharges(client.ActiveCharacter, spell.SoulGuardProffLevel))
        return;
      ProcessUseSkill(client, targetType, skillId, targetId);
      SendSetSkiillPowersStatsResponse(client.ActiveCharacter, false, 0);
    }

    private static bool TryConsumeSoulGuardCharges(Character character, byte soulGuardProffLevel)
    {
      switch(soulGuardProffLevel)
      {
        case 1:
          if(character.GreenCharges < 5)
            return SendNotEnoughSoulGuardCharges(character);
          character.GreenCharges -= 5;
          return true;
        case 2:
          if(character.BlueCharges < 5)
            return SendNotEnoughSoulGuardCharges(character);
          character.BlueCharges -= 5;
          return true;
        case 3:
          if(character.RedCharges < 5)
            return SendNotEnoughSoulGuardCharges(character);
          character.RedCharges -= 5;
          return true;
      }

      return false;
    }

    private static bool SendNotEnoughSoulGuardCharges(Character character)
    {
      character.SendInfoMsg("Not enougt charges.");
      SendSetSkiillPowersStatsResponse(character, false, 0);
      return false;
    }

    public static void SendSetSkiillPowersStatsResponse(Character chr, bool animate, short skillId)
    {
      using(RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.SetSkiillPowersStats))
      {
        packet.WriteInt32(355335);
        packet.WriteByte(animate ? 1 : 0);
        packet.WriteByte((byte) chr.Archetype.ClassId);
        packet.WriteInt16(skillId);
        packet.WriteByte(chr.GreenCharges);
        packet.WriteByte(chr.BlueCharges);
        packet.WriteByte(chr.RedCharges);
        chr.Send(packet, true);
      }
    }

    /// <summary>Clears a single spell's cooldown</summary>
    public static void SendClearCoolDown(Character chr, SpellId spellId)
    {
      Spell spell = SpellHandler.Get(spellId);
      if(spell == null)
        chr.SendSystemMessage(string.Format("Can't clear cooldown for {0} cause skill not exist.",
          spellId));
      else
        SendClearCoolDown(chr, spell.RealId);
    }

    public static void SendClearCoolDown(Character chr, short realId)
    {
      if(chr == null)
        return;
      using(RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.SkillReady))
      {
        packet.WriteInt16(realId);
        chr.Send(packet, true);
      }
    }

    public static void SendSetSkillCooldownResponse(Character chr, Spell spell)
    {
      if(chr == null || spell == null)
        return;
      using(RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.SetSkillCooldown))
      {
        packet.WriteByte(1);
        packet.WriteInt16(chr.SessionId);
        packet.WriteInt16(spell.RealId);
        packet.WriteInt16(2);
        chr.Send(packet, false);
      }
    }

    public static void SendBuffEndedResponse(Character chr, short buffId)
    {
      using(RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.BuffEnded))
      {
        packet.WriteInt16(chr.SessionId);
        packet.WriteInt16(buffId);
        chr.SendPacketToArea(packet, true, true, Locale.Any, new float?());
      }
    }

    public static void SendUseSkillResultResponse(Character chr, short skillId, Asda2UseSkillResult status)
    {
      using(RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.UseSkillResult))
      {
        packet.WriteByte((byte) status);
        packet.WriteInt16(chr.SessionId);
        packet.WriteInt16(skillId);
        packet.WriteByte(0);
        packet.WriteInt16(-1);
        chr.Send(packet, false);
      }
    }

    public static void SendMonstrUsedSkillResponse(NPC caster, short skillId, Unit initialTarget,
      DamageAction[] actions)
    {
      if(caster == null)
        return;
      using(RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.MonstrUsedSkill))
      {
        Character character = initialTarget as Character;
        packet.WriteByte(0);
        packet.WriteInt16(skillId);
        packet.WriteInt16(caster.UniqIdOnMap);
        packet.WriteByte(0);
        packet.WriteByte(1);
        packet.WriteInt16(character == null ? 0 : character.SessionId);
        int num1 = 0;
        if(actions != null)
        {
          foreach(DamageAction action in actions)
          {
            if(num1 <= 16 && action != null)
            {
              Character victim = action.Victim as Character;
              packet.WriteByte(1);
              packet.WriteInt16(victim == null ? 0 : victim.SessionId);
              int num2 = action.ActualDamage;
              if(num2 < 0 || num2 > 200000000)
                num2 = 0;
              packet.WriteInt32(actions.Length == 0 ? 0 : num2);
              packet.WriteByte(actions.Length == 0 ? 3 : 1);
              packet.WriteSkip(unk14);
              ++num1;
            }
            else
              break;
          }
        }
        caster.SendPacketToArea(packet, false, true, Locale.Any, new float?());
      }
    }

    public static void SendAnimateSkillStrikeResponse(Character caster, short spellRealId, DamageAction[] actions,
      Unit initialTarget)
    {
      SendSetAtackStateGuiResponse(caster);
      using(RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.AnimateSkillStrike))
      {
        NPC npc = initialTarget as NPC;
        Character character = initialTarget as Character;
        bool isFuriousCharge = spellRealId == FuriousChargeRealId;
        short visibleSpellRealId = GetVisibleSkillRealId(spellRealId);
        int impactEffectId = GetSkillImpactEffectId(caster, visibleSpellRealId);
        if(character == null && npc == null)
          caster.SendSystemMessage(string.Format("Wrong spell target {0}. can't animate cast. SpellId {1}",
            initialTarget, spellRealId));
        if(isFuriousCharge)
          SendPendingChargeAnimationPath(caster, npc);
        packet.WriteInt16(caster.SessionId);
        packet.WriteInt16(visibleSpellRealId);
        packet.WriteInt16(6);
        packet.WriteByte(GetSkillStrikeActionCount(actions, initialTarget));
        packet.WriteByte(npc == null ? (byte) 1 : (byte) 0);
        if(character != null && actions != null)
        {
          int writtenEntries = 0;
          for(int index = 0; index < actions.Length; ++index)
          {
            DamageAction action = actions[index];
            if(action != null)
            {
              SpellHitStatus spellHitStatus = SpellHitStatus.Ok;
              if(action.IsCritical)
                spellHitStatus = SpellHitStatus.Crit;
              else if(action.Damage == 0)
                spellHitStatus = SpellHitStatus.Miss;
              else if(action.Blocked > 0)
                spellHitStatus = SpellHitStatus.Bloced;
              if(writtenEntries < 16)
              {
                packet.WriteUInt16(character.SessionId);
                int actualDamage = GetSkillStrikeDamage(action);
                packet.WriteInt32(actualDamage);
                packet.WriteInt32((byte) spellHitStatus);
                packet.WriteInt32(impactEffectId);
                packet.WriteSkip(unk12);
                ++writtenEntries;
              }

              action.OnFinished();
            }
          }
        }
        else if(actions != null)
        {
          int writtenEntries = 0;
          for(int index = 0; index < actions.Length; ++index)
          {
            DamageAction action = actions[index];
            if(action != null)
            {
              SpellHitStatus spellHitStatus = SpellHitStatus.Ok;
              if(action.IsCritical)
                spellHitStatus = SpellHitStatus.Crit;
              else if(action.Damage == 0)
                spellHitStatus = SpellHitStatus.Miss;
              else if(action.Blocked > 0)
                spellHitStatus = SpellHitStatus.Bloced;
              ushort val = 0;
              if(initialTarget is NPC)
                val = action.Victim == null || !(action.Victim is NPC)
                  ? ushort.MaxValue
                  : action.Victim.UniqIdOnMap;
              if(writtenEntries < 16)
              {
                packet.WriteUInt16(val);
                int actualDamage = GetSkillStrikeDamage(action);
                packet.WriteInt32(actualDamage);
                packet.WriteInt32((byte) spellHitStatus);
                packet.WriteInt32(impactEffectId);
                packet.WriteSkip(unk12);
                ++writtenEntries;
              }

              action.OnFinished();
            }
          }
        }
        else if(character != null)
        {
          packet.WriteUInt16(character.SessionId);
          packet.WriteInt32(0);
          packet.WriteInt32(3);
          packet.WriteInt32(impactEffectId);
          packet.WriteSkip(unk12);
        }
        else if(npc != null)
        {
          packet.WriteUInt16(npc.UniqIdOnMap);
          packet.WriteInt32(0);
          packet.WriteInt32(3);
          packet.WriteInt32(impactEffectId);
          packet.WriteSkip(unk12);
        }

        caster.SendPacketToArea(packet, true, false, Locale.Any, new float?());
      }
    }

    private static int GetSkillStrikeDamage(DamageAction action)
    {
      if(action == null)
        return 0;
      int damage = action.ActualDamage;
      if(damage < 0 || damage > 200000000)
        return 0;
      return damage;
    }

    private static void SendPendingChargeAnimationPath(Character caster, NPC npc)
    {
      if(caster == null || !caster.HasPendingChargeAnimationPath)
        return;

      Asda2MovmentHandler.SendStartMoveCommonToAreaResponse(caster, true,
        npc == null ? -1 : npc.UniqIdOnMap, caster.PendingChargeAnimationStart,
        caster.PendingChargeAnimationEnd, ChargeMoveSpeed);
      caster.HasPendingChargeAnimationPath = false;
    }

    private static short GetVisibleSkillRealId(short spellRealId)
    {
      return spellRealId == FuriousChargeRealId ? ChargeAnimationRealId : spellRealId;
    }

    private static byte GetSkillStrikeActionCount(DamageAction[] actions, Unit initialTarget)
    {
      if(actions == null)
        return initialTarget is Character || initialTarget is NPC ? (byte) 1 : (byte) 0;
      return (byte) GetSkillStrikeRealActionCount(actions);
    }

    private static int GetSkillStrikeRealActionCount(DamageAction[] actions)
    {
      if(actions == null)
        return 0;
      int count = 0;
      for(int index = 0; index < actions.Length && index < 16; ++index)
      {
        if(actions[index] != null)
          ++count;
      }

      return count;
    }

    private static int GetSkillImpactEffectId(Character caster, short spellRealId)
    {
      if(spellRealId == ChargeAnimationRealId)
        return ChargeImpactEffectId;

      Spell spell = caster == null ? null : caster.Spells.GetSpellByRealId(spellRealId);
      if(spell == null)
        return 0;
      if(IsGrandEarthquake(spell))
        return GetGrandEarthquakeImpactEffectId(spell);
      if(spell.Effect0_EffectType == SpellEffectType.CastAnotherSpell)
        return GetFirstVisibleEffectRealId(spell, spell.Effect0_MiscValue, spell.Effect0_MiscValueB, spell.Effect0_MiscValueC);
      if(spell.Effect1_EffectType == SpellEffectType.CastAnotherSpell)
        return GetFirstVisibleEffectRealId(spell, spell.Effect1_MiscValue, spell.Effect1_MiscValueB, spell.Effect1_MiscValueC);
      if(spell.Effect0_EffectType == SpellEffectType.ApplyAura ||
         spell.Effect1_EffectType == SpellEffectType.ApplyAura)
        return spell.RealId;
      if(spell.Effect0_EffectType == SpellEffectType.DamageFromPrcAtack ||
         spell.Effect1_EffectType == SpellEffectType.DamageFromPrcAtack)
        return spell.RealId;
      return 0;
    }

    private static bool IsGrandEarthquake(Spell spell)
    {
      return spell != null && spell.RealId == (short) SpellLineId.GrandEarthquake;
    }

    private static int GetGrandEarthquakeImpactEffectId(Spell spell)
    {
      return GrandEarthquakeImpactEffectId;
    }

    private static int GetFirstVisibleEffectRealId(Spell parentSpell, int first, int second, int third)
    {
      int realId = GetEffectSpellRealId(first);
      if(realId == 0)
        realId = GetEffectSpellRealId(second);
      if(realId == 0)
        realId = GetEffectSpellRealId(third);
      return realId == 0 || realId == 235 ? parentSpell.RealId : realId;
    }

    private static int GetEffectSpellRealId(int spellId)
    {
      if(spellId <= 0)
        return 0;
      Spell spell = SpellHandler.Get((uint) spellId);
      return spell == null ? 0 : spell.RealId;
    }

    public static void SendSetAtackStateGuiResponse(Character chr)
    {
      using(RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.SetAtackStateGui))
      {
        packet.WriteInt16(chr.SessionId);
        packet.WriteInt32(chr.Account.AccountId);
        chr.SendPacketToArea(packet, true, true, Locale.Any, new float?());
      }
    }

    public static void SendMonstrTakesDamageSecondaryResponse(Character chr, Character targetChr, NPC targetNpc,
      int damage)
    {
      if(targetChr == null && targetNpc == null)
        return;
      using(RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.MonstrTakesDamageSecondary))
      {
        packet.WriteByte(targetNpc != null ? 0 : 1);
        packet.WriteInt16(targetNpc != null ? (short) targetNpc.UniqIdOnMap : targetChr.SessionId);
        packet.WriteInt16(160);
        packet.WriteInt32(damage);
        packet.WriteInt32(450);
        packet.WriteByte(1);
        packet.WriteInt16(66);
        packet.WriteByte(0);
        if(targetChr != null)
          targetChr.SendPacketToArea(packet, true, true, Locale.Any, new float?(), targetChr.Client.Channel);
        else
          targetNpc.SendPacketToArea(packet, true, true, Locale.Any, new float?(), targetNpc.Channel);
      }
    }

    public static void SendCharacterBuffedResponse(Character target, Aura aura)
    {
      if(aura.Spell == null)
        return;
      using(RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.CharacterBuffed))
      {
        packet.WriteInt16(target.SessionId);
        packet.WriteInt16(aura.Spell.RealId);
        packet.WriteInt16(aura.Spell.BuffIconId);
        packet.WriteInt16(aura.Spell.RealId);
        packet.WriteInt16(1);
        packet.WriteByte(2);
        packet.WriteInt16((short) (aura.TimeLeft / 1000));
        packet.WriteByte(2);
        packet.WriteSkip(stub14);
        target.SendPacketToArea(packet, true, true, Locale.Any, new float?());
      }
    }

    [PacketHandler(RealmServerOpCode.LearnSkill)]
    public static void LearnSkillRequest(IRealmClient client, RealmPacketIn packet)
    {
      short skillId = packet.ReadInt16();
      byte level = packet.ReadByte();
      SkillLearnStatus status = client.ActiveCharacter.PlayerSpells.TryLearnSpell(skillId, level);
      if(status == SkillLearnStatus.Ok)
        return;
      SendSkillLearnedResponse(status, client.ActiveCharacter, 0U, 0);
    }

    public static void SendSkillLearnedResponse(SkillLearnStatus status, Character ownerChar, uint id, int level)
    {
      using(RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.SkillLearned))
      {
        packet.WriteByte((byte) status);
        packet.WriteInt16(ownerChar.Spells.AvalibleSkillPoints);
        packet.WriteInt32(ownerChar.Money);
        packet.WriteInt16(id);
        packet.WriteByte(level);
        packet.WriteSkip(stab16);
        packet.WriteInt16(ownerChar.Asda2Strength);
        packet.WriteInt16(ownerChar.Asda2Agility);
        packet.WriteInt16(ownerChar.Asda2Stamina);
        packet.WriteInt16(ownerChar.Asda2Spirit);
        packet.WriteInt16(ownerChar.Asda2Intellect);
        packet.WriteInt16(ownerChar.Asda2Luck);
        packet.WriteInt16(0);
        packet.WriteInt16(0);
        packet.WriteInt16(0);
        packet.WriteInt16(0);
        packet.WriteInt16(0);
        packet.WriteInt16(0);
        packet.WriteInt16(ownerChar.Asda2Strength);
        packet.WriteInt16(ownerChar.Asda2Agility);
        packet.WriteInt16(ownerChar.Asda2Stamina);
        packet.WriteInt16(ownerChar.Asda2Spirit);
        packet.WriteInt16(ownerChar.Asda2Intellect);
        packet.WriteInt16(ownerChar.Asda2Luck);
        packet.WriteInt32(ownerChar.MaxHealth);
        packet.WriteInt16(ownerChar.MaxPower);
        packet.WriteInt32(ownerChar.Health);
        packet.WriteInt16(ownerChar.Power);
        packet.WriteInt16((short) ownerChar.MinDamage);
        packet.WriteInt16((short) ownerChar.MaxDamage);
        packet.WriteInt16(ownerChar.MinMagicDamage);
        packet.WriteInt16(ownerChar.MaxMagicDamage);
        packet.WriteInt16((short) ownerChar.Asda2MagicDefence);
        packet.WriteInt16((short) ownerChar.Asda2Defence);
        packet.WriteInt16((short) ownerChar.Asda2Defence);
        packet.WriteFloat(ownerChar.BlockChance);
        packet.WriteFloat(ownerChar.BlockValue);
        packet.WriteInt16(15);
        packet.WriteInt16(7);
        packet.WriteInt16(4);
        packet.WriteSkip(stub87);
        ownerChar.Send(packet, false);
      }
    }

    public static void SendSkillLearnedFirstTimeResponse(IRealmClient client, short skillId, int cooldownSecs)
    {
      using(RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.SkillLearnedFirstTime))
      {
        packet.WriteInt16(skillId);
        packet.WriteByte(1);
        packet.WriteByte(1);
        packet.WriteInt16(cooldownSecs);
        packet.WriteSkip(stab12);
        packet.WriteInt16(271);
        packet.WriteInt32(28);
        packet.WriteByte(100);
        packet.WriteByte(100);
        packet.WriteInt16(8);
        packet.WriteSkip(stab24);
        client.Send(packet, true);
      }
    }

        //[PacketHandler((RealmServerOpCode) 5430)]
        //public static void U5330(IRealmClient client, RealmPacketIn packet)
        //{
        //}

        [PacketHandler(RealmServerOpCode.ClientIdleTick, IsGamePacket = false, RequiresLogin = false)]
        public static void ClientIdleTickRequest(IRealmClient client, RealmPacketIn packet)
        {
            // One-byte client heartbeat sent after world visibility is initialized.
        }

        [PacketHandler(RealmServerOpCode.ClientLoadVerification, IsGamePacket = false, RequiresLogin = false)]
        public static void ClientLoadVerificationRequest(IRealmClient client, RealmPacketIn packet)
        {
            // Sent during game-server handoff before character initialization; payload is client verification data.
        }

        [PacketHandler(RealmServerOpCode.ClientPreLocationVerification, IsGamePacket = false, RequiresLogin = false)]
        public static void ClientPreLocationVerificationRequest(IRealmClient client, RealmPacketIn packet)
        {
            // Sent by some client builds during game-server handoff before LocationInit.
        }

        //[PacketHandler((RealmServerOpCode) 5045)]
        //public static void U5045(IRealmClient client, RealmPacketIn packet)
        //{
        //}

        //[PacketHandler(RealmServerOpCode.MSG_TABARDVENDOR_ACTIVATE | RealmServerOpCode.SMSG_LFG_TELEPORT_DENIED)]
        //public static void U1010(IRealmClient client, RealmPacketIn packet)
        //{
        //}

        //[PacketHandler(RealmServerOpCode.SkillLearnedFirstTime | RealmServerOpCode.CMSG_LEARN_SPELL)]
        //public static void U6072(IRealmClient client, RealmPacketIn packet)
        //{
        //}

        //[PacketHandler(RealmServerOpCode.CharacterSoulMateIntrodactionUpdate |
        //               RealmServerOpCode.CMSG_QUERY_OBJECT_POSITION)]
        //public static void U6084(IRealmClient client, RealmPacketIn packet)
        //{
        //}

        [PacketHandler(RealmServerOpCode.DailyAttendanceRequest)]
        public static void U6059(IRealmClient client, RealmPacketIn packet)
        {
            DailyAttendanceRecord dailyAttendanceRecord = DailyAttendanceRecord.GetRecordByID(client.ActiveCharacter.EntityId.Low);
            if(dailyAttendanceRecord != null)
            {
                DailyAttendanceResponse(client, 2);
            }
            else
            {
                Asda2Item item = null;
                if(client.ActiveCharacter.Asda2Inventory.TryAdd(33628, 1, false, ref item, Asda2InventoryType.Regular) == Asda2InventoryError.Ok)
                {
                    client.ActiveCharacter.Asda2Inventory.AddDonateItem(Asda2ItemMgr.GetTemplate(33628), 1, "Daily Attendance", true);
                    var record = DailyAttendanceRecord.CreateRecord(client.ActiveCharacter.EntityId.Low);
                    record.Save();
                    DailyAttendanceResponse(client, 1);
                }
                else
                {
                    DailyAttendanceResponse(client, 3);
                }
            }
        }

        public static void DailyAttendanceResponse(IRealmClient client, byte status)
        {
            using (RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.DailyAttendanceResponse))
            {

                packet.WriteInt16(client.ActiveCharacter.SessionId);
                packet.WriteByte(status); // 1 - 13

                client.Send(packet, true);
            }
        }

        //[PacketHandler(RealmServerOpCode.WarEndedOne | RealmServerOpCode.CMSG_LEARN_SPELL)]
        //public static void U6749(IRealmClient client, RealmPacketIn packet)
        //{
        //}

        //[PacketHandler(RealmServerOpCode.WarEndedOne | RealmServerOpCode.CMSG_LEARN_SPELL)]
        //public static void U6749(IRealmClient client, RealmPacketIn packet)
        //{
        //}

        //[PacketHandler(RealmServerOpCode.EatApple | RealmServerOpCode.CMSG_LEARN_SPELL)]
        //public static void U6591(IRealmClient client, RealmPacketIn packet)
        //{
        //}

        [PacketHandler((RealmServerOpCode)6581)]
        public static void U6581(IRealmClient client, RealmPacketIn packet)
        {
            SpellCast cast = client.ActiveCharacter.SpellCast;
            Spell spell = client.ActiveCharacter.Spells.GetSpellByRealId(533);
            var reason = cast.Start(spell, true, client.ActiveCharacter);
        }

        [PacketHandler((RealmServerOpCode)6577)]
        public static void GachaMachineRequest(IRealmClient client, RealmPacketIn packet)
        {
            Character chr = client.ActiveCharacter;
            Asda2Item gacha = chr.Asda2Inventory.GetShopItemById(656);
            if (gacha != null)
            {
                if (gacha.Amount >= 4)
                {
                    gacha.Amount -= 4;
                    Asda2Item reward = new Asda2Item();
                    Asda2InventoryError tryadd = chr.Asda2Inventory.TryAdd(46411, 1, false, ref reward);
                    if (tryadd == Asda2InventoryError.NoSpace)
                    {
                        SendGachaMachineResponse(client, 5, gacha, reward);
                    }
                    else
                    {
                        SendGachaMachineResponse(client, 1, gacha, reward);
                    }
                }
                else
                {
                    SendGachaMachineResponse(client, 4, null, null);
                }
            }
            else
            {
                SendGachaMachineResponse(client, 3, null, null);
            }
        }

        public static void SendGachaMachineResponse(IRealmClient client, byte status, Asda2Item gacha, Asda2Item reward)
        {
            using (RealmPacketOut packet = new RealmPacketOut((RealmServerOpCode)6578))
            {
                packet.WriteByte(status);
                packet.WriteInt32(client.ActiveCharacter.AccId);
                packet.WriteInt16(client.ActiveCharacter.SessionId);
                packet.WriteInt16(client.ActiveCharacter.Asda2Inventory.Weight);
                Asda2InventoryHandler.WriteItemInfoToPacket(packet, gacha);
                Asda2InventoryHandler.WriteItemInfoToPacket(packet, reward);
                client.Send(packet);
            }
        }

        //[PacketHandler((RealmServerOpCode) 5474)]
        //public static void U5474(IRealmClient client, RealmPacketIn packet)
        //{
        //}

        [PacketHandler((RealmServerOpCode)6506)]
        public static void UpgradeResetRequest(IRealmClient client, RealmPacketIn packet)
        {
            packet.Position += 5;
            short itemCell = packet.ReadInt16();
            short scrollId = packet.ReadInt16();

            Character chr = client.ActiveCharacter;
            Asda2Item item = chr.Asda2Inventory.GetShopShopItem(itemCell);
            Asda2Item scroll = chr.Asda2Inventory.GetShopItemById(scrollId);

            Dictionary<int, int> enchantModMap = new Dictionary<int, int> { { 10, 8 }, { 11, 7 }, { 12, 7 }, { 13, 6 }, { 14, 6 }, { 15, 5 }, { 16, 5 }, { 17, 4 }, { 18, 4 }, { 19, 3 }, { 20, 3 } };

            if (enchantModMap.TryGetValue(item.Enchant, out int modAmount))
            {
                scroll.Amount -= modAmount;
            }
            item.Enchant = (byte)Math.Max(0, item.Enchant - 13);
            item.Record.EnchantResetCount++;
            item.Save();

            chr.SubtractMoney(1000000);
            chr.SendMoneyUpdate();

            UpgradeResetResponse(client, 1, item, scroll);
        }

        public static void UpgradeResetResponse(IRealmClient client, byte status, Asda2Item item, Asda2Item scroll)
        {
            using (RealmPacketOut packet = new RealmPacketOut((RealmServerOpCode)6507))
            {
                packet.WriteByte(status); // 1 - 13
                packet.WriteInt32(client.ActiveCharacter.Asda2Inventory.Weight);
                packet.WriteInt32(client.ActiveCharacter.Money);
                Asda2InventoryHandler.WriteItemInfoToPacket(packet, item, false);
                Asda2InventoryHandler.WriteItemInfoToPacket(packet, scroll, false);
                client.Send(packet, true);
            }
        }
        
        public static void TestPackets(IRealmClient client, int code)
        {
            using (RealmPacketOut packet = new RealmPacketOut((RealmServerOpCode)code))
            {
                packet.WriteByte(1); // 1 - 13
                packet.WriteInt32(client.ActiveCharacter.Asda2Inventory.Weight);
                packet.WriteInt32(client.ActiveCharacter.Money);

                client.Send(packet, true);
            }
        }
    }
}
