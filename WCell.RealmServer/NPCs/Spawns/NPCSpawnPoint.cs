using System;
using WCell.Core.Paths;
using WCell.RealmServer.Entities;
using WCell.RealmServer.Spawns;

namespace WCell.RealmServer.NPCs.Spawns
{
  [Serializable]
  public class NPCSpawnPoint : SpawnPoint<NPCSpawnPoolTemplate, NPCSpawnEntry, NPC, NPCSpawnPoint, NPCSpawnPool>,
    IWorldLocation, IHasPosition
  {
    protected internal override void SignalSpawnlingDied(NPC obj)
    {
      if(m_spawnling != null && !ReferenceEquals(m_spawnling, obj))
      {
        Pool.SpawnedObjects.Remove(obj);
        return;
      }
      m_spawnling = null;
      Pool.SpawnedObjects.Remove(obj);
      if(!Pool.IsActive || !m_spawnEntry.AutoSpawns)
        return;
      SpawnLater();
    }
  }
}
