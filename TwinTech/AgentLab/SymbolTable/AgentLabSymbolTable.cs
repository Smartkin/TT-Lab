using System;
using System.Collections.Generic;
using System.Text;

namespace Twinsanity.AgentLab.SymbolTable;

internal class AgentLabSymbolTable
{
    private readonly Dictionary<string, AgentLabSymbol> _symbols = new();

    public AgentLabSymbolTable()
    {
        InitBuiltInTypes();
    }

    public void Define(AgentLabSymbol symbol)
    {
        _symbols.Add(symbol.Name, symbol);
    }

    public AgentLabSymbol Lookup(string name)
    {
        return _symbols.GetValueOrDefault(name);
    }

    private void InitBuiltInTypes()
    {
        static AgentLabBuiltInSymbol CreateBuiltInSymbol(AgentLabToken.TokenType token)
        {
            return new AgentLabBuiltInSymbol(token.ToString());
        }

        AgentLabConstSymbol CreateBuiltInConstSymbol(string name, AgentLabToken.TokenType token)
        {
            return new AgentLabConstSymbol(name, Lookup(token.ToString()));
        }
        
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
        
        // InstanceType consts
        Define(CreateBuiltInConstSymbol("Pickup", AgentLabToken.TokenType.EnumType));
        Define(CreateBuiltInConstSymbol("Projectile", AgentLabToken.TokenType.EnumType));
        
        // Starter consts
        Define(CreateBuiltInConstSymbol("GlobalObjectId", AgentLabToken.TokenType.StringType));
        Define(CreateBuiltInConstSymbol("AssignType", AgentLabToken.TokenType.EnumType));
        Define(CreateBuiltInConstSymbol("AssignLocality", AgentLabToken.TokenType.EnumType));
        Define(CreateBuiltInConstSymbol("AssignStatus", AgentLabToken.TokenType.EnumType));
        Define(CreateBuiltInConstSymbol("AssignPreference", AgentLabToken.TokenType.EnumType));
        
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
        
        // ControlPacket settings consts
        Define(CreateBuiltInConstSymbol("SpaceType", AgentLabToken.TokenType.EnumType));
        Define(CreateBuiltInConstSymbol("MotionType", AgentLabToken.TokenType.EnumType));
        Define(CreateBuiltInConstSymbol("AccelerationFunction", AgentLabToken.TokenType.EnumType));
        Define(CreateBuiltInConstSymbol("Axes", AgentLabToken.TokenType.EnumType));
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
        Define(CreateBuiltInConstSymbol("ContinuouslyRotatesInWorldSpace", AgentLabToken.TokenType.BooleanType));
        Define(CreateBuiltInConstSymbol("Stalls", AgentLabToken.TokenType.BooleanType));
    }

    public override String ToString()
    {
        var resultString = new StringBuilder();
        resultString.Append("Symbols:\n");
        foreach (var symbol in _symbols.Values)
        {
            resultString.AppendLine($"\t{symbol}");
        }
        return resultString.ToString();
    }
}