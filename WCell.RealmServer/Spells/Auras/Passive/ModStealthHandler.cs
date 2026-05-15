using WCell.RealmServer.Entities;

namespace WCell.RealmServer.Spells.Auras.Handlers
{
  /// <summary>
  /// Activates Stealth (renders caster invisible for others and makes him/her sneaky)
  /// </summary>
  public class ModStealthHandler : AuraEffectHandler
  {
    protected override void Apply()
    {
      m_aura.Auras.Owner.Stealthed += EffectValue;
    }

    protected override void Remove(bool cancelled)
    {
      m_aura.Auras.Owner.Stealthed -= EffectValue;
    }
  }
  public class ModDetectStealthHandler : AuraEffectHandler
  {
    protected override void Apply()
    {
      var chars = m_aura.Auras.Owner.GetNearbyCharacters(includeSelf: false);
        foreach (var chr in chars)
        {
            m_aura.Auras.Owner.HandleStealthDetection(chr);
        }
    }

    protected override void Remove(bool cancelled)
    {
      var chars = m_aura.Auras.Owner.GetNearbyCharacters(includeSelf: false);
        foreach (var chr in chars)
        {
            m_aura.Auras.Owner.HandleStealthDetection(chr);
        }
    }
  }
}