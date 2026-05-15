using System;
using System.Collections.Generic;
using WCell.RealmServer.Entities;
using WCell.RealmServer.Global;
using WCell.RealmServer.Spawns;

namespace WCell.RealmServer.NPCs.Spawns
{
  [Serializable]
  public class NPCSpawnPool : SpawnPool<NPCSpawnPoolTemplate, NPCSpawnEntry, NPC, NPCSpawnPoint, NPCSpawnPool>
  {
    private readonly List<NPC> m_spawnedObjects = new List<NPC>();

    public NPCSpawnPool(Map map, NPCSpawnPoolTemplate templ)
      : base(map, templ)
    {
    }

    public override IList<NPC> SpawnedObjects
    {
      get { return m_spawnedObjects; }
    }
  }
}
