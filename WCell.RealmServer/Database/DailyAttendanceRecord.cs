using Castle.ActiveRecord;
using NHibernate.Criterion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WCell.Constants.Spells;
using WCell.Core;
using WCell.Core.Database;
using WCell.RealmServer.Items;

namespace WCell.RealmServer.Database
{
    [ActiveRecord("dailyattendancerecord", Access = PropertyAccess.Property)]
    public class DailyAttendanceRecord : WCellRecord<DailyAttendanceRecord>
    {
        [PrimaryKey(PrimaryKeyType.Assigned)]
        public uint CharLowId { get; set; }

        public DailyAttendanceRecord(uint charLowId)
        {
            CharLowId = charLowId;
        }

        public DailyAttendanceRecord() {}

        public static DailyAttendanceRecord GetRecordByID(uint id)
        {
            return FindOne((ICriterion)Restrictions.Eq("CharLowId", id));
        }

        internal static DailyAttendanceRecord CreateRecord()
        {
            try
            {
                DailyAttendanceRecord daillyAttendanceRecord = new DailyAttendanceRecord();
                daillyAttendanceRecord.State = RecordState.New;
                return daillyAttendanceRecord;
            }
            catch (Exception ex)
            {
                throw new WCellException(ex, "Unable to create new ItemRecord.");
            }
        }

        public static DailyAttendanceRecord CreateRecord(uint id)
        {
            DailyAttendanceRecord record = CreateRecord();
            record.CharLowId = id;
            return record;
        }
    }
}
