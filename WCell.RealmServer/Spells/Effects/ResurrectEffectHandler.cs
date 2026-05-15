using WCell.Constants.Updates;
using WCell.RealmServer.Entities;
using WCell.RealmServer.Handlers;
using WCell.RealmServer.Misc;

namespace WCell.RealmServer.Spells.Effects
{
  public class ResurrectEffectHandler : SpellEffectHandler
  {
    public ResurrectEffectHandler(SpellCast cast, SpellEffect effect)
      : base(cast, effect)
    {
    }

    protected override void Apply(WorldObject target, ref DamageAction[] actions)
    {
      Character character = target as Character;
      if(character == null || !character.IsDead)
        return;
      Asda2CharacterHandler.SendResurrectOptionsResponse(character.Client, Cast.CasterChar, Asda2CharacterHandler.ResurrectOptions.HelpOfSpellFrom);
      character.GainXp(character.LastExpLooseAmount * Effect.MiscValue, "resurect_spell", false);
    }

    public override ObjectTypes TargetType
    {
      get { return ObjectTypes.Unit; }
    }
  }
}