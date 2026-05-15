using Castle.ActiveRecord;
using Cell.Core;
using NHibernate.Engine;
using NHibernate.Linq;
using NHibernate.Mapping;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Security.Cryptography;
using System.ServiceModel.Channels;
using WCell.Constants;
using WCell.Constants.Factions;
using WCell.Constants.Items;
using WCell.Constants.NPCs;
using WCell.Constants.Skills;
using WCell.Constants.Spells;
using WCell.Core;
using WCell.Core.Network;
using WCell.Intercommunication.DataTypes;
using WCell.RealmServer.Asda2BattleGround;
using WCell.RealmServer.Asda2Looting;
using WCell.RealmServer.Chat;
using WCell.RealmServer.Database;
using WCell.RealmServer.Entities;
using WCell.RealmServer.Factions;
using WCell.RealmServer.Global;
using WCell.RealmServer.Guilds;
using WCell.RealmServer.Handlers;
using WCell.RealmServer.Items;
using WCell.RealmServer.Misc;
using WCell.RealmServer.Network;
using WCell.RealmServer.NPCs;
using WCell.RealmServer.Social;
using WCell.RealmServer.Spells;
using WCell.RealmServer.Spells.Auras;
using WCell.Util;
using WCell.Util.Commands;
using WCell.Util.Graphics;
using WCell.Util.Threading;
using WCell.Util.Variables;

namespace WCell.RealmServer.Commands
{
    public class RatesCommand : RealmServerCommand
    {
        protected RatesCommand()
        {
        }

        public override RoleStatus RequiredStatusDefault
        {
            get { return RoleStatus.EventManager; }
        }

        protected override void Initialize()
        {
            Init("rates");
            EnglishParamInfo = "";
            EnglishDescription = "";
        }

        public override void Process(CmdTrigger<RealmServerCmdArgs> trigger)
        {

            float xpRateInput = trigger.Text.NextFloat(0);
            float moneyDropFactorInput = trigger.Text.NextFloat(0);
            int someIntParameter = trigger.Text.NextInt(0);

            uint xpRate;
            if (xpRateInput <= 0)
            {
                xpRate = 1;
            }
            else if (xpRateInput < 1.5)
            {
                xpRate = 1; 
            }
            else
            {
                xpRate = (uint)xpRateInput;
            }

            uint moneyDropFactor;
            if (moneyDropFactorInput <= 0)
            {
                moneyDropFactor = 1;
            }
            else if (moneyDropFactorInput < 2.5)
            {
                moneyDropFactor = 2;
            }
            else if (moneyDropFactorInput < 3.5)
            {
                moneyDropFactor = 3;
            }
            else
            {
                moneyDropFactor = 4;
            }

            RealmServerConfiguration instance = RealmServerConfiguration.Instance;
            instance.Set("XpRate", xpRate);
            instance.Set("DefaultMoneyDropFactor", moneyDropFactor);
            instance.Save();
            

            bool shouldSendPacket = xpRateInput > 0 || moneyDropFactorInput > 0;
            int goldValue = moneyDropFactorInput == 0 ? -1 : (int)(moneyDropFactor * 100);
            int xpValue = xpRateInput <= 0 ? -1 : (int)(xpRateInput * 100);


            List<Character> chrs = World.GetAllCharacters();

            chrs.ForEach((chr) =>
            {
                using (RealmPacketOut packet = new RealmPacketOut((RealmServerOpCode)4301))
                {
                    packet.WriteByte(shouldSendPacket);
                    packet.WriteInt32(goldValue);
                    packet.WriteInt32(xpValue);
                    chr.Send(packet, false);
                }
            });

        }
    }
}