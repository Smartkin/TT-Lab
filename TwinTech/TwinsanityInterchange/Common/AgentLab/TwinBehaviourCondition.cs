using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Twinsanity.AgentLab.Resolvers;
using Twinsanity.AgentLab.Resolvers.Interfaces;
using Twinsanity.Libraries;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace Twinsanity.TwinsanityInterchange.Common.AgentLab
{
    /// <summary>
    /// The condition a state body runs on. Every update the game evaluates the conditions of the state's bodies, a body
    /// passes when its result is above the threshold (below it when inverted) and the passing body with the largest
    /// (result - threshold) * weight runs
    /// </summary>
    public class TwinBehaviourCondition : ITwinAgentLab
    {
        /// <summary>
        /// The condition the game evaluates, an index into its table of conditions (see ConditionDefinitions.lab)
        /// </summary>
        public UInt16 ConditionIndex;
        /// <summary>
        /// 15-bit parameter of the condition, only some conditions read it (a message number, an actor type, a counter index)
        /// </summary>
        public UInt16 Parameter;
        /// <summary>
        /// Passes when the result is below the threshold instead of above it
        /// </summary>
        public Boolean NotGate;
        /// <summary>
        /// Extra parameter only some conditions read, a time window in seconds for the event conditions (the event
        /// happened within the last so many seconds). The game never uses it as a check interval
        /// </summary>
        public Single TimeWindow;
        /// <summary>
        /// The result has to be above this (below it when inverted) for the body to pass. Some conditions read it as a parameter as well
        /// </summary>
        public Single Threshold;
        /// <summary>
        /// Weights the passing bodies against each other, (result - threshold) * weight, the largest one runs. The game's
        /// scripts have 1 / threshold here
        /// </summary>
        public Single Weight;

        /// <summary>
        /// Index of the fallback condition, its body runs when no other body of the state passes
        /// </summary>
        public const UInt16 ElseConditionIndex = 2;
        /// <summary>
        /// Index of the condition that always passes, the completion bodies use it
        /// </summary>
        public const UInt16 NextConditionIndex = 0;
        /// <summary>
        /// Threshold the game's scripts give the fallback and completion bodies
        /// </summary>
        public const Single DefaultThreshold = 0.5f;

        public Int32 GetLength()
        {
            return 16;
        }

        public void Compile()
        {
            return;
        }

        /// <summary>
        /// The weight the game's scripts have for a threshold
        /// </summary>
        public static Single DefaultWeight(Single threshold)
        {
            return threshold == 0.0f ? 1e30f : 1.0f / threshold;
        }

        /// <summary>
        /// Whether the condition is the fallback the way the game's scripts use it, which decompiles to an else block
        /// </summary>
        public Boolean IsPlainElse => ConditionIndex == ElseConditionIndex && Parameter == 0 && !NotGate;

        /// <summary>
        /// Whether the condition is the always passing one the game's scripts give the completion bodies
        /// </summary>
        public Boolean IsPlainNext => ConditionIndex == NextConditionIndex && Parameter == 0 && !NotGate;

        /// <summary>
        /// Writes the head of the body's block, either "if Condition(parameter) > threshold {" or "else {"
        /// </summary>
        public void Decompile(IResolver resolver, StreamWriter writer, int tabs = 0)
        {
            if (IsPlainElse)
            {
                StringUtils.WriteLineTabulated(writer, "else {", tabs);
                DecompileValues(writer, tabs + 1, true);
                return;
            }
            {
                StringUtils.WriteTabulated(writer, $"if {MapIndex(ConditionIndex, PS2BehaviourGraph.GetAgentLabDefs())}({Parameter})", tabs);
                writer.Write(NotGate ? " < " : " > ");
                writer.Write(TaggedValue.FloatLiteral(Threshold) ?? Threshold.ToString("R", CultureInfo.InvariantCulture));
                writer.WriteLine(" {");
            }

            DecompileValues(writer, tabs + 1);
        }

        /// <summary>
        /// Writes the values that differ from what an absent statement compiles to. The else and completion blocks
        /// name the threshold as well, their head doesn't have it
        /// </summary>
        public void DecompileValues(StreamWriter writer, int tabs, bool includeThreshold = false)
        {
            if (includeThreshold && Threshold != DefaultThreshold)
            {
                StringUtils.WriteLineTabulated(writer, $"threshold = {TaggedValue.FloatLiteral(Threshold) ?? Threshold.ToString("R", CultureInfo.InvariantCulture)};", tabs);
            }

            if (TimeWindow != 0.0f)
            {
                StringUtils.WriteLineTabulated(writer, $"window = {TaggedValue.FloatLiteral(TimeWindow) ?? TimeWindow.ToString("R", CultureInfo.InvariantCulture)};", tabs);
            }

            if (Weight != DefaultWeight(Threshold))
            {
                StringUtils.WriteLineTabulated(writer, $"weight = {TaggedValue.FloatLiteral(Weight) ?? Weight.ToString("R", CultureInfo.InvariantCulture)};", tabs);
            }
        }

        public void Read(BinaryReader reader, Int32 length)
        {
            var bitfield = reader.ReadInt32();
            ConditionIndex = (UInt16)(bitfield & 0xFFFF);
            Parameter = (UInt16)((bitfield & 0xFFFF0000) >> 17);
            NotGate = (bitfield & 0x10000) != 0;
            TimeWindow = reader.ReadSingle();
            Threshold = reader.ReadSingle();
            Weight = reader.ReadSingle();
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
            writer.Write(TimeWindow);
            writer.Write(Threshold);
            writer.Write(Weight);
        }

        public void WriteText(StreamWriter writer, Int32 tabs = 0)
        {
            AgentLabDefs defs = PS2BehaviourGraph.GetAgentLabDefs();
            StringUtils.WriteLineTabulated(writer, $"Condition {MapIndex(ConditionIndex, defs)}({Parameter}) {"{"}", tabs);
            StringUtils.WriteLineTabulated(writer, $"({TimeWindow.ToString(CultureInfo.InvariantCulture)}, " +
                $"{Threshold.ToString(CultureInfo.InvariantCulture)}, " +
                $"{Weight.ToString(CultureInfo.InvariantCulture)}) == {(NotGate ? "false" : "true")}", tabs + 1);
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
                    TimeWindow = Single.Parse(floats[0], CultureInfo.InvariantCulture);
                    Threshold = Single.Parse(floats[1], CultureInfo.InvariantCulture);
                    Weight = Single.Parse(floats[2], CultureInfo.InvariantCulture);
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
                return $"Unknown_{str_index}";
            }
        }
    }
}
