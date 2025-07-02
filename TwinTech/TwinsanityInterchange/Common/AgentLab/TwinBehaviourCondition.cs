using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Twinsanity.Libraries;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Common.AgentLab
{
    /// <summary>
    /// Behaviour conditions allowing to jump to different states in the behaviours
    /// </summary>
    public class TwinBehaviourCondition : ITwinSerializable
    {
        /// <summary>
        /// Condition identified/delegate index in the game's table
        /// </summary>
        public UInt16 ConditionIndex;
        /// <summary>
        /// Some parameter
        /// </summary>
        public UInt16 Parameter;
        /// <summary>
        /// If the result should be inversed
        /// </summary>
        public Boolean NotGate;
        /// <summary>
        /// How often the condition is checked
        /// </summary>
        public Single CheckInterval;
        /// <summary>
        /// The expected value to pass the check
        /// </summary>
        public Single ReturnCheck;
        /// <summary>
        /// Priority of the condition
        /// </summary>
        public Single ConditionPowerMultiplier;

        public Int32 GetLength()
        {
            return 16;
        }

        public void Compile()
        {
            return;
        }

        public void Read(BinaryReader reader, Int32 length)
        {
            var bitfield = reader.ReadInt32();
            ConditionIndex = (UInt16)(bitfield & 0xFFFF);
            Parameter = (UInt16)((bitfield & 0xFFFF0000) >> 17);
            NotGate = (bitfield & 0x10000) != 0;
            CheckInterval = reader.ReadSingle();
            ReturnCheck = reader.ReadSingle();
            ConditionPowerMultiplier = reader.ReadSingle();
        }

        public void Write(BinaryWriter writer)
        {
            Int32 newBitfield = ConditionIndex;
            newBitfield = (newBitfield & 0x1FFFF) | (Int32)((Parameter << 17) & 0xFFFE0000);
            if (NotGate)
            {
                newBitfield |= 0x10000;
            }
            writer.Write(newBitfield);
            writer.Write(CheckInterval);
            writer.Write(ReturnCheck);
            writer.Write(ConditionPowerMultiplier);
        }

        public void WriteText(StreamWriter writer, Int32 tabs = 0)
        {
            AgentLabDefs defs = PS2BehaviourGraph.GetAgentLabDefs();
            StringUtils.WriteLineTabulated(writer, $"Condition {MapIndex(ConditionIndex, defs)}({Parameter}) {"{"}", tabs);
            StringUtils.WriteLineTabulated(writer, $"({CheckInterval.ToString(CultureInfo.InvariantCulture)}, " +
                $"{ReturnCheck.ToString(CultureInfo.InvariantCulture)}, " +
                $"{ConditionPowerMultiplier.ToString(CultureInfo.InvariantCulture)}) == {(NotGate ? "false" : "true")}", tabs + 1);
            StringUtils.WriteLineTabulated(writer, "}", tabs);
        }

        public void ReadText(StreamReader reader, String condName)
        {
            String line = "";
            if (condName.StartsWith("ById_"))
            {
                ConditionIndex = UInt16.Parse(StringUtils.GetStringAfter(condName, "ById_"));
            }
            else
            {
                AgentLabDefs defs = PS2BehaviourGraph.GetAgentLabDefs();
                ConditionIndex = UInt16.Parse((defs.ConditionMap.FirstOrDefault(x => x.Value == condName).Key));
            }
            while (!line.EndsWith("}"))
            {
                line = reader.ReadLine().Trim();
                if (String.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                if (line.StartsWith("("))
                {
                    String right = StringUtils.GetStringAfter(line, "==").Trim();
                    if (right == "true")
                    {
                        NotGate = false;
                    }
                    else
                    {
                        NotGate = true;
                    }
                    String[] floats = StringUtils.GetStringInBetween(line, "(", ")").Split(',');
                    CheckInterval = Single.Parse(floats[0], CultureInfo.InvariantCulture);
                    ReturnCheck = Single.Parse(floats[1], CultureInfo.InvariantCulture);
                    ConditionPowerMultiplier = Single.Parse(floats[2], CultureInfo.InvariantCulture);
                }
            }
        }

        private string MapIndex(UInt32 index, AgentLabDefs defs)
        {
            string str_index = index.ToString();
            if (defs.ConditionMap.ContainsKey(str_index))
            {
                return defs.ConditionMap[str_index];
            }
            else
            {
                return $"ById_{str_index}";
            }
        }
    }
}
