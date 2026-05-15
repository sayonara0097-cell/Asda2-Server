using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WCell.Constants;
using WCell.RealmServer.Entities;
using WCell.RealmServer.Modifiers;
using WCell.RealmServer.Spells.Auras;

namespace WCell.RealmServer.Handlers
{
    public class Asda2SoulGuard
    {
        public static void CheckSoulGuard(Character chr)
        {
            if (chr == null)
                return;
            int soulid = chr.GetSoulId();
            if (soulid < 0)
            {
                ShowSoulGuardIfChanged(chr, -1);
                return;
            }

            if (chr.RedCharges >= 8)
            {
                ShowSoulGuardIfChanged(chr, soulid + 2);
                AddSoulSpell(chr, soulid + 2);
            }
            else if (chr.BlueCharges >= 8)
            {
                ShowSoulGuardIfChanged(chr, soulid + 1);
                AddSoulSpell(chr, soulid + 1);
                if(chr.RedCharges < 8)
                {
                    RemoveSoulSpell(chr, soulid + 2);
                }
            }
            else if (chr.GreenCharges >= 8)
            {
                ShowSoulGuardIfChanged(chr, soulid);
                AddSoulSpell(chr, soulid);
                if (chr.BlueCharges < 8)
                {
                    RemoveSoulSpell(chr, soulid + 1);
                }
            }
            else
            {
                ShowSoulGuardIfChanged(chr, -1);
                if (chr.GreenCharges < 8)
                {
                    RemoveSoulSpell(chr, soulid);
                }
            }

        }

        private static void ShowSoulGuardIfChanged(Character chr, int soulid)
        {
            if (chr.CurrentSoulGuardId == soulid)
                return;
            chr.CurrentSoulGuardId = soulid;
            Asda2SoulGuardHandler.SendShowSoulGuard(chr, soulid);
        }

        public static void AddSoulSpell(Character chr, int soulid)
        {
            
            switch (soulid)
            {
                case 0:
                    if(!chr.SoulBuffed1)
                    chr.ChangeModifier(StatModifierFloat.Damage, 15 / 100f);
                    chr.SoulBuffed1 = true;
                    Asda2SoulGuardHandler.AddSoulGuardSpell(chr, 237);
                    break;
                case 1:
                    if(!chr.SoulBuffed2)
                    chr.ChangeModifier(StatModifierFloat.Asda2Defence, 15 / 100f);
                    chr.SoulBuffed2 = true;
                    Asda2SoulGuardHandler.AddSoulGuardSpell(chr, 219);
                    break;
                 case 2:
                    if(!chr.SoulBuffed3)
                    chr.ChangeModifier(StatModifierFloat.Asda2Defence, 15 / 100f);
                    chr.SoulBuffed3 = true;
                    Asda2SoulGuardHandler.AddSoulGuardSpell(chr, 220);
                    break;
                case 3:
                    if(!chr.SoulBuffed1)
                    chr.ChangeModifier(StatModifierFloat.Damage, 15 / 100f);
                    chr.SoulBuffed1 = true;
                    Asda2SoulGuardHandler.AddSoulGuardSpell(chr, 224);
                    break;
                case 4:
                    if(!chr.SoulBuffed2)
                    chr.ChangeModifier(StatModifierFloat.Damage, 15 / 100f);
                    chr.SoulBuffed2 = true;
                    Asda2SoulGuardHandler.AddSoulGuardSpell(chr, 226);
                    break;
                case 5:
                    if(!chr.SoulBuffed3)
                    chr.ChangeModifier(StatModifierFloat.Asda2Defence, 15 / 100f);
                    chr.SoulBuffed3 = true;
                    Asda2SoulGuardHandler.AddSoulGuardSpell(chr, 220);
                    break;
                case 6:
                    if(!chr.SoulBuffed1)
                    chr.ChangeModifier(StatModifierFloat.Damage, 15 / 100f);
                    chr.SoulBuffed1 = true;
                    Asda2SoulGuardHandler.AddSoulGuardSpell(chr, 233);
                    break;
                case 7:
                    if(!chr.SoulBuffed2)
                    chr.ChangeModifier(StatModifierFloat.Asda2Defence, 15 / 100f);
                    chr.SoulBuffed2 = true;
                    Asda2SoulGuardHandler.AddSoulGuardSpell(chr, 219);
                    break;
                case 8:
                    if(!chr.SoulBuffed3)
                    chr.ChangeModifier(StatModifierFloat.Damage, 15 / 100f);
                    chr.SoulBuffed3 = true;
                    Asda2SoulGuardHandler.AddSoulGuardSpell(chr, 220);
                    break;
                case 9:
                    if (!chr.SoulBuffed1)
                    {
                    chr.ChangeModifier(StatModifierFloat.Damage, 15 / 100f);
                    chr.ChangeModifier(StatModifierFloat.Health, 15 / 100f);
                    }
                    chr.SoulBuffed1 = true;
                    Asda2SoulGuardHandler.AddSoulGuardSpell(chr, 237);
                    break;
                case 10:
                    if(!chr.SoulBuffed2)
                    chr.ChangeModifier(StatModifierFloat.Asda2Defence, 15 / 100f);
                    chr.SoulBuffed2 = true;
                    Asda2SoulGuardHandler.AddSoulGuardSpell(chr, 233);
                    break;
                case 11:
                    if(!chr.SoulBuffed3)
                    chr.ChangeModifier(StatModifierFloat.Damage, 15 / 100f);
                    chr.SoulBuffed3 = true;
                    Asda2SoulGuardHandler.AddSoulGuardSpell(chr, 238);
                    break;
                case 12:
                    if(!chr.SoulBuffed1)
                    chr.ChangeModifier(StatModifierFloat.Speed, 15 / 100f);
                    chr.SoulBuffed1 = true;
                    Asda2SoulGuardHandler.AddSoulGuardSpell(chr, 241);
                    break;
                case 13:
                    if(!chr.SoulBuffed2)
                    chr.ChangeModifier(StatModifierFloat.Damage, 15 / 100f);
                    chr.SoulBuffed2 = true;
                    Asda2SoulGuardHandler.AddSoulGuardSpell(chr, 242);
                    break;
                case 14:
                    if(!chr.SoulBuffed3)
                    chr.ChangeModifier(StatModifierFloat.Damage, 15 / 100f);
                    chr.SoulBuffed3 = true;
                    Asda2SoulGuardHandler.AddSoulGuardSpell(chr, 231);
                    break;
                case 15:
                    if(!chr.SoulBuffed1)
                    Asda2SoulGuardHandler.AddSoulGuardSpell(chr, 248);
                    chr.SoulBuffed1 = true;
                    break;
                case 16:
                    if(!chr.SoulBuffed2)
                    Asda2SoulGuardHandler.AddSoulGuardSpell(chr, 249);
                    chr.SoulBuffed2 = true;
                    break;
                case 17:
                    if(!chr.SoulBuffed3)
                    Asda2SoulGuardHandler.AddSoulGuardSpell(chr, 250);
                    chr.SoulBuffed3 = true;
                    break;
                case 18:
                    if(!chr.SoulBuffed1)
                    {
                        chr.ChangeModifier(StatModifierFloat.HealthRegen, 15 / 100f);
                        chr.SoulBuffed1 = true;
                        Asda2SoulGuardHandler.AddSoulGuardSpell(chr, 264);
                    }
                    break;
                case 19:
                    if (!chr.SoulBuffed2)
                    {
                        chr.ChangeModifier(StatModifierFloat.LightAttribute, 15 / 100f);
                        chr.ChangeModifier(StatModifierFloat.ClimateAttribute, 15 / 100f);
                        chr.SoulBuffed2 = true;
                        Asda2SoulGuardHandler.AddSoulGuardSpell(chr, 113);
                    }
                    break;
                case 20:
                    if(!chr.SoulBuffed3)
                    {
                        chr.ChangeModifier(StatModifierFloat.HealthRegen, 15 / 100f);
                        chr.SoulBuffed3 = true;
                        Asda2SoulGuardHandler.AddSoulGuardSpell(chr, 270);
                    }
                    break;
                case 21:
                    if(!chr.SoulBuffed1)
                    {
                        chr.ChangeModifier(StatModifierFloat.EarthAttribute, 15 / 100f);
                        chr.SoulBuffed1 = true;
                        Asda2SoulGuardHandler.AddSoulGuardSpell(chr, 255);
                    }
                    break;
                case 22:
                    if(!chr.SoulBuffed2)
                    {
                        chr.ChangeModifier(StatModifierFloat.Asda2Defence, 15 / 100f);
                        chr.SoulBuffed2 = true;
                        Asda2SoulGuardHandler.AddSoulGuardSpell(chr, 113);
                    }
                    break;
                case 23:
                    if(!chr.SoulBuffed3)
                    {
                        chr.ChangeModifier(StatModifierFloat.EarthAttribute, 15 / 100f);
                        chr.SoulBuffed3 = true;
                        Asda2SoulGuardHandler.AddSoulGuardSpell(chr, 299);
                    }
                    break;
                case 24:
                    if (!chr.SoulBuffed1)
                    {
                        chr.ChangeModifier(StatModifierFloat.FireAttribute, 15 / 100f);
                        chr.ChangeModifier(StatModifierFloat.DarkAttribute, 15 / 100f);
                    }
                    chr.SoulBuffed1 = true;
                    Asda2SoulGuardHandler.AddSoulGuardSpell(chr, 254);
                    break;
                case 25:
                    if(!chr.SoulBuffed2)
                    chr.ChangeModifier(StatModifierFloat.Asda2Defence, 15 / 100f);
                    chr.SoulBuffed2 = true;
                    Asda2SoulGuardHandler.AddSoulGuardSpell(chr, 270);
                    break;
                case 26:
                    if(!chr.SoulBuffed3)
                    chr.ChangeModifier(StatModifierFloat.HealthRegen, 15 / 100f);
                    chr.SoulBuffed3 = true;
                    Asda2SoulGuardHandler.AddSoulGuardSpell(chr, 252);
                    break;
                default:
                    Asda2SoulGuardHandler.AddSoulGuardSpell(chr, -1);
                    break;

            }
        }

        public static void RemoveSoulSpell(Character chr, int soulid)
        {

            switch (soulid)
            {
                case 0:
                    if(chr.SoulBuffed1)
                    chr.ChangeModifier(StatModifierFloat.Damage, -15 / 100f);
                    chr.SoulBuffed1 = false;
                    Asda2SoulGuardHandler.RemoveSoulGuardSpell(chr, 237);
                    break;
                case 1:
                    if(chr.SoulBuffed2)
                    chr.ChangeModifier(StatModifierFloat.Asda2Defence, -15 / 100f);
                    chr.SoulBuffed2 = false;
                    Asda2SoulGuardHandler.RemoveSoulGuardSpell(chr, 219);
                    break;
                case 2:
                    if(chr.SoulBuffed3)
                    chr.ChangeModifier(StatModifierFloat.Asda2Defence, -15 / 100f);
                    chr.SoulBuffed3 = false;
                    Asda2SoulGuardHandler.RemoveSoulGuardSpell(chr, 220);
                    break;
                case 3:
                    if(chr.SoulBuffed1)
                    chr.ChangeModifier(StatModifierFloat.Damage, -15 / 100f);
                    chr.SoulBuffed1 = false;
                    Asda2SoulGuardHandler.RemoveSoulGuardSpell(chr, 224);
                    break;
                case 4:
                    if(chr.SoulBuffed2)
                    chr.ChangeModifier(StatModifierFloat.Damage, -15 / 100f);
                    chr.SoulBuffed2 = false;
                    Asda2SoulGuardHandler.RemoveSoulGuardSpell(chr, 226);
                    break;
                case 5:
                    if(chr.SoulBuffed3)
                    chr.ChangeModifier(StatModifierFloat.Asda2Defence, -15 / 100f);
                    chr.SoulBuffed3 = false;
                    Asda2SoulGuardHandler.RemoveSoulGuardSpell(chr, 220);
                    break;
                case 6:
                    if(chr.SoulBuffed1)
                    chr.ChangeModifier(StatModifierFloat.Damage, -15 / 100f);
                    chr.SoulBuffed1 = false;
                    Asda2SoulGuardHandler.RemoveSoulGuardSpell(chr, 233);
                    break;
                case 7:
                    if(chr.SoulBuffed2)
                    chr.ChangeModifier(StatModifierFloat.Asda2Defence, -15 / 100f);
                    chr.SoulBuffed2 = false;
                    Asda2SoulGuardHandler.RemoveSoulGuardSpell(chr, 219);
                    break;
                case 8:
                    if(chr.SoulBuffed3)
                    chr.ChangeModifier(StatModifierFloat.Damage, -15 / 100f);
                    chr.SoulBuffed3 = false;
                    Asda2SoulGuardHandler.RemoveSoulGuardSpell(chr, 220);
                    break;
                case 9:
                    if (chr.SoulBuffed1)
                    {
                        chr.ChangeModifier(StatModifierFloat.Damage, -15 / 100f);
                        chr.ChangeModifier(StatModifierFloat.Health, -15 / 100f);
                    }
                    chr.SoulBuffed1 = false;
                    Asda2SoulGuardHandler.RemoveSoulGuardSpell(chr, 237);
                    break;
                case 10:
                    if(chr.SoulBuffed2)
                    chr.ChangeModifier(StatModifierFloat.Asda2Defence, -15 / 100f);
                    chr.SoulBuffed2 = false;
                    Asda2SoulGuardHandler.RemoveSoulGuardSpell(chr, 233);
                    break;
                case 11:
                    if(chr.SoulBuffed3)
                    chr.ChangeModifier(StatModifierFloat.Damage, -15 / 100f);
                    chr.SoulBuffed3 = false;
                    Asda2SoulGuardHandler.RemoveSoulGuardSpell(chr, 238);
                    break;
                case 12:
                    if(chr.SoulBuffed1)
                    chr.ChangeModifier(StatModifierFloat.Speed, -15 / 100f);
                    chr.SoulBuffed1 = false;
                    Asda2SoulGuardHandler.RemoveSoulGuardSpell(chr, 241);
                    break;
                case 13:
                    if(chr.SoulBuffed2)
                    chr.ChangeModifier(StatModifierFloat.Damage, -15 / 100f);
                    chr.SoulBuffed2 = false;
                    Asda2SoulGuardHandler.RemoveSoulGuardSpell(chr, 242);
                    break;
                case 14:
                    if(chr.SoulBuffed3)
                    chr.ChangeModifier(StatModifierFloat.Damage, -15 / 100f);
                    chr.SoulBuffed3 = false;
                    Asda2SoulGuardHandler.RemoveSoulGuardSpell(chr, 231);
                    break;
                case 15:
                    if(chr.SoulBuffed1)
                    Asda2SoulGuardHandler.RemoveSoulGuardSpell(chr, 248);
                    chr.SoulBuffed1 = false;
                    break;
                case 16:
                    if(chr.SoulBuffed2)
                    Asda2SoulGuardHandler.RemoveSoulGuardSpell(chr, 249);
                    chr.SoulBuffed2 = false;
                    break;
                case 17:
                    if(chr.SoulBuffed3)
                    Asda2SoulGuardHandler.RemoveSoulGuardSpell(chr, 250);
                    chr.SoulBuffed3 = false;
                    break;
                case 18:
                    if(chr.SoulBuffed1)
                    {
                        chr.ChangeModifier(StatModifierFloat.HealthRegen, -15 / 100f);
                        chr.SoulBuffed1 = false;
                        Asda2SoulGuardHandler.RemoveSoulGuardSpell(chr, 264);
                    }
                    break;
                case 19:
                    if (chr.SoulBuffed2)
                    {
                        chr.ChangeModifier(StatModifierFloat.LightAttribute, -15 / 100f);
                        chr.ChangeModifier(StatModifierFloat.ClimateAttribute, -15 / 100f);
                        chr.SoulBuffed2 = false;
                        Asda2SoulGuardHandler.RemoveSoulGuardSpell(chr, 113);
                    }
                    break;
                case 20:
                    if(chr.SoulBuffed3)
                    {
                        chr.ChangeModifier(StatModifierFloat.HealthRegen, -15 / 100f);
                        chr.SoulBuffed3 = false;
                        Asda2SoulGuardHandler.RemoveSoulGuardSpell(chr, 270);
                    }
                    break;
                case 21:
                    if(chr.SoulBuffed1)
                    {
                        chr.ChangeModifier(StatModifierFloat.EarthAttribute, -15 / 100f);
                        chr.SoulBuffed1 = false;
                        Asda2SoulGuardHandler.RemoveSoulGuardSpell(chr, 255);
                    }
                    break;
                case 22:
                    if(chr.SoulBuffed2)
                    {
                        chr.ChangeModifier(StatModifierFloat.Asda2Defence, -15 / 100f);
                        chr.SoulBuffed2 = false;
                        Asda2SoulGuardHandler.RemoveSoulGuardSpell(chr, 113);
                    }
                    break;
                case 23:
                    if(chr.SoulBuffed3)
                    {
                        chr.ChangeModifier(StatModifierFloat.EarthAttribute, -15 / 100f);
                        chr.SoulBuffed3 = false;
                        Asda2SoulGuardHandler.RemoveSoulGuardSpell(chr, 299);
                    }
                    break;
                case 24:
                    if (chr.SoulBuffed1)
                    {
                        chr.ChangeModifier(StatModifierFloat.FireAttribute, -15 / 100f);
                        chr.ChangeModifier(StatModifierFloat.DarkAttribute, -15 / 100f);
                    }
                    chr.SoulBuffed1 = false;
                    Asda2SoulGuardHandler.RemoveSoulGuardSpell(chr, 254);
                    break;
                case 25:
                    if(chr.SoulBuffed2)
                    chr.ChangeModifier(StatModifierFloat.Asda2Defence, -15 / 100f);
                    chr.SoulBuffed2 = false;
                    Asda2SoulGuardHandler.RemoveSoulGuardSpell(chr, 270);
                    break;
                case 26:
                    if(chr.SoulBuffed3)
                    chr.ChangeModifier(StatModifierFloat.HealthRegen, -15 / 100f);
                    chr.SoulBuffed3 = false;
                    Asda2SoulGuardHandler.RemoveSoulGuardSpell(chr, 252);
                    break;
                default:
                    Asda2SoulGuardHandler.RemoveSoulGuardSpell(chr, -1);
                    break;

            }
        }

    }
}
