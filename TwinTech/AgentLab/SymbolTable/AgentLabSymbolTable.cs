using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace Twinsanity.AgentLab.SymbolTable;

internal class AgentLabSymbolTable
{
    private readonly Dictionary<string, AgentLabSymbol> _symbols = new();

    internal IEnumerable<AgentLabSymbol> GetAllSymbols()
    {
        return _symbols.Values;
    }

    internal IEnumerable<T> GetSymbols<T>() where T : AgentLabSymbol
    {
        return _symbols.Values.OfType<T>();
    }

    public void Define(AgentLabSymbol symbol)
    {
        _symbols.Add(symbol.Name, symbol);
    }

    public AgentLabSymbol Lookup(string name)
    {
        return _symbols.GetValueOrDefault(name);
    }

    public void InitBuiltInTypes()
    {
        static AgentLabBuiltInSymbol CreateBuiltInSymbol(AgentLabToken.TokenType token)
        {
            return new AgentLabBuiltInSymbol(token.ToString());
        }

        AgentLabConstSymbol CreateBuiltInConstSymbol(string name, AgentLabToken.TokenType token)
        {
            return new AgentLabConstSymbol(name, Lookup(token.ToString()));
        }

        AgentLabArraySymbol CreateBuiltInArraySymbol(string name, int size, AgentLabToken.TokenType storageType)
        {
            return new AgentLabArraySymbol(name, size, Lookup(storageType.ToString()), Lookup(nameof(AgentLabToken.TokenType.ArrayType)));
        }

        AgentLabEnumSymbol CreateBuiltInEnumSymbol(string name, params string[] enumNames)
        {
            return new AgentLabEnumSymbol(name, Lookup(nameof(AgentLabToken.TokenType.EnumType)), enumNames);
        }
        
        Define(CreateBuiltInSymbol(AgentLabToken.TokenType.ArrayType));
        Define(CreateBuiltInSymbol(AgentLabToken.TokenType.IntegerType));
        Define(CreateBuiltInSymbol(AgentLabToken.TokenType.FloatType));
        Define(CreateBuiltInSymbol(AgentLabToken.TokenType.BooleanType));
        Define(CreateBuiltInSymbol(AgentLabToken.TokenType.EnumType));
        Define(CreateBuiltInSymbol(AgentLabToken.TokenType.StringType));
        Define(CreateBuiltInSymbol(AgentLabToken.TokenType.ControlPacket));
        Define(CreateBuiltInSymbol(AgentLabToken.TokenType.State));
        Define(CreateBuiltInSymbol(AgentLabToken.TokenType.Action));
        Define(CreateBuiltInSymbol(AgentLabToken.TokenType.Condition));
        Define(CreateBuiltInSymbol(AgentLabToken.TokenType.Behaviour));
        Define(CreateBuiltInSymbol(AgentLabToken.TokenType.BehaviourLibrary));
        
        // Object behaviour slots
        Define(CreateBuiltInEnumSymbol("ObjectBehaviourSlot", Enum.GetNames<ITwinBehaviourState.ObjectBehaviourSlots>()));
        
        // InstanceType consts
        Define(CreateBuiltInEnumSymbol("InstanceType", Enum.GetNames<ITwinBehaviourCommandsSequence.InstanceType>()));
        
        // Starter consts
        Define(CreateBuiltInConstSymbol("GlobalObjectId", AgentLabToken.TokenType.StringType));
        Define(CreateBuiltInEnumSymbol("AssignType", Enum.GetNames<TwinBehaviourAssigner.AssignTypeID>()));
        Define(CreateBuiltInEnumSymbol("AssignLocality", Enum.GetNames<TwinBehaviourAssigner.AssignLocalityID>()));
        Define(CreateBuiltInEnumSymbol("AssignStatus", Enum.GetNames<TwinBehaviourAssigner.AssignStatusID>()));
        Define(CreateBuiltInEnumSymbol("AssignPreference", Enum.GetNames<TwinBehaviourAssigner.AssignPreferenceID>()));
        
        // ControlPacket data consts
        Define(CreateBuiltInConstSymbol("Selector", AgentLabToken.TokenType.IntegerType));
        Define(CreateBuiltInConstSymbol("KeyIndex", AgentLabToken.TokenType.IntegerType));
        Define(CreateBuiltInConstSymbol("SyncUnit", AgentLabToken.TokenType.IntegerType));
        Define(CreateBuiltInConstSymbol("JointIndex", AgentLabToken.TokenType.IntegerType));
        Define(CreateBuiltInConstSymbol("MoveSpeed", AgentLabToken.TokenType.FloatType));
        Define(CreateBuiltInConstSymbol("TurnSpeed", AgentLabToken.TokenType.FloatType));
        Define(CreateBuiltInConstSymbol("RawPosX", AgentLabToken.TokenType.FloatType));
        Define(CreateBuiltInConstSymbol("RawPosY", AgentLabToken.TokenType.FloatType));
        Define(CreateBuiltInConstSymbol("RawPosZ", AgentLabToken.TokenType.FloatType));
        Define(CreateBuiltInConstSymbol("Pitch", AgentLabToken.TokenType.FloatType));
        Define(CreateBuiltInConstSymbol("Yaw", AgentLabToken.TokenType.FloatType));
        Define(CreateBuiltInConstSymbol("Roll", AgentLabToken.TokenType.FloatType));
        Define(CreateBuiltInConstSymbol("Delay", AgentLabToken.TokenType.FloatType));
        Define(CreateBuiltInConstSymbol("Duration", AgentLabToken.TokenType.FloatType));
        Define(CreateBuiltInConstSymbol("Tumble", AgentLabToken.TokenType.FloatType));
        Define(CreateBuiltInConstSymbol("Spin", AgentLabToken.TokenType.FloatType));
        Define(CreateBuiltInConstSymbol("Twist", AgentLabToken.TokenType.FloatType));
        Define(CreateBuiltInConstSymbol("RandRange", AgentLabToken.TokenType.FloatType));
        Define(CreateBuiltInConstSymbol("Power", AgentLabToken.TokenType.FloatType));
        Define(CreateBuiltInConstSymbol("Damping", AgentLabToken.TokenType.FloatType));
        Define(CreateBuiltInConstSymbol("AcDist", AgentLabToken.TokenType.FloatType));
        Define(CreateBuiltInConstSymbol("DecDist", AgentLabToken.TokenType.FloatType));
        Define(CreateBuiltInConstSymbol("Bounce", AgentLabToken.TokenType.FloatType));
        
        Define(CreateBuiltInEnumSymbol("SpaceType", Enum.GetNames<TwinBehaviourControlPacket.SpaceType>()));
        Define(CreateBuiltInEnumSymbol("MotionType", Enum.GetNames<TwinBehaviourControlPacket.MotionType>()));
        Define(CreateBuiltInEnumSymbol("AccelerationFunction", Enum.GetNames<TwinBehaviourControlPacket.AccelFunction>()));
        Define(CreateBuiltInEnumSymbol("Axes", Enum.GetNames<TwinBehaviourControlPacket.NaturalAxes>()));
        Define(CreateBuiltInEnumSymbol("ContinuousRotate", Enum.GetNames<TwinBehaviourControlPacket.ContinuousRotate>()));
        
        Define(CreateBuiltInConstSymbol("DoesTranslate", AgentLabToken.TokenType.BooleanType));
        Define(CreateBuiltInConstSymbol("DoesRotate", AgentLabToken.TokenType.BooleanType));
        Define(CreateBuiltInConstSymbol("DoesTranslationContinue", AgentLabToken.TokenType.BooleanType));
        Define(CreateBuiltInConstSymbol("DoesInterpolateAngles", AgentLabToken.TokenType.BooleanType));
        Define(CreateBuiltInConstSymbol("DoesYawFaces", AgentLabToken.TokenType.BooleanType));
        Define(CreateBuiltInConstSymbol("DoesPitchFaces", AgentLabToken.TokenType.BooleanType));
        Define(CreateBuiltInConstSymbol("DoesOrientPredicts", AgentLabToken.TokenType.BooleanType));
        Define(CreateBuiltInConstSymbol("KeyIsLocal", AgentLabToken.TokenType.BooleanType));
        Define(CreateBuiltInConstSymbol("UsesInterpolator", AgentLabToken.TokenType.BooleanType));
        Define(CreateBuiltInConstSymbol("UsesPhysics", AgentLabToken.TokenType.BooleanType));
        Define(CreateBuiltInConstSymbol("UsesRotator", AgentLabToken.TokenType.BooleanType));
        Define(CreateBuiltInConstSymbol("ContinuouslyRotatesInWorldSpace", AgentLabToken.TokenType.BooleanType));
        Define(CreateBuiltInConstSymbol("Stalls", AgentLabToken.TokenType.BooleanType));
        Define(CreateBuiltInArraySymbol("InstanceFloat", 126, AgentLabToken.TokenType.FloatType));
    }

    public override String ToString()
    {
        var resultString = new StringBuilder();
        resultString.Append("Symbols:\n");
        var noConditionsOrActions = _symbols.Values.Where(s => s is not AgentLabActionSymbol and not AgentLabConditionSymbol).ToList();
        foreach (var symbol in noConditionsOrActions)
        {
            resultString.AppendLine($"\t{symbol}");
        }
        return resultString.ToString();
    }
}