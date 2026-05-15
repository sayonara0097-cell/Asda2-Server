using System;
using WCell.Constants.World;
using WCell.Core.Timers;
using WCell.RealmServer.Entities;
using WCell.RealmServer.Global;
using WCell.Util.Graphics;

namespace WCell.RealmServer.Spawns
{
  [Serializable]
  public abstract class SpawnPoint<T, E, O, POINT, POOL> : ISpawnPoint
    where T : SpawnPoolTemplate<T, E, O, POINT, POOL>
    where E : SpawnEntry<T, E, O, POINT, POOL>
    where O : WorldObject
    where POINT : SpawnPoint<T, E, O, POINT, POOL>, new()
    where POOL : SpawnPool<T, E, O, POINT, POOL>
  {
    protected E m_spawnEntry;
    protected internal TimerEntry m_timer;
    protected int m_nextRespawn;
    protected O m_spawnling;
    protected bool m_spawnPending;

    internal void InitPoint(POOL pool, E entry)
    {
      m_timer = new TimerEntry(SpawnNow);
      Pool = pool;
      m_spawnEntry = entry;
    }

    public POOL Pool { get; protected set; }

    public E SpawnEntry
    {
      get { return m_spawnEntry; }
    }

    /// <summary>The currently active NPC of this SpawnPoint (or null)</summary>
    public O ActiveSpawnling
    {
      get { return m_spawnling; }
    }

    public Map Map
    {
      get { return Pool.Map; }
    }

    public MapId MapId
    {
      get { return Map.Id; }
    }

    public Vector3 Position
    {
      get { return m_spawnEntry.Position; }
    }

    public uint Phase
    {
      get { return m_spawnEntry.Phase; }
    }

    public bool HasSpawned
    {
      get { return m_spawnling != null; }
    }

    /// <summary>
    /// Whether timer is running and will spawn a new NPC when the timer elapses
    /// </summary>
    public bool IsSpawning
    {
      get { return m_timer.IsRunning || m_spawnPending; }
    }

    /// <summary>Pool active, but spawn inactive and npc autospawns</summary>
    public bool IsReadyToSpawn
    {
      get
      {
        if(Pool.IsActive && !IsActive && m_spawnEntry.AutoSpawns)
          return WorldEventMgr.IsEventActive(m_spawnEntry.EventId);
        return false;
      }
    }

    /// <summary>Whether NPC is already spawned or timer is running</summary>
    public bool IsActive
    {
      get
      {
        if(!HasSpawned)
          return IsSpawning;
        return true;
      }
    }

    public void Respawn()
    {
      if(Map.IsInContext)
      {
        RespawnNow();
        return;
      }
      Map.AddMessage(RespawnNow);
    }

    private void RespawnNow()
    {
      StopTimer();
      O spawnling = m_spawnling;
      m_spawnling = default(O);
      m_spawnPending = false;
      if(spawnling != null)
      {
        Pool.SpawnedObjects.Remove(spawnling);
        if(!spawnling.IsDeleted)
          spawnling.DeleteNow();
      }
      SpawnNow();
    }

    private void SpawnNow(int dt)
    {
      SpawnNow(true);
    }

    public void SpawnNow()
    {
      SpawnNow(false);
    }

    private void SpawnNow(bool fromTimer)
    {
      if(m_spawnling != null || m_spawnPending || (!fromTimer && m_timer.IsRunning))
        return;
      m_spawnPending = true;
      if(Map.IsInContext && !Map.IsUpdating)
      {
        SpawnNowInContext();
        return;
      }
      Map.AddMessage(SpawnNowInContext);
    }

    private void SpawnNowInContext()
    {
      try
      {
        if(m_spawnling == null)
          SpawnEntry.SpawnObject((POINT) this);
      }
      finally
      {
        m_spawnPending = false;
        Map.UnregisterUpdatable(m_timer);
      }
    }

    public void SpawnLater()
    {
      SpawnAfter(m_spawnEntry.GetRandomRespawnMillis());
    }

    /// <summary>Restarts the spawn timer with the given delay</summary>
    public void SpawnAfter(int delay)
    {
      if(!Pool.IsActive || m_timer.IsRunning || m_spawnPending || m_spawnling != null)
        return;
      m_nextRespawn = Environment.TickCount + delay;
      m_timer.Start(delay);
      Map.RegisterUpdatableLater(m_timer);
    }

    /// <summary>Stops the Respawn timer, if it was running</summary>
    public void StopTimer()
    {
      m_timer.Stop();
      Map.UnregisterUpdatableLater(m_timer);
    }

    public void RemoveSpawnedObject()
    {
      O spawnling = m_spawnling;
      if(spawnling == null)
        return;
      spawnling.Delete();
    }

    /// <summary>Stops timer and deletes spawnling</summary>
    public void Disable()
    {
      StopTimer();
      RemoveSpawnedObject();
    }

    /// <summary>Called when object enters map</summary>
    protected internal void SignalSpawnlingActivated(O obj)
    {
      m_spawnPending = false;
      if(m_spawnling != null && !ReferenceEquals(m_spawnling, obj))
      {
        obj.Delete();
        return;
      }
      m_spawnling = obj;
      if(!Pool.SpawnedObjects.Contains(m_spawnling))
        Pool.SpawnedObjects.Add(m_spawnling);
    }

    /// <summary>
    /// Is called when the given spawn died or was removed from Map.
    /// </summary>
    protected internal virtual void SignalSpawnlingDied(O obj)
    {
      if(m_spawnling != null && !ReferenceEquals(m_spawnling, obj))
      {
        Pool.SpawnedObjects.Remove(obj);
        return;
      }
      m_spawnling = default(O);
      Pool.SpawnedObjects.Remove(obj);
      if(!Pool.IsActive || !m_spawnEntry.AutoSpawns)
        return;
      Pool.SpawnOneLater();
    }
  }
}
