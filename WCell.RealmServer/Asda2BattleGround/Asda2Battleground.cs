using Cell.Core;
using NHibernate.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Threading.Tasks;
using WCell.Constants;
using WCell.Constants.Achievements;
using WCell.Constants.Factions;
using WCell.Constants.World;
using WCell.Core;
using WCell.Core.Network;
using WCell.Core.Timers;
using WCell.RealmServer.Achievements;
using WCell.RealmServer.Asda2_Items;
using WCell.RealmServer.Battlegrounds;
using WCell.RealmServer.Database;
using WCell.RealmServer.Entities;
using WCell.RealmServer.Factions;
using WCell.RealmServer.Formulas;
using WCell.RealmServer.Global;
using WCell.RealmServer.Handlers;
using WCell.RealmServer.Logs;
using WCell.RealmServer.Modifiers;
using WCell.RealmServer.Network;
using WCell.Util.Graphics;

namespace WCell.RealmServer.Asda2BattleGround
{
    public class Asda2Battleground : IUpdatable
    {
        public List<Asda2WarPoint> Points = new List<Asda2WarPoint>(7);
        public WorldLocation LightStartLocation = new WorldLocation(MapId.BatleField, new Vector3(19254f, 19107f), 1U);
        public WorldLocation DarkStartLocation = new WorldLocation(MapId.BatleField, new Vector3(19255f, 19397f), 1U);
        public Dictionary<byte, Character> LightTeam = new Dictionary<byte, Character>();
        public Dictionary<byte, Character> LightTeamWaiting = new Dictionary<byte, Character>();
        public Dictionary<byte, Character> DarkTeam = new Dictionary<byte, Character>();
        public Dictionary<byte, Character> DarkTeamWaiting = new Dictionary<byte, Character>();
        public Dictionary<byte, Character> WaitingList = new Dictionary<byte, Character>();
        public List<byte> FreeLightIds = new List<byte>();
        public List<byte> FreeDarkIds = new List<byte>();
        public List<string> DissmisedCharacterNames = new List<string>();
        public readonly object JoinLock = new object();
        private int _notificationsAboutStart = 3;
        private short? _forcedWiningFactionId;
        private bool _lightTeamEnteredThisWar;
        private bool _darkTeamEnteredThisWar;
        public List<Character> DissmissYes = new List<Character>();
        public List<Character> DissmissNo = new List<Character>();
        public bool IsDismissInProgress;

        public bool IsStarted { get; set; }

        public byte CurrentWarDurationMins
        {
            get
            {
                if (this.WarType != Asda2BattlegroundType.Occupation)
                    return Asda2BattlegroundMgr.DeathMatchDurationMins;
                return Asda2BattlegroundMgr.OccupationDurationMins;
            }
        }

        public short LightWins
        {
            get { return (short)Asda2BattlegroundMgr.LightWins[(int)this.Town]; }
        }

        public short LightLooses
        {
            get { return (short)Asda2BattlegroundMgr.DarkWins[(int)this.Town]; }
        }

        public short DarkWins
        {
            get { return (short)Asda2BattlegroundMgr.DarkWins[(int)this.Town]; }
        }

        public short DarkLooses
        {
            get { return (short)Asda2BattlegroundMgr.LightWins[(int)this.Town]; }
        }

        public Asda2BattlegroundTown Town { get; set; }

        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public int LightScores { get; set; }

        public int DarkScores { get; set; }

        public byte MinEntryLevel { get; set; }

        public byte MaxEntryLevel { get; set; }

        public Character MvpCharacter { get; set; }

        public byte WarNotofocationStep { get; set; }

        public byte AmountOfBattleGroundsInList
        {
            get { return (byte)Asda2BattlegroundMgr.AllBattleGrounds[this.Town].Count; }
        }

        public Asda2BattlegroundType WarType { get; set; }

        public bool IsRunning { get; set; }

        public Asda2Battleground()
        {
            for (byte index = 0; index < byte.MaxValue; ++index)
            {
                this.FreeDarkIds.Add(index);
                this.FreeLightIds.Add(index);
            }
        }

        private bool TryGetFactionLists(Character chr, out Dictionary<byte, Character> team,
            out Dictionary<byte, Character> waitingTeam, out List<byte> freeIds)
        {
            team = null;
            waitingTeam = null;
            freeIds = null;

            if (chr == null)
                return false;

            if (chr.Asda2FactionId == (short)0)
            {
                team = this.LightTeam;
                waitingTeam = this.LightTeamWaiting;
                freeIds = this.FreeLightIds;
                return true;
            }

            if (chr.Asda2FactionId == (short)1)
            {
                team = this.DarkTeam;
                waitingTeam = this.DarkTeamWaiting;
                freeIds = this.FreeDarkIds;
                return true;
            }

            return false;
        }

        private bool TryGetNextWaitingListId(out byte freeId)
        {
            for (int index = 0; index < byte.MaxValue; ++index)
            {
                byte candidate = (byte)index;
                if (!this.WaitingList.ContainsKey(candidate))
                {
                    freeId = candidate;
                    return true;
                }
            }

            freeId = 0;
            return false;
        }

        private static bool TryTakeFreeTeamId(Dictionary<byte, Character> team,
            Dictionary<byte, Character> waitingTeam, List<byte> freeIds, out byte freeId)
        {
            freeIds.Sort();
            for (int index = 0; index < freeIds.Count; ++index)
            {
                byte candidate = freeIds[index];
                if (team.ContainsKey(candidate) || waitingTeam.ContainsKey(candidate))
                    continue;

                freeIds.RemoveAt(index);
                freeId = candidate;
                return true;
            }

            for (int index = 0; index < byte.MaxValue; ++index)
            {
                byte candidate = (byte)index;
                if (!team.ContainsKey(candidate) && !waitingTeam.ContainsKey(candidate))
                {
                    freeId = candidate;
                    return true;
                }
            }

            freeId = 0;
            return false;
        }

        private static bool TryGetCharacterKey(Dictionary<byte, Character> list, Character chr, out byte key)
        {
            foreach (KeyValuePair<byte, Character> entry in list)
            {
                if (entry.Value == chr)
                {
                    key = entry.Key;
                    return true;
                }
            }

            key = 0;
            return false;
        }

        private static List<byte> GetCharacterKeys(Dictionary<byte, Character> list, Character chr)
        {
            return list.Where(entry => entry.Value == chr).Select(entry => entry.Key).ToList();
        }

        private static void ReleaseTeamId(List<byte> freeIds, byte id, Dictionary<byte, Character> team,
            Dictionary<byte, Character> waitingTeam)
        {
            if (team.ContainsKey(id) || waitingTeam.ContainsKey(id) || freeIds.Contains(id))
                return;

            freeIds.Add(id);
            freeIds.Sort();
        }

        private bool RemoveWaitingListEntries(Character chr)
        {
            bool removed = false;
            byte waitingKey;

            while (TryGetCharacterKey(this.WaitingList, chr, out waitingKey))
            {
                this.ReassignWaitingList(waitingKey);
                removed = true;
            }

            return removed;
        }

        private static bool RemoveWaitingTeamEntries(Dictionary<byte, Character> waitingTeam, Character chr,
            Dictionary<byte, Character> team, List<byte> freeIds)
        {
            bool removed = false;
            foreach (byte waitingKey in GetCharacterKeys(waitingTeam, chr))
            {
                waitingTeam.Remove(waitingKey);
                ReleaseTeamId(freeIds, waitingKey, team, waitingTeam);
                removed = true;
            }

            return removed;
        }

        private bool LeaveTeam(Character chr, Dictionary<byte, Character> team,
            Dictionary<byte, Character> waitingTeam, List<byte> freeIds, out byte leftBattleGroundId)
        {
            if (!TryGetCharacterKey(team, chr, out leftBattleGroundId))
                return false;

            team.Remove(leftBattleGroundId);

            if (leftBattleGroundId == 0 && team.Count > 0)
            {
                KeyValuePair<byte, Character> promotedEntry = team.OrderBy(entry => entry.Key).First();
                Character promotedCharacter = promotedEntry.Value;
                byte promotedOldId = promotedEntry.Key;

                if (promotedOldId != 0)
                {
                    team.Remove(promotedOldId);
                    team[0] = promotedCharacter;
                    promotedCharacter.CurrentBattleGroundId = 0;
                    ReleaseTeamId(freeIds, promotedOldId, team, waitingTeam);

                    Asda2BattlegroundHandler.SendTransferWarLeadershipResponse(promotedCharacter.Client, 1, chr);
                    Asda2BattlegroundHandler.SendTransferWarLeadershipResponse(chr.Client, 1, promotedCharacter);
                    Asda2BattlegroundHandler.SendWarTeamListResponse(promotedCharacter);
                    Asda2BattlegroundHandler.SendWarTeamListResponse(chr);
                }
            }

            ReleaseTeamId(freeIds, leftBattleGroundId, team, waitingTeam);
            RemoveWaitingTeamEntries(waitingTeam, chr, team, freeIds);
            this.RemoveWaitingListEntries(chr);
            return true;
        }

        public int JoinList(Character chr, out int key, out int freeId)
        {
            lock (this.JoinLock)
            {
                key = -1;
                freeId = -1;

                Dictionary<byte, Character> team;
                Dictionary<byte, Character> waitingTeam;
                List<byte> freeIds;
                if (!this.TryGetFactionLists(chr, out team, out waitingTeam, out freeIds))
                    return -1;

                byte existingTeamId;
                if (TryGetCharacterKey(team, chr, out existingTeamId))
                {
                    chr.CurrentBattleGround = this;
                    chr.CurrentBattleGroundId = existingTeamId;
                    key = existingTeamId;
                    return key;
                }

                byte existingWaitingTeamId;
                if (TryGetCharacterKey(waitingTeam, chr, out existingWaitingTeamId))
                {
                    byte existingWaitingListId;
                    if (TryGetCharacterKey(this.WaitingList, chr, out existingWaitingListId))
                    {
                        freeId = existingWaitingListId;
                    }
                    else if (this.TryGetNextWaitingListId(out existingWaitingListId))
                    {
                        this.WaitingList[existingWaitingListId] = chr;
                        freeId = existingWaitingListId;
                    }
                    else
                    {
                        return -1;
                    }

                    key = existingWaitingTeamId;
                    return key;
                }

                byte waitingListId;
                byte teamId;
                if (!this.TryGetNextWaitingListId(out waitingListId) ||
                    !TryTakeFreeTeamId(team, waitingTeam, freeIds, out teamId))
                    return -1;

                waitingTeam[teamId] = chr;
                this.WaitingList[waitingListId] = chr;
                key = teamId;
                freeId = waitingListId;

                if (this.IsRunning)
                    Asda2BattlegroundHandler.SendYouCanEnterWarResponse(chr.Client);

                return key;
            }
        }

        public void ReassignWaitingList(byte key)
        {
            if (!WaitingList.ContainsKey(key))
                return;

            WaitingList.Remove(key);
            var affectedEntries = WaitingList
                .Where(e => e.Key > key)
                .OrderBy(e => e.Key)
                .ToList();

            foreach (var entry in affectedEntries)
            {
                WaitingList.Remove(entry.Key);
                byte newKey = (byte)(entry.Key - 1);
                WaitingList[newKey] = entry.Value;
                Asda2BattlegroundHandler.SendYouCanEnterWarAfterResponse(entry.Value.Client, newKey);
            }
        }

        public bool Join(Character chr)
        {
            lock (this.JoinLock)
            {
                Dictionary<byte, Character> team;
                Dictionary<byte, Character> waitingTeam;
                List<byte> freeIds;
                if (!this.TryGetFactionLists(chr, out team, out waitingTeam, out freeIds))
                    return false;

                byte activeWarId;
                if (TryGetCharacterKey(team, chr, out activeWarId))
                {
                    chr.CurrentBattleGround = this;
                    chr.CurrentBattleGroundId = activeWarId;
                    RemoveWaitingTeamEntries(waitingTeam, chr, team, freeIds);
                    this.RemoveWaitingListEntries(chr);
                    return true;
                }

                byte WarId;
                if (!TryGetCharacterKey(waitingTeam, chr, out WarId))
                    return false;

                Character existingCharacter;
                if (team.TryGetValue(WarId, out existingCharacter) && existingCharacter != chr &&
                    !TryTakeFreeTeamId(team, waitingTeam, freeIds, out WarId))
                    return false;

                chr.BattlegroundActPoints = (short)0;
                chr.BattlegroundKills = 0;
                chr.BattlegroundDeathes = 0;
                chr.CurrentBattleGround = this;
                chr.CurrentBattleGroundId = WarId;
                team[WarId] = chr;
                chr.LocatonBeforeOnEnterWar = new WorldLocation(chr.Map, chr.Position, 1U);
                RemoveWaitingTeamEntries(waitingTeam, chr, team, freeIds);
                this.RemoveWaitingListEntries(chr);
                return true;
            }
        }

        public bool LeaveList(Character chr)
        {
            lock (this.JoinLock)
            {
                bool removed = this.RemoveWaitingListEntries(chr);

                Dictionary<byte, Character> team;
                Dictionary<byte, Character> waitingTeam;
                List<byte> freeIds;
                if (this.TryGetFactionLists(chr, out team, out waitingTeam, out freeIds))
                    removed = RemoveWaitingTeamEntries(waitingTeam, chr, team, freeIds) || removed;

                return removed;
            }
        }

        public bool Leave(Character chr)
        {
            lock (this.JoinLock)
            {
                Dictionary<byte, Character> team;
                Dictionary<byte, Character> waitingTeam;
                List<byte> freeIds;
                if (!this.TryGetFactionLists(chr, out team, out waitingTeam, out freeIds))
                    return false;

                byte leftBattleGroundId;
                if (!this.LeaveTeam(chr, team, waitingTeam, freeIds, out leftBattleGroundId))
                    return false;

                Action sendLeaveUpdate = () =>
                {
                    Asda2BattlegroundHandler.SendHowManyPeopleInWarTeamsResponse(this, (Character)null);
                    Asda2BattlegroundHandler.SendCharacterHasLeftWarResponse(this, 0, (int)chr.AccId,
                        leftBattleGroundId, chr.Name, (int)chr.Asda2FactionId);
                };
                Map map = chr.Map;
                if (map != null)
                    map.CallDelayed(1, sendLeaveUpdate);
                else
                    sendLeaveUpdate();
                chr.IsChangingAsda2BattleGroundMap = false;
                chr.CurrentBattleGround = (Asda2Battleground)null;
                if (chr.CurrentCapturingPoint != null)
                    chr.CurrentCapturingPoint.StopCapture();
                if (chr.Client != null)
                    GlobalHandler.SendFightingModeChangedResponse(chr.Client, chr.SessionId, (int)chr.AccId, (short)-1);
                if (chr.MapId == MapId.BatleField)
                    chr.TeleportTo((IWorldLocation)chr.LocatonBeforeOnEnterWar ?? chr.BindLocation);
                if (chr.IsStunned)
                    --chr.Stunned;
                chr.CurrentBattleGroundId = 0;
                this.StopIfOneTeamLeft();

                return true;
            }
        }

        public void Update(int dt)
        {
            switch (this._notificationsAboutStart)
            {
                case 1:
                    if (DateTime.Now > this.StartTime.Subtract(new TimeSpan(0, 5, 0)))
                    {
                        --this._notificationsAboutStart;
                        WCell.RealmServer.Global.World.BroadcastMsg("War Manager",
                            string.Format("{1} in {0} starts in 5 mins.", (object)this.Town, (object)this.WarType),
                            Color.Firebrick);
                        Asda2BattlegroundHandler.SendMessageServerAboutWarStartsResponse((byte)5);
                        break;
                    }

                    break;
                case 2:
                    if (DateTime.Now > this.StartTime.Subtract(new TimeSpan(0, 15, 0)))
                    {
                        --this._notificationsAboutStart;
                        WCell.RealmServer.Global.World.BroadcastMsg("War Manager",
                            string.Format("{1} in {0} starts in 15 mins.", (object)this.Town, (object)this.WarType),
                            Color.Firebrick);
                        Asda2BattlegroundHandler.SendMessageServerAboutWarStartsResponse((byte)15);
                        break;
                    }

                    break;
                case 3:
                    if (DateTime.Now > this.StartTime.Subtract(new TimeSpan(0, 30, 0)))
                    {
                        --this._notificationsAboutStart;
                        WCell.RealmServer.Global.World.BroadcastMsg("War Manager",
                            string.Format("{1} in {0} starts in 30 mins.", (object)this.Town, (object)this.WarType),
                            Color.Firebrick);
                        Asda2BattlegroundHandler.SendMessageServerAboutWarStartsResponse((byte)30);
                        break;
                    }

                    break;
            }

            if (DateTime.Now > this.EndTime && this.IsRunning)
            {
                this.Stop();
            }
            else
            {
                if (!(DateTime.Now > this.StartTime) || !(DateTime.Now < this.EndTime))
                    return;
                this.Start();
            }
        }

        public int WiningFactionId
        {
            get
            {
                if (this._forcedWiningFactionId.HasValue)
                    return this._forcedWiningFactionId.Value;
                if (this.LightScores == this.DarkScores)
                    return 2;
                return this.LightScores <= this.DarkScores ? 1 : 0;
            }
        }

        private void StopIfOneTeamLeft()
        {
            if (!this.IsRunning || !this.HaveBothTeamsEnteredThisWar)
                return;
            if (this.LightTeam.Count > 0 && this.DarkTeam.Count == 0)
            {
                this.StopWithForcedWinner((short)0);
            }
            else if (this.DarkTeam.Count > 0 && this.LightTeam.Count == 0)
            {
                this.StopWithForcedWinner((short)1);
            }
        }

        private void StopWithForcedWinner(short factionId)
        {
            this._forcedWiningFactionId = factionId;
            if (factionId == (short)0 && this.LightScores <= this.DarkScores)
                this.LightScores = this.DarkScores + 1;
            else if (factionId == (short)1 && this.DarkScores <= this.LightScores)
                this.DarkScores = this.LightScores + 1;

            this.Stop();
        }

        private bool HaveBothTeamsEnteredThisWar
        {
            get { return this._lightTeamEnteredThisWar && this._darkTeamEnteredThisWar; }
        }

        internal void MarkTeamEnteredThisWar(short factionId)
        {
            if (!this.IsRunning)
                return;
            if (factionId == (short)0)
                this._lightTeamEnteredThisWar = true;
            else if (factionId == (short)1)
                this._darkTeamEnteredThisWar = true;
        }

        public long CurrentWarResultRecordGuid { get; set; }

        public void Stop()
        {
            if (!this.IsRunning)
                return;
            this._notificationsAboutStart = 3;
            this.IsStarted = false;
            WCell.RealmServer.Global.World.Broadcast(string.Format(
                "War in {0} has ended. Light scores {1} vs {2} dark scores.", (object)this.Town,
                (object)this.LightScores, (object)this.DarkScores));
            this.IsRunning = false;
            this.SetNextWarParametrs();
            lock (this.JoinLock)
            {
                foreach (Character character in this.LightScores > this.DarkScores
                    ? this.LightTeam.Values
                    : this.DarkTeam.Values)
                {
                    if (this.MvpCharacter == null)
                        this.MvpCharacter = character;
                    else if ((int)this.MvpCharacter.BattlegroundActPoints < (int)character.BattlegroundActPoints)
                        this.MvpCharacter = character;
                }

                Asda2BattlegroundHandler.SendWiningFactionInfoResponse(this.Town, this.WiningFactionId,
                    this.MvpCharacter == null ? "[No character]" : this.MvpCharacter.Name);
                if (this.MvpCharacter != null)
                    ServerApp<WCell.RealmServer.RealmServer>.IOQueue.AddMessage((Action)(() =>
                    {
                        BattlegroundResultRecord battlegroundResultRecord = new BattlegroundResultRecord(this.Town,
                            this.MvpCharacter.Name, this.MvpCharacter.EntityId.Low, this.LightScores, this.DarkScores);
                        battlegroundResultRecord.CreateLater();
                        this.CurrentWarResultRecordGuid = battlegroundResultRecord.Guid;
                        Asda2BattlegroundMgr.ProcessBattlegroundResultRecord(battlegroundResultRecord);
                    }));
                foreach (Character character in this.LightTeam.Values)
                    this.ProcessEndWar(character);
                foreach (Character character in this.DarkTeam.Values)
                    this.ProcessEndWar(character);
                foreach (Asda2WarPoint point in this.Points)
                {
                    point.Status = Asda2WarPointStatus.NotOwned;
                    point.OwnedFaction = (short)-1;
                    Asda2BattlegroundHandler.SendWarPointStateResponse(point);
                }

                WCell.RealmServer.Global.World.TaskQueue.CallDelayed(60000, new Action(this.KickAll));
            }
        }

        private void SetNextWarParametrs()
        {
            Asda2BattlegroundType type;
            this.StartTime = Asda2BattlegroundMgr.GetNextWarTime(this.Town, out type, DateTime.Now);
            this.WarType = type;
            this.EndTime = this.StartTime.AddMinutes(this.WarType == Asda2BattlegroundType.Occupation
                ? (double)Asda2BattlegroundMgr.OccupationDurationMins
                : (double)Asda2BattlegroundMgr.DeathMatchDurationMins);
        }

        private void ProcessEndWar(Character character)
        {
            if (character == null)
                return;
            IRealmClient client = character.Client;
            ++character.Stunned;
            if (client != null)
                GlobalHandler.SendFightingModeChangedResponse(client, character.SessionId, (int)character.AccId,
                    (short)-1);
            if (this.MvpCharacter != null)
                ServerApp<WCell.RealmServer.RealmServer>.IOQueue.AddMessage((Action)(() =>
                    new BattlegroundCharacterResultRecord(this.CurrentWarResultRecordGuid, character.Name,
                        character.EntityId.Low, (int)character.BattlegroundActPoints, character.BattlegroundKills,
                        character.BattlegroundDeathes).CreateLater()));
            int honorPoints = this.WiningFactionId == 2
                ? 0
                : CharacterFormulas.CalcHonorPoints(character.Level, character.BattlegroundActPoints,
                    this.LightScores > this.DarkScores, character.BattlegroundDeathes, character.BattlegroundKills,
                    this.MvpCharacter == character, this.Town);
            short honorCoins = this.WiningFactionId == 2
                ? (short)0
                : (short)((double)honorPoints / (double)CharacterFormulas.HonorCoinsDivider);
            if (character.BattlegroundActPoints < (short)5)
                character.BattlegroundActPoints = (short)5;
            if (honorPoints <= 0)
                honorPoints = 1;
            if (honorCoins <= (short)0)
                honorCoins = (short)1;
            Asda2Item asda2Item = (Asda2Item)null;
            if (honorCoins > (short)0 && character.Asda2Inventory != null)
            {
                int num = (int)character.Asda2Inventory.TryAdd(20614, (int)honorCoins, true, ref asda2Item,
                    new Asda2InventoryType?(), (Asda2Item)null);
                Log.Create(Log.Types.ItemOperations, LogSourceType.Character, character.EntryId)
                    .AddAttribute("source", 0.0, "honor_coins_for_bg").AddItemAttributes(asda2Item, "")
                    .AddAttribute("amount", (double)honorCoins, "").Write();
            }

            int bonusExp = this.WiningFactionId == 2
                ? 0
                : (int)((double)XpGenerator.GetBaseExpForLevel(character.Level) *
                         (double)character.BattlegroundActPoints / 2.5);
            character.GainXp(bonusExp, "battle_ground", false);
            character.Asda2HonorPoints += honorPoints;
            AchievementProgressRecord progressRecord = character.Achievements.GetOrCreateProgressRecord(20U);
            if (character.FactionId == (FactionId)this.WiningFactionId)
            {
                switch (++progressRecord.Counter)
                {
                    case 5:
                        character.DiscoverTitle(Asda2TitleId.Challenger125);
                        break;
                    case 10:
                        character.GetTitle(Asda2TitleId.Challenger125);
                        break;
                    case 25:
                        character.DiscoverTitle(Asda2TitleId.Winner126);
                        break;
                    case 50:
                        character.GetTitle(Asda2TitleId.Winner126);
                        break;
                    case 75:
                        character.DiscoverTitle(Asda2TitleId.Champion127);
                        break;
                    case 100:
                        character.GetTitle(Asda2TitleId.Champion127);
                        break;
                    case 250:
                        character.DiscoverTitle(Asda2TitleId.Conqueror128);
                        break;
                    case 500:
                        character.GetTitle(Asda2TitleId.Conqueror128);
                        break;
                }

                progressRecord.SaveAndFlush();
            }
            character.MaxHealth /= 2;
            character.Resurrect();
            Map map = character.Map;
            if (client != null && map != null)
            {
                map.CallDelayed(500,
                    (Action)(() => Asda2BattlegroundHandler.SendWarEndedResponse(client,
                        (byte)this.WiningFactionId,
                        this.LightScores > this.DarkScores ? this.LightScores : this.DarkScores,
                        this.LightScores > this.DarkScores ? this.DarkScores : this.LightScores, honorPoints, honorCoins,
                        (long)bonusExp, this.MvpCharacter == null ? "" : this.MvpCharacter.Name)));
                Asda2BattlegroundHandler.SendWarEndedOneResponse(client,
                    (IEnumerable<Asda2Item>)new List<Asda2Item>()
                    {
                        asda2Item
                    });
                character.SendWarMsg("You will automaticly teleported to town in 1 minute.");
            }
        }

        public void KickAll()
        {
            lock (this.JoinLock)
            {
                List<Character> characterList = new List<Character>();
                characterList.AddRange((IEnumerable<Character>)this.LightTeam.Values);
                characterList.AddRange((IEnumerable<Character>)this.DarkTeam.Values);
                foreach (Character chr in characterList)
                    this.Leave(chr);

            }
        }

        public void Start()
        {
            if (this.IsRunning)
                return;
            this.StartTime = DateTime.Now;
            this.EndTime = DateTime.Now.AddMinutes(this.CurrentWarDurationMins);
            if (this.DarkTeam.Count < 0)
            {
                WCell.RealmServer.Global.World.Broadcast(string.Format("War terminated due not enough players in {0}.",
                    (object)this.Town));
                this.SetNextWarParametrs();
            }
            else
            {
                WCell.RealmServer.Global.World.Broadcast(string.Format("War started in {0}. Availible lvls {1}-{2}.",
                    (object)this.Town, (object)this.MinEntryLevel, (object)this.MaxEntryLevel));
                foreach (Asda2WarPoint point in this.Points)
                {
                    point.Status = Asda2WarPointStatus.NotOwned;
                    point.OwnedFaction = (short)-1;
                }

                this.DissmisedCharacterNames.Clear();
                this.IsRunning = true;
                this.LightScores = 0;
                this.DarkScores = 0;
                this.LightBuffed = false;
                this.DarkBuffed = false;
                this.MvpCharacter = (Character)null;
                this.WarNotofocationStep = (byte)0;
                this._forcedWiningFactionId = null;
                this._lightTeamEnteredThisWar = false;
                this._darkTeamEnteredThisWar = false;
                lock (this.JoinLock)
                {
                    foreach (Character character in this.LightTeamWaiting.Values)
                        Asda2BattlegroundHandler.SendYouCanEnterWarResponse(character.Client);
                    foreach (Character character in this.DarkTeamWaiting.Values)
                        Asda2BattlegroundHandler.SendYouCanEnterWarResponse(character.Client);
                }

                Asda2BattlegroundHandler.SendWarRemainingTimeResponse(this);
                WCell.RealmServer.Global.World.TaskQueue.CallDelayed(60000, new Action(this.SendWarTimeMotofocation));
            }
        }

        bool LightBuffed = false;
        bool DarkBuffed = false;

        private void CheckForTeamBuff()
        {
            foreach (var chr in LightTeam.Values)
            {
                chr.ChangeModifier(StatModifierFloat.Health, -15 / 100f);
                chr.ChangeModifier(StatModifierFloat.Damage, -15 / 100f);
                chr.ChangeModifier(StatModifierFloat.MagicDamage, -15 / 100f);
                chr.ChangeModifier(StatModifierFloat.Asda2Defence, -15 / 100f);
                chr.ChangeModifier(StatModifierFloat.Asda2MagicDefence, -15 / 100f);
            }
            if ((LightScores - DarkScores) >= 200 && !DarkBuffed)
            {
                foreach (var chr in DarkTeam.Values)
                {
                    chr.MaxHealth += (int)(chr.MaxHealth * 0.5);
                }
                DarkBuffed = true;
                Asda2BattlegroundHandler.SendWarCurrentActionInfoResponse(this, BattleGroundInfoMessageType.DarkWillReciveBuffs, 1);
            }
            else
            {
                if (DarkBuffed)
                {
                    foreach (var chr in DarkTeam.Values)
                    {
                        chr.MaxHealth -= (int)(chr.MaxHealth * 0.5);
                    }
                    DarkBuffed = false;
                    Asda2BattlegroundHandler.SendWarCurrentActionInfoResponse(this, BattleGroundInfoMessageType.DarkBuffsHasBeedRemoved, 1);
                }
            }

            if ((DarkScores - LightScores) >= 200 && !LightBuffed)
            {
                foreach (var chr in LightTeam.Values)
                {
                    chr.MaxHealth += (int)(chr.MaxHealth * 0.5);
                }
                LightBuffed = true;
                Asda2BattlegroundHandler.SendWarCurrentActionInfoResponse(this, BattleGroundInfoMessageType.DarkWillReciveBuffs, 0);
            }
            else
            {
                if (LightBuffed)
                {
                    foreach (var chr in LightTeam.Values)
                    {
                        chr.MaxHealth -= (int)(chr.MaxHealth * 0.5);
                    }
                    LightBuffed = false;
                    Asda2BattlegroundHandler.SendWarCurrentActionInfoResponse(this, BattleGroundInfoMessageType.DarkBuffsHasBeedRemoved, 0);
                }
            }
        }

        private void SendWarTimeMotofocation()
        {
            if (!this.IsRunning)
                return;
            Asda2BattlegroundHandler.SendWarRemainingTimeResponse(this);
            switch (this.WarNotofocationStep)
            {
                case 0:
                    Asda2BattlegroundHandler.SendWarCurrentActionInfoResponse(this,
                        BattleGroundInfoMessageType.WarStartsInNumMins, (short)1, (Character)null, new short?());
                    WCell.RealmServer.Global.World.TaskQueue.CallDelayed(60000,
                        new Action(this.SendWarTimeMotofocation));
                    break;
                case 1:
                    Asda2BattlegroundHandler.SendWarCurrentActionInfoResponse(this,
                        BattleGroundInfoMessageType.WarStarted, (short)0, (Character)null, new short?());
                    WCell.RealmServer.Global.World.TaskQueue.CallDelayed(300000,
                        new Action(this.SendWarTimeMotofocation));
                    this.IsStarted = true;
                    break;
                case 2:
                    CheckForTeamBuff();
                    WCell.RealmServer.Global.World.TaskQueue.CallDelayed(60000,
                        new Action(this.SendWarTimeMotofocation));
                    break;
                case 3:
                    CheckForTeamBuff();
                    WCell.RealmServer.Global.World.TaskQueue.CallDelayed(300000,
                        new Action(this.SendWarTimeMotofocation));
                    break;
                case 4:
                    CheckForTeamBuff();
                    WCell.RealmServer.Global.World.TaskQueue.CallDelayed(300000,
                        new Action(this.SendWarTimeMotofocation));
                    break;
                case 5:
                    CheckForTeamBuff();
                    WCell.RealmServer.Global.World.TaskQueue.CallDelayed(300000,
                        new Action(this.SendWarTimeMotofocation));
                    break;
                case 6:
                    Asda2BattlegroundHandler.SendWarCurrentActionInfoResponse(this,
                        BattleGroundInfoMessageType.WarEndsInNumMins, (short)5, (Character)null, new short?());
                    WCell.RealmServer.Global.World.TaskQueue.CallDelayed(60000,
                        new Action(this.SendWarTimeMotofocation));
                    break;
                case 7:
                    Asda2BattlegroundHandler.SendWarCurrentActionInfoResponse(this,
                        BattleGroundInfoMessageType.WarEndsInNumMins, (short)4, (Character)null, new short?());
                    WCell.RealmServer.Global.World.TaskQueue.CallDelayed(60000,
                        new Action(this.SendWarTimeMotofocation));
                    break;
                case 8:
                    Asda2BattlegroundHandler.SendWarCurrentActionInfoResponse(this,
                        BattleGroundInfoMessageType.WarEndsInNumMins, (short)3, (Character)null, new short?());
                    WCell.RealmServer.Global.World.TaskQueue.CallDelayed(60000,
                        new Action(this.SendWarTimeMotofocation));
                    break;
                case 9:
                    Asda2BattlegroundHandler.SendWarCurrentActionInfoResponse(this,
                        BattleGroundInfoMessageType.WarEndsInNumMins, (short)2, (Character)null, new short?());
                    WCell.RealmServer.Global.World.TaskQueue.CallDelayed(60000,
                        new Action(this.SendWarTimeMotofocation));
                    break;
                case 10:
                    Asda2BattlegroundHandler.SendWarCurrentActionInfoResponse(this,
                        BattleGroundInfoMessageType.WarEndsInNumMins, (short)1, (Character)null, new short?());
                    break;
            }

            ++this.WarNotofocationStep;
        }

        public void Send(RealmPacketOut packet, bool addEnd = false, short? asda2FactionId = null,
            Locale locale = Locale.Any)
        {
            lock (this.JoinLock)
            {
                short? nullable1 = asda2FactionId;
                if (!(nullable1.HasValue ? new int?((int)nullable1.GetValueOrDefault()) : new int?()).HasValue)
                {
                    foreach (Character character in this.DarkTeam.Values)
                    {
                        if (character.Client == null)
                            continue;
                        if (locale == Locale.Any || character.Client.Locale == locale)
                            character.Send(packet, addEnd);
                    }

                    foreach (Character character in this.LightTeam.Values)
                    {
                        if (character.Client == null)
                            continue;
                        if (locale == Locale.Any || character.Client.Locale == locale)
                            character.Send(packet, addEnd);
                    }
                }
                else
                {
                    short? nullable2 = asda2FactionId;
                    foreach (Character character in (nullable2.GetValueOrDefault() != (short)0
                                                        ? 0
                                                        : (nullable2.HasValue ? 1 : 0)) != 0
                        ? this.LightTeam.Values
                        : this.DarkTeam.Values)
                    {
                        if (character.Client == null)
                            continue;
                        if (locale == Locale.Any || character.Client.Locale == locale)
                            character.Send(packet, false);
                    }
                }
            }
        }

        public void TeleportToWar(Character activeCharacter, bool isResurrect = false)
        {
            activeCharacter.MaxHealth *= 2;
            activeCharacter.HealPercent(100);
            if (activeCharacter.Asda2FactionId == (short)0)
                activeCharacter.TeleportTo((IWorldLocation)this.LightStartLocation);
            else
                activeCharacter.TeleportTo((IWorldLocation)this.DarkStartLocation);

            if (!isResurrect)
            {
                Asda2BattlegroundHandler.SendHowManyPeopleInWarTeamsResponse(this);
                Asda2BattlegroundHandler.SendCharacterHasLeftWarResponse(this, 1, (int)activeCharacter.AccId, activeCharacter.CurrentBattleGroundId, activeCharacter.Name, activeCharacter.Asda2FactionId);
            }
        }

        public void GainScores(Character killer, short points)
        {
            this.GainScores(killer.Asda2FactionId, points);
        }

        public void GainScores(short factionId, short points)
        {
            if (factionId == (short)0)
                this.LightScores += (int)points;
            else
                this.DarkScores += (int)points;

            Asda2BattlegroundHandler.SendTeamPointsResponse(this, (Character)null);
        }

        byte[] unk81 = new byte[10]
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
        };



        public void SendCurrentProgress(Character character)
        {
            if (DateTime.Now < this.StartTime.AddMinutes(2.0))
                Asda2BattlegroundHandler.SendWarCurrentActionInfoResponse(this,
                    BattleGroundInfoMessageType.WarStartsInNumMins,
                    (short)(this.StartTime.AddMinutes(2.0) - DateTime.Now).TotalMinutes, character, new short?());
            else
                Asda2BattlegroundHandler.SendWarCurrentActionInfoResponse(this, BattleGroundInfoMessageType.WarStarted,
                    (short)0, character, new short?());
        }

        public Vector3 GetBasePosition(Character activeCharacter)
        {
            if (activeCharacter.Asda2FactionId != (short)0)
                return this.DarkStartLocation.Position;
            return this.LightStartLocation.Position;
        }

        public Vector3 GetForeigLocation(Character activeCharacter)
        {
            if (activeCharacter.Asda2FactionId != (short)0)
                return this.LightStartLocation.Position;
            return this.DarkStartLocation.Position;
        }

        public short DissmisFaction { get; set; }

        public Character DissmissingCharacter { get; set; }

        public DateTime DissmissTimeouted { get; set; }

        public void AnswerDismiss(bool kick, Character answerer)
        {
            lock (this)
            {
                if (!this.IsDismissInProgress || (int)answerer.Asda2FactionId != (int)this.DissmisFaction ||
                    answerer == this.DissmissingCharacter)
                    return;
                if (kick)
                {
                    if (this.DissmissYes.Contains(answerer))
                        return;
                    this.DissmissYes.Add(answerer);
                    if ((double)this.DissmissYes.Count <= (this.DissmisFaction == (short)0
                            ? (double)this.LightTeam.Count * 0.65
                            : (double)this.DarkTeam.Count * 0.65))
                        return;
                    Asda2BattlegroundHandler.SendDissmissResultResponse(this, DismissPlayerResult.Ok,
                        this.DissmissingCharacter.SessionId, (int)this.DissmissingCharacter.AccId);
                    this.Leave(this.DissmissingCharacter);
                    this.IsDismissInProgress = false;
                    this.DissmissingCharacter = (Character)null;
                }
                else
                {
                    if (this.DissmissNo.Contains(answerer))
                        return;
                    this.DissmissNo.Add(answerer);
                    if ((double)this.DissmissNo.Count <= (this.DissmisFaction == (short)0
                            ? (double)this.LightTeam.Count * 0.3
                            : (double)this.DarkTeam.Count * 0.3))
                        return;
                    Asda2BattlegroundHandler.SendDissmissResultResponse(this, DismissPlayerResult.Fail,
                        this.DissmissingCharacter.SessionId, (int)this.DissmissingCharacter.AccId);
                    this.IsDismissInProgress = false;
                    this.DissmissingCharacter = (Character)null;
                }
            }
        }

        public bool TryStartDissmisProgress(Character initer, Character dissmiser)
        {
            lock (this)
            {
                if (this.IsDismissInProgress)
                {
                    if (!(this.DissmissTimeouted < DateTime.Now))
                        return false;
                    Asda2BattlegroundHandler.SendDissmissResultResponse(this, DismissPlayerResult.Fail,
                        this.DissmissingCharacter.SessionId, (int)this.DissmissingCharacter.AccId);
                }

                this.IsDismissInProgress = true;
                Asda2BattlegroundHandler.SendQuestionDismissPlayerOrNotResponse(this, initer, dissmiser);
                this.DissmissingCharacter = dissmiser;
                this.DissmissYes.Clear();
                this.DissmissNo.Clear();
                this.DissmissTimeouted = DateTime.Now.AddMinutes(1.0);
                this.DissmisFaction = initer.Asda2FactionId;
                return true;
            }
        }

        public Character GetCharacter(short asda2FactionId, byte warId)
        {
            Character character;
            if (asda2FactionId == (byte)0)
                return this.LightTeam.TryGetValue(warId, out character) ? character : (Character)null;

            return this.DarkTeam.TryGetValue(warId, out character) ? character : (Character)null;
        }
    }
}
