using System.Collections.Generic;
using System.Globalization;
using WCell.Constants;
using WCell.Constants.Updates;
using WCell.Core.Network;
using WCell.RealmServer.Entities;
using WCell.RealmServer.Global;
using WCell.Util;
using WCell.Util.Commands;

namespace WCell.RealmServer.Commands
{
  public class SendRawCommand : RealmServerCommand
  {
    protected SendRawCommand()
    {
    }

    protected override void Initialize()
    {
      Init("SendRaw", "SendRawInt", "SendRawB", "SendRawByte", "SendRawBytes");
      EnglishParamInfo = "[byte|int] <opcode> [<value1> [<value2> ...]]";
      EnglishDescription = "Sends a raw packet to the client";
    }

    public override void Process(CmdTrigger<RealmServerCmdArgs> trigger)
    {
      bool writeBytes = IsByteAlias(trigger.Alias);
      bool forceInts = IsIntAlias(trigger.Alias);
      string opcodeText = trigger.Text.NextWord();
      if(IsByteMode(opcodeText))
      {
        writeBytes = true;
        opcodeText = trigger.Text.NextWord();
      }
      else if(IsIntMode(opcodeText))
      {
        writeBytes = false;
        forceInts = true;
        opcodeText = trigger.Text.NextWord();
      }

      RealmServerOpCode packetOpCode;
      if(!EnumUtil.TryParse(opcodeText, out packetOpCode) || packetOpCode == RealmServerOpCode.Unknown)
      {
        trigger.Reply("Invalid OpCode.");
        return;
      }

      if(!writeBytes && !forceInts && packetOpCode == RealmServerOpCode.SetClientTime)
        writeBytes = true;

      List<int> values = ReadValues(trigger);
      if(values == null)
        return;

      if(writeBytes && !ValidateByteValues(trigger, values))
        return;

      using(RealmPacketOut packet = new RealmPacketOut(packetOpCode))
      {
        foreach(int value in values)
        {
          if(writeBytes)
            packet.WriteByte((byte) value);
          else
            packet.Write(value);
        }

        int recipients = SendPacket(trigger, packet, packetOpCode);
        trigger.Reply("Sent raw packet {0} with {1} {2} to {3} client(s).", packetOpCode, values.Count,
          writeBytes ? "byte(s)" : "int(s)", recipients);
      }
    }

    private static int SendPacket(CmdTrigger<RealmServerCmdArgs> trigger, RealmPacketOut packet,
      RealmServerOpCode packetOpCode)
    {
      if(packetOpCode == RealmServerOpCode.SetClientTime)
        return SendPacketToAll(packet);

      Character target = (Character) trigger.Args.Target;
      if(target.Client == null)
        return 0;
      target.Client.Send(packet, false);
      return 1;
    }

    private static int SendPacketToAll(RealmPacketOut packet)
    {
      int count = 0;
      foreach(Character character in World.GetAllCharacters())
      {
        if(character.Client == null)
          continue;
        character.Client.Send(packet, false);
        ++count;
      }
      return count;
    }

    private static List<int> ReadValues(CmdTrigger<RealmServerCmdArgs> trigger)
    {
      List<int> values = new List<int>();
      while(trigger.Text.HasNext)
      {
        string valueText = trigger.Text.NextWord();
        int value;
        if(!int.TryParse(valueText, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
          trigger.Reply("Invalid value: " + valueText);
          return null;
        }
        values.Add(value);
      }
      return values;
    }

    private static bool ValidateByteValues(CmdTrigger<RealmServerCmdArgs> trigger, List<int> values)
    {
      foreach(int value in values)
      {
        if(value >= byte.MinValue && value <= byte.MaxValue)
          continue;
        trigger.Reply("Byte value out of range: " + value);
        return false;
      }
      return true;
    }

    private static bool IsByteAlias(string alias)
    {
      return IsMode(alias, "SendRawB") || IsMode(alias, "SendRawByte") || IsMode(alias, "SendRawBytes");
    }

    private static bool IsIntAlias(string alias)
    {
      return IsMode(alias, "SendRawInt");
    }

    private static bool IsByteMode(string mode)
    {
      return IsMode(mode, "b") || IsMode(mode, "byte") || IsMode(mode, "bytes");
    }

    private static bool IsIntMode(string mode)
    {
      return IsMode(mode, "i") || IsMode(mode, "int") || IsMode(mode, "ints");
    }

    private static bool IsMode(string value, string expected)
    {
      return string.Equals(value, expected, System.StringComparison.InvariantCultureIgnoreCase);
    }

    public override ObjectTypeCustom TargetTypes
    {
      get { return ObjectTypeCustom.Player; }
    }
  }
}
