using System;
using WCell.Constants.Spells;
using WCell.Constants.Updates;
using WCell.RealmServer.Entities;
using WCell.RealmServer.Handlers;
using WCell.RealmServer.Misc;
using WCell.Util;

namespace WCell.RealmServer.Spells
{
  internal class DamageFromPrcAtackHandler : SpellEffectHandler
  {
    private const uint SupportMageEarthSoulGuardSpellId = 2254U;
    private const int SupportMageEarthSoulGuardVisibleHits = 2;
    private const int GrandEarthquakeVisibleHits = 2;
    private const int MaxSkillStrikeActionsPerPacket = 16;

    public DamageFromPrcAtackHandler(SpellCast cast, SpellEffect effect)
      : base(cast, effect)
    {
    }

    public override void Apply()
    {
      if(!IsGrandEarthquake)
      {
        base.Apply();
        return;
      }

      ApplyGrandEarthquakeDamage();
    }

    protected override void Apply(WorldObject target, ref DamageAction[] actions)
    {
      try
      {
        if(Cast == null || Cast.CasterUnit == null || target == null)
          return;
        int length = GetVisibleHitCount();
        if(length == 0 && Effect.MiscValue > 0)
          length = 1;
        DamageAction[] values = new DamageAction[length];
        int damage = CalcPrcBoostDamageValue();
        if(IsSupportMageEarthSoulGuardDoubleHit && length > 1)
          damage = Math.Max(1, damage / length);
        for(int index = 0; index < length; ++index)
        {
          values[index] = ((Unit) target).DealSpellDamage(m_cast.CasterUnit, Effect,
            damage, true, true, false, false);
          if(Effect.Spell.RealId == 709)
          {
            float hp = MathUtil.ClampMinMax(values[index].ActualDamage * 0.7f, 0.0f,
              Cast.CasterUnit.MaxHealth * 0.3f);
            Cast.CasterUnit.Map.CallDelayed((int) Effect.Spell.CastDelay + 200,
              () => Cast.CasterUnit.Heal((int) hp, null, null));
          }
        }

        if(actions == null)
          actions = values;
        else
          ArrayUtil.Concat(ref actions, values);
      }
      catch(NullReferenceException)
      {
      }
    }

    private void ApplyGrandEarthquakeDamage()
    {
      if(Cast == null || Cast.CasterUnit == null || Cast.Map == null)
        return;

      DamageAction[] actions = null;
      float radius = GetRadius();
      if(radius <= 0.0f)
        radius = Effect.Radius;
      if(radius <= 0.0f)
        radius = 9f;

      Cast.Map.IterateObjects(Cast.CasterUnit.Position, radius, Cast.Phase, obj =>
      {
        Unit target = obj as Unit;
        if(!CanHitGrandEarthquakeTarget(target))
          return true;

        SendGrandEarthquakeTargetAnimation(target);
        Apply(target, ref actions);
        return true;
      });

      if(actions == null || actions.Length == 0)
        return;
      SendGrandEarthquakeDamage(actions);
    }

    private void SendGrandEarthquakeTargetAnimation(Unit target)
    {
      if(Cast.CasterChar == null || target == null)
        return;

      for(int index = 0; index < GrandEarthquakeVisibleHits; ++index)
        Asda2SpellHandler.SendAnimateSkillStrikeResponse(Cast.CasterChar, Effect.Spell.RealId, null, target);
    }

    private void SendGrandEarthquakeDamage(DamageAction[] actions)
    {
      for(int index = 0; index < actions.Length; index += MaxSkillStrikeActionsPerPacket)
      {
        int length = Math.Min(MaxSkillStrikeActionsPerPacket, actions.Length - index);
        DamageAction[] batch = new DamageAction[length];
        Array.Copy(actions, index, batch, 0, length);
        Unit firstTarget = GetFirstActionTarget(batch);
        if(firstTarget == null)
          continue;

        SendGrandEarthquakeDamageBatch(batch, firstTarget);
      }
    }

    private void SendGrandEarthquakeDamageBatch(DamageAction[] actions, Unit firstTarget)
    {
      if(Cast.CasterChar != null)
        Asda2SpellHandler.SendAnimateSkillStrikeResponse(Cast.CasterChar, Effect.Spell.RealId, actions,
          firstTarget);
      else if(Cast.CasterObject is NPC casterNpc)
        Asda2SpellHandler.SendMonstrUsedSkillResponse(casterNpc, Effect.Spell.RealId, firstTarget, actions);
    }

    private static Unit GetFirstActionTarget(DamageAction[] actions)
    {
      if(actions == null)
        return null;
      for(int index = 0; index < actions.Length; ++index)
      {
        if(actions[index] != null && actions[index].Victim != null)
          return actions[index].Victim;
      }

      return null;
    }

    private bool CanHitGrandEarthquakeTarget(Unit target)
    {
      if(target == null || target == Cast.CasterUnit || !target.IsInContext || !target.IsAlive)
        return false;
      if(Cast.CasterObject == null || !Cast.CasterObject.MayAttack(target) || !target.CanBeHarmed)
        return false;
      Character casterChar = Cast.CasterChar;
      NPC targetNpc = target as NPC;
      if(casterChar != null && targetNpc != null && targetNpc.Channel != casterChar.Client.Channel)
        return false;
      return true;
    }

    private int GetVisibleHitCount()
    {
      if(IsGrandEarthquake)
        return GrandEarthquakeVisibleHits;
      if(IsSupportMageEarthSoulGuardDoubleHit)
        return SupportMageEarthSoulGuardVisibleHits;
      return Effect.MiscValueB;
    }

    private bool IsGrandEarthquake
    {
      get
      {
        return Effect != null && Effect.Spell != null &&
               Effect.Spell.RealId == (short) SpellLineId.GrandEarthquake;
      }
    }

    private bool IsSupportMageEarthSoulGuardDoubleHit
    {
      get { return Effect != null && Effect.Spell != null && Effect.Spell.Id == SupportMageEarthSoulGuardSpellId; }
    }

    private int CalcPrcBoostDamageValue()
    {
      float num1 = Effect.MiscValue / 100f;
      int num2 = 0;
      int num3;
      if(m_cast.CasterUnit is NPC)
      {
        num3 = (int) m_cast.CasterUnit.MainWeapon.Damages[0].Minimum;
      }
      else
      {
        num2 = m_cast.CasterChar.GetRandomMagicDamage();
        num3 = (int) m_cast.CasterChar.GetRandomPhysicalDamage();
      }

      if(num2 > num3)
        return (int) (num2 * (0.05 + num1));
      return (int) (num3 * (0.05 + num1));
    }

    public override ObjectTypes TargetType
    {
      get { return ObjectTypes.Unit; }
    }
  }
}
