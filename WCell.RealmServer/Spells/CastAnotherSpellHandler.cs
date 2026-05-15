using WCell.Constants.Spells;
using WCell.RealmServer.Entities;
using WCell.RealmServer.Misc;
using WCell.Util;
using WCell.Util.Graphics;

namespace WCell.RealmServer.Spells
{
  internal class CastAnotherSpellHandler : SpellEffectHandler
  {
    private bool m_grandEarthquakeSelfTriggersApplied;

    public CastAnotherSpellHandler(SpellCast cast, SpellEffect effect)
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

      Unit triggerTarget = GetTriggerTarget(null);
      if(triggerTarget == null)
        return;
      RunTriggerSpells(triggerTarget);
    }

    protected override void Apply(WorldObject target, ref DamageAction[] actions)
    {
      Unit triggerTarget = GetTriggerTarget(target);
      if(triggerTarget == null)
        return;
      RunTriggerSpells(triggerTarget);
    }

    private void RunTriggerSpells(Unit triggerTarget)
    {
      if(Effect.MiscValue != 0)
        RunSpell(triggerTarget, (uint) Effect.MiscValue);
      if(Effect.MiscValueB != 0)
        RunSpell(triggerTarget, (uint) Effect.MiscValueB);
      if(Effect.MiscValueC == 0)
        return;
      RunSpell(triggerTarget, (uint) Effect.MiscValueC);
    }

    private Unit GetTriggerTarget(WorldObject target)
    {
      if(!IsGrandEarthquake)
        return target as Unit;
      if(m_grandEarthquakeSelfTriggersApplied)
        return null;
      m_grandEarthquakeSelfTriggersApplied = true;
      return Cast == null ? null : Cast.CasterUnit;
    }

    private bool IsGrandEarthquake
    {
      get
      {
        return Cast != null && Cast.Spell != null &&
               Cast.Spell.RealId == (short) SpellLineId.GrandEarthquake;
      }
    }

    private void RunSpell(Unit target, uint spellId)
    {
      if(target == null)
        return;
      if(Utility.Random(0, 101) > Cast.Spell.ProcChance)
        return;
      Spell spell = SpellHandler.Get(spellId);
      if(spell == null)
        return;
      Vector3 position = target.Position;
      if(IsLastResortPetrifySpell(spellId))
      {
        TriggerLastResortPetrify(target, spell, ref position);
        return;
      }

      SpellCast.Trigger(m_cast.CasterUnit, spell, ref position, target);
    }

    private bool IsLastResortPetrifySpell(uint spellId)
    {
      return Cast != null && Cast.Spell != null &&
             Cast.Spell.RealId == (short) SpellLineId.LastResort &&
             spellId == Spell.LastResortPetrifySpellId;
    }

    private void TriggerLastResortPetrify(Unit target, Spell spell, ref Vector3 position)
    {
      int oldDuration = spell.Duration;
      var oldDurations = spell.Durations;
      try
      {
        spell.Duration = Spell.LastResortPetrifyDurationMillis;
        spell.SetDuration(Spell.LastResortPetrifyDurationMillis);
        SpellCast.Trigger(m_cast.CasterUnit, spell, ref position, target);
      }
      finally
      {
        spell.Duration = oldDuration;
        spell.Durations = oldDurations;
      }
    }
  }
}
