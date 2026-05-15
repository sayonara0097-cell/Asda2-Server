using Castle.ActiveRecord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WCell.Core.Database;

namespace WCell.RealmServer.Database
{
    [ActiveRecord("warresults", Access = PropertyAccess.Property)]
    public class WarResultRecord : WCellRecord<WarResultRecord>
    {
        [PrimaryKey(PrimaryKeyType.Assigned)]
        public int Id { get; set; }

        [Property(NotNull = true)]
        public int guildid { get; set; }

        [Property(NotNull = true)]
        public int tax { get; set; }

        [Property(NotNull = true)]
        public int mapid { get; set; }

        [Property(NotNull = true)]
        public int faction { get; set; }

    }
}
