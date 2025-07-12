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
    private Func<int> constIdGenerator;
    
    public AgentLabSymbolTable Parent { get; init; }
    public List<AgentLabSymbolTable> Children { get; } = new();

    public AgentLabSymbolTable()
    {
        constIdGenerator = GetIdGenerator();
        InitBuiltInPrimitives();
    }

    internal int GenerateConstId()
    {
        return constIdGenerator();
    }

    private Func<int> GetIdGenerator()
    {
        var id = 0;
        return () => id++;
    }

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

    public T Lookup<T>(string name) where T : AgentLabSymbol
    {
        return Lookup(name) as T;
    }

    public AgentLabSymbol Lookup(string name)
    {
        var symbol = _symbols.GetValueOrDefault(name);
        if (symbol == null)
        {
            foreach (var child in Children)
            {
                symbol = child.Lookup(name);
                if (symbol != null)
                {
                    break;
                }
            }
        }
        
        if (symbol == null && Parent != null)
        {
            symbol = Parent.Lookup(name);
        }
        return symbol;
    }

    private void InitBuiltInPrimitives()
    {
        static AgentLabBuiltInSymbol CreateBuiltInSymbol(AgentLabToken.TokenType token)
        {
            return new AgentLabBuiltInSymbol(token.ToString());
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
        Define(CreateBuiltInSymbol(AgentLabToken.TokenType.Undefined));
    }

    public void InitBuiltInTypes()
    {
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
        
        // ControlPacket setting consts
        Define(CreateBuiltInEnumSymbol("Space", Enum.GetNames<TwinBehaviourControlPacket.SpaceType>()));
        Define(CreateBuiltInEnumSymbol("Motion", Enum.GetNames<TwinBehaviourControlPacket.MotionType>()));
        Define(CreateBuiltInEnumSymbol("AccelerationFunction", Enum.GetNames<TwinBehaviourControlPacket.AccelFunction>()));
        Define(CreateBuiltInEnumSymbol("Axes", Enum.GetNames<TwinBehaviourControlPacket.NaturalAxes>()));
        Define(CreateBuiltInEnumSymbol("ContinuousRotate", Enum.GetNames<TwinBehaviourControlPacket.ContinuousRotateType>()));
        Define(CreateBuiltInConstSymbol("Translates", AgentLabToken.TokenType.BooleanType));
        Define(CreateBuiltInConstSymbol("Rotates", AgentLabToken.TokenType.BooleanType));
        Define(CreateBuiltInConstSymbol("TranslationContinues", AgentLabToken.TokenType.BooleanType));
        Define(CreateBuiltInConstSymbol("InterpolatesAngles", AgentLabToken.TokenType.BooleanType));
        Define(CreateBuiltInConstSymbol("YawFaces", AgentLabToken.TokenType.BooleanType));
        Define(CreateBuiltInConstSymbol("PitchFaces", AgentLabToken.TokenType.BooleanType));
        Define(CreateBuiltInConstSymbol("OrientsPredicts", AgentLabToken.TokenType.BooleanType));
        Define(CreateBuiltInConstSymbol("KeyIsLocal", AgentLabToken.TokenType.BooleanType));
        Define(CreateBuiltInConstSymbol("UsesInterpolator", AgentLabToken.TokenType.BooleanType));
        Define(CreateBuiltInConstSymbol("UsesPhysics", AgentLabToken.TokenType.BooleanType));
        Define(CreateBuiltInConstSymbol("UsesRotator", AgentLabToken.TokenType.BooleanType));
        Define(CreateBuiltInConstSymbol("ContinuouslyRotatesInWorldSpace", AgentLabToken.TokenType.BooleanType));
        Define(CreateBuiltInConstSymbol("Stalls", AgentLabToken.TokenType.BooleanType));
        
        // BuiltIn arrays
        Define(CreateBuiltInArraySymbol("InstanceFloat", 126, AgentLabToken.TokenType.FloatType));
        return;

        AgentLabEnumSymbol CreateBuiltInEnumSymbol(string name, params string[] enumNames)
        {
            var enumSymbol = new AgentLabEnumSymbol(name, Lookup(nameof(AgentLabToken.TokenType.EnumType)), enumNames);
            Children.Add(enumSymbol.Enums);
            return enumSymbol;
        }

        AgentLabArraySymbol CreateBuiltInArraySymbol(string name, int size, AgentLabToken.TokenType storageType)
        {
            return new AgentLabArraySymbol(name, size, Lookup(storageType.ToString()), Lookup(nameof(AgentLabToken.TokenType.ArrayType)));
        }

        AgentLabConstSymbol CreateBuiltInConstSymbol(string name, AgentLabToken.TokenType token)
        {
            return new AgentLabConstSymbol(name, Lookup(token.ToString()), constIdGenerator());
        }
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