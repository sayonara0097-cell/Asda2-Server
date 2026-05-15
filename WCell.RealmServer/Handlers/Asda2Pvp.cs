using System;
using WCell.RealmServer.Entities;

namespace WCell.RealmServer.Handlers
{
  public class Asda2Pvp
  {
    private const int StartCountdownMillis = 10000;
    private const int FinishAfterLethalHitDelayMillis = 800;
    private const int LethalHitHealthUpdateDelayMillis = 50;
    private const int LoserZeroHealthPreviewDurationMillis = 2000;
    public static int PvpTimeSecs = 300;
    private Character _losser;

    public bool IsActive { get; set; }

    public bool IsFightStarted { get; private set; }

    public Character FirstCharacter { get; set; }

    public Character SecondCharacter { get; set; }

    public Character HealthAdjustedCharacter { get; set; }

    public bool IsFinishScheduled { get; set; }

    public Character Losser
    {
      get { return _losser; }
      set
      {
        _losser = value;
        StopPvp();
      }
    }

    public Character Winner
    {
      get
      {
        if(FirstCharacter != Losser)
          return FirstCharacter;
        return SecondCharacter;
      }
    }

    public int PvpTimeOuted { get; set; }

    public Asda2Pvp(Character firstCharacter, Character secondCharacter)
    {
      firstCharacter.Asda2Duel = this;
      secondCharacter.Asda2Duel = this;
      firstCharacter.Asda2DuelingOponent = secondCharacter;
      secondCharacter.Asda2DuelingOponent = firstCharacter;
      PvpTimeOuted = Environment.TickCount + PvpTimeSecs * 1000;
      IsActive = true;
      FirstCharacter = firstCharacter;
      SecondCharacter = secondCharacter;
      Asda2PvpHandler.SendPvpStartedResponse(Asda2PvpResponseStatus.Accept, firstCharacter,
        secondCharacter);
      Asda2PvpHandler.SendPvpStartedResponse(Asda2PvpResponseStatus.Accept, secondCharacter,
        firstCharacter);
      Asda2PvpHandler.SendPvpRoundEffectResponse(firstCharacter, secondCharacter);
      firstCharacter.Map.CallDelayed(StartCountdownMillis, StartPvp);
    }

    public void StartPvp()
    {
      if(!IsActive)
        return;
      IsFightStarted = true;
      FirstCharacter.EnemyCharacters.Add(SecondCharacter);
      SecondCharacter.EnemyCharacters.Add(FirstCharacter);
      GlobalHandler.SendFightingModeChangedResponse(FirstCharacter.Client, FirstCharacter.SessionId,
        (int) FirstCharacter.AccId, SecondCharacter.SessionId);
      GlobalHandler.SendFightingModeChangedResponse(SecondCharacter.Client, SecondCharacter.SessionId,
        (int) SecondCharacter.AccId, FirstCharacter.SessionId);
      UpdatePvp();
    }

    public void StopPvp()
    {
      if(!IsActive)
        return;
      IsActive = false;
      IsFightStarted = false;
      if(_losser == null)
        _losser = FirstCharacter;
      ReduceDuelistHealth(Losser);
      RestoreDuelistHealth(Winner);
      FirstCharacter.EnemyCharacters.Remove(SecondCharacter);
      SecondCharacter.EnemyCharacters.Remove(FirstCharacter);
      FirstCharacter.CheckEnemysCount();
      SecondCharacter.CheckEnemysCount();
      Asda2PvpHandler.SendDuelEndedResponse(Winner, Losser);
      FirstCharacter.Asda2Duel = null;
      SecondCharacter.Asda2Duel = null;
      FirstCharacter.Asda2DuelingOponent = null;
      SecondCharacter.Asda2DuelingOponent = null;
      FirstCharacter = null;
      SecondCharacter = null;
    }

    public void FinishAfterPreventedDeath(Character loser)
    {
      if(!IsActive || IsFinishScheduled || loser == null)
        return;
      _losser = loser;
      HealthAdjustedCharacter = loser;
      IsFinishScheduled = true;
      IsFightStarted = false;
      SetDuelistHealthToHalfMax(loser);
      SendLoserDefeatHealthPreview(loser);
      if(loser.Map == null)
      {
        StopPvp();
        return;
      }

      loser.Map.CallDelayed(FinishAfterLethalHitDelayMillis, () =>
      {
        if(IsActive)
          StopPvp();
      });
    }

    private void ReduceDuelistHealth(Character character)
    {
      if(character == null || character == HealthAdjustedCharacter || !character.IsAlive)
        return;
      SetDuelistHealthToHalfMax(character);
      SendLoserDefeatHealthPreview(character);
    }

    private static void SetDuelistHealthToHalfMax(Character character)
    {
      if(character == null)
        return;
      character.Health = Math.Max(1, character.MaxHealth / 2);
    }

    private static void SendLoserDefeatHealthPreview(Character character)
    {
      if(character == null || character.Map == null)
        return;

      character.Map.CallDelayed(LethalHitHealthUpdateDelayMillis,
        () => Asda2CharacterHandler.SendHealthUpdate(character, 0, false, false));
      character.Map.CallDelayed(
        LethalHitHealthUpdateDelayMillis + LoserZeroHealthPreviewDurationMillis,
        () => Asda2CharacterHandler.SendHealthUpdate(character, false, false));
    }

    private static void RestoreDuelistHealth(Character character)
    {
      if(character == null || !character.IsAlive)
        return;
      character.Health = character.MaxHealth;
      Asda2CharacterHandler.SendHealthUpdate(character, false, false);
    }

    public void UpdatePvp()
    {
      if(PvpTimeOuted < Environment.TickCount || FirstCharacter == null ||
         (SecondCharacter == null || FirstCharacter.Map == null) || SecondCharacter.Map == null)
        StopPvp();
      else
        FirstCharacter.Map.CallDelayed(3000, UpdatePvp);
    }
  }
}
