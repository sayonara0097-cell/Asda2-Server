using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Threading.Tasks;
using WCell.Constants;
using WCell.Core.Network;
using WCell.RealmServer.Entities;

namespace WCell.RealmServer.Handlers
{
    public class Asda2SoulGuardHandler
    {

        public static void SendShowSoulGuard(Character chr, int soulid)
        {
            using (var packet = new RealmPacketOut(RealmServerOpCode.ShowSoulGuardResponse))
            {
                packet.WriteInt32(chr.AccId);
                packet.WriteInt16(soulid);
                chr.SendPacketToArea(packet, addEnd: true);
            }

        }

        public static void AddSoulGuardSpell(Character chr, int auraId)
        {
            using (RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.BuffsOnCharacterInfo))
            {
                packet.WriteInt16(chr.SessionId);
                packet.WriteInt16(0);
                packet.WriteInt32(auraId);
                packet.WriteInt32(-1);
                packet.WriteInt16(0);

                chr.SendPacketToArea(packet, true, true, Locale.Any, new float?());
            }
        }
        
        public static void RemoveSoulGuardSpell(Character chr, int auraId)
        {
            using (RealmPacketOut packet = new RealmPacketOut(RealmServerOpCode.BuffEnded))
            {
                packet.WriteInt16(chr.SessionId);
                packet.WriteInt16(auraId);
                chr.SendPacketToArea(packet, true, true, Locale.Any, new float?());
            }
        }
    }
}
