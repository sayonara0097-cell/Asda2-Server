using WCell.Constants.Spells;
using WCell.Constants.Updates;
using WCell.RealmServer.Entities;
using WCell.RealmServer.Handlers;
using WCell.RealmServer.Misc;
using WCell.Util.Graphics;

namespace WCell.RealmServer.Spells.Effects
{
  public class ChargeEffectHandler : SpellEffectHandler
  {
    private const float ChargeStopPadding = 0.7f;
    private const float ChargeMoveSpeed = 5f;

    public ChargeEffectHandler(SpellCast cast, SpellEffect effect)
      : base(cast, effect)
    {
    }

    public override SpellFailedReason Initialize()
    {
      return SpellFailedReason.Ok;
    }

    protected override void Apply(WorldObject target, ref DamageAction[] actions)
    {
      Character caster = Cast.CasterChar;
      Unit targetUnit = target as Unit;
      if(caster == null || targetUnit == null || !IsChargeMovementSpell(Effect.Spell.Id))
        return;

      Vector3 chargeStart = caster.Asda2Position;
      Vector3 chargeEnd = chargeStart;
      float chargeDistance = (float) (caster.Position.GetDistance(target.Position) -
                                      targetUnit.BoundingRadius - ChargeStopPadding);
      if(chargeDistance > 0f)
      {
        Vector3 direction = target.Position - caster.Position;
        direction.Normalize();
        caster.Position = caster.Position + direction * chargeDistance;
        chargeEnd = caster.Asda2Position;
      }

      caster.LastNewPosition = chargeEnd;
      caster.CurrentMovingVector = Vector2.Zero;
      caster.IsMoving = false;
      caster.SetOrientationTowards(targetUnit);
      caster.HasPendingChargeAnimationPath = false;

      NPC npcTarget = targetUnit as NPC;
      Asda2MovmentHandler.SendStartMoveCommonToAreaResponse(caster, true,
        npcTarget == null ? -1 : npcTarget.UniqIdOnMap, chargeStart, chargeEnd, ChargeMoveSpeed);
    }

    private static bool IsChargeMovementSpell(uint spellId)
    {
      switch((SpellId) spellId)
      {
        case SpellId.Charge283Rank1FromCharge:
        case SpellId.Charge283Rank2FromCharge:
        case SpellId.Charge283Rank3FromCharge:
        case SpellId.Charge283Rank1FromFuriousCharge:
          return true;
        default:
          return false;
      }
    }

    public override ObjectTypes TargetType
    {
      get { return ObjectTypes.Unit; }
    }

    public override ObjectTypes CasterType
    {
      get { return ObjectTypes.Unit; }
    }
  }
}
