using System;
using System.Collections.Generic;
using System.Linq;
using Twinsanity.AgentLab.AbstractSyntaxTree;
using Twinsanity.AgentLab.AbstractSyntaxTree.Attributes;
using Twinsanity.AgentLab.AbstractSyntaxTree.ControlPacket;
using Twinsanity.AgentLab.SymbolTable;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace Twinsanity.AgentLab;

internal class AgentLabCompilerNodeVisitor : NodeVisitor
{
    private AgentLabSymbolTable _symbolTable;
    private readonly AgentLabCompiler.CompilerResult _result;
    private readonly AgentLabCompiler.CompilerOptions _options;
    private readonly AgentLabMemory _memory = new();
    private readonly Dictionary<string, Type> _enumTypes = new();
    
    public AgentLabCompilerNodeVisitor(AgentLabCompiler.CompilerResult result, AgentLabCompiler.CompilerOptions options, AgentLabSymbolTable symbolTable)
    {
        _result = result;
        _options = options;
        _symbolTable = symbolTable;
        
        _enumTypes.Add(nameof(TwinBehaviourAssigner.AssignLocality), typeof(TwinBehaviourAssigner.AssignLocalityID));
        _enumTypes.Add(nameof(TwinBehaviourAssigner.AssignPreference), typeof(TwinBehaviourAssigner.AssignPreferenceID));
        _enumTypes.Add(nameof(TwinBehaviourAssigner.AssignStatus), typeof(TwinBehaviourAssigner.AssignStatusID));
        _enumTypes.Add(nameof(TwinBehaviourAssigner.AssignType), typeof(TwinBehaviourAssigner.AssignTypeID));
        _enumTypes.Add(nameof(TwinBehaviourControlPacket.AccelerationFunction), typeof(TwinBehaviourControlPacket.AccelFunction));
        _enumTypes.Add(nameof(TwinBehaviourControlPacket.Space), typeof(TwinBehaviourControlPacket.SpaceType));
        _enumTypes.Add(nameof(TwinBehaviourControlPacket.Motion), typeof(TwinBehaviourControlPacket.MotionType));
        _enumTypes.Add(nameof(TwinBehaviourControlPacket.ContinuousRotate), typeof(TwinBehaviourControlPacket.ContinuousRotateType));
        _enumTypes.Add(nameof(TwinBehaviourControlPacket.Axes), typeof(TwinBehaviourControlPacket.NaturalAxes));
        
        Visitors.Add(typeof(IAgentLabListNode), VisitListNode);
        Visitors.Add(typeof(UnaryOperationNode), VisitUnaryOperationNode);
        Visitors.Add(typeof(ConstDeclarationNode), VisitConstDeclarationNode);
        Visitors.Add(typeof(ConstNode), VisitConstNode);
        Visitors.Add(typeof(NumberNode), VisitNumberNode);
        Visitors.Add(typeof(BooleanNode), VisitBooleanNode);
        Visitors.Add(typeof(StringNode), VisitStringNode);
        Visitors.Add(typeof(BehaviourNode), VisitBehaviourNode);
        Visitors.Add(typeof(BehaviourBodyNode), VisitBehaviourBodyNode);
        Visitors.Add(typeof(PriorityAttributeNode), VisitPriorityAttributeNode);
        Visitors.Add(typeof(GraphPriorityAttributeNode), VisitGraphPriorityAttributeNode);
        Visitors.Add(typeof(StartFromAttributeNode), VisitStartFromAttributeNode);
        Visitors.Add(typeof(StarterNode), VisitStarterNode);
        Visitors.Add(typeof(StarterAssignerNode), VisitStarterAssignerNode);
        Visitors.Add(typeof(StarterAssignNode), VisitStarterAssignNode);
        Visitors.Add(typeof(ControlPacketNode), VisitControlPacketNode);
        Visitors.Add(typeof(ControlPacketBodyNode), VisitControlPacketBodyNode);
        Visitors.Add(typeof(ControlPacketDataNode), VisitControlPacketDataNode);
        Visitors.Add(typeof(ControlPacketSettingNode), VisitControlPacketSettingNode);
        Visitors.Add(typeof(StateListNode), VisitStateListNode);
        Visitors.Add(typeof(StateBodyListNode), VisitStateBodyListNode);
        Visitors.Add(typeof(StateExecuteNode), VisitStateExecuteNode);
        Visitors.Add(typeof(StateNode), VisitStateNode);
        Visitors.Add(typeof(ControlPacketAttributeNode), VisitControlPacketAttributeNode);
        Visitors.Add(typeof(UseObjectSlotAttributeNode), VisitUseObjectSlotAttributeNode);
        Visitors.Add(typeof(StateBodyNode), VisitStateBodyNode);
        Visitors.Add(typeof(ActionListNode), VisitActionListNode);
        Visitors.Add(typeof(ActionNode), VisitActionNode);
        Visitors.Add(typeof(ParamListNode), VisitParamListNode);
        Visitors.Add(typeof(ParamNode), VisitParamNode);
        Visitors.Add(typeof(ConditionNode), VisitConditionNode);
        Visitors.Add(typeof(BehaviourLibraryNode), VisitBehaviourLibraryNode);
        Visitors.Add(typeof(GlobalIndexAttributeNode), VisitGlobalIndexAttributeNode);
        Visitors.Add(typeof(InstanceTypeAttributeNode), VisitInstanceTypeAttributeNode);
        Visitors.Add(typeof(LinearBehaviourNode), VisitLinearBehaviourNode);
    }

    private Object VisitBooleanNode(IAgentLabTreeNode node)
    {
        var boolean = (BooleanNode)node;
        return boolean.Value;
    }

    private Object VisitStateExecuteNode(IAgentLabTreeNode node)
    {
        var stateExecute = (StateExecuteNode)node;
        var stateSymbol = _symbolTable.Lookup<AgentLabStateSymbol>(stateExecute.StateName);
        return stateSymbol.Id;
    }

    private Object VisitInstanceTypeAttributeNode(IAgentLabTreeNode node)
    {
        var instanceTypeNode = (InstanceTypeAttributeNode)node;
        return Enum.Parse<ITwinBehaviourCommandsSequence.InstanceType>(instanceTypeNode.Key.Name);
    }

    private Object VisitGlobalIndexAttributeNode(IAgentLabTreeNode node)
    {
        var globalIndexAttribute = (GlobalIndexAttributeNode)node;
        return Visit(globalIndexAttribute.Index);
    }

    private Object VisitLinearBehaviourNode(IAgentLabTreeNode node)
    {
        var linearBehaviour = (LinearBehaviourNode)node;
        var pack = _options.CommandPack.Construct();
        pack.Commands = (List<ITwinBehaviourCommand>)Visit(linearBehaviour.Actions) ?? new List<ITwinBehaviourCommand>();
        var behaviourId = _options.Resolver.GetGraphResolver().ResolveGraphReference(linearBehaviour.Name);

        return new KeyValuePair<ushort, ITwinBehaviourCommandPack>((ushort)behaviourId, pack);
    }

    private Object VisitBehaviourLibraryNode(IAgentLabTreeNode node)
    {
        var libraryNode = (BehaviourLibraryNode)node;
        var library = _options.CommandsSequence.Construct();
        _result.Add(library);

        library.IndexInGlobalStorage = (byte)(int)Visit(libraryNode.GlobalIndex);
        library.Key = (ITwinBehaviourCommandsSequence.InstanceType)Visit(libraryNode.InstanceType);
        library.Commands.Add((ITwinBehaviourCommand)Visit(libraryNode.CreationAction));
        
        foreach (var linearBehaviour in libraryNode.LinearBehaviours.Children)
        {
            library.BehaviourPacks.Add((KeyValuePair<ushort, ITwinBehaviourCommandPack>)Visit(linearBehaviour));
        }
        
        return null;
    }

    private Object VisitStateListNode(IAgentLabTreeNode node)
    {
        var stateListNode = (StateListNode)node;

        return stateListNode.Children.Select(state => (ITwinBehaviourState)Visit(state)).ToList();
    }

    private Object VisitUnaryOperationNode(IAgentLabTreeNode node)
    {
        var unOp = (UnaryOperationNode)node;
        var result = Visit(unOp.Expression);
        return unOp.Token.Type switch
        {
            AgentLabToken.TokenType.SubtractOperator => result switch
            {
                float fValue => -fValue,
                int iValue => -iValue,
                _ => null
            },
            AgentLabToken.TokenType.AddOperator => result switch
            {
                float fValue => fValue,
                int iValue => iValue,
                _ => null
            },
            _ => null
        };
    }

    private Object VisitParamNode(IAgentLabTreeNode node)
    {
        var paramNode = (ParamNode)node;
        return EncodeArgument(paramNode.Value, null);
    }

    private Object VisitParamListNode(IAgentLabTreeNode node)
    {
        var paramsNode = (ParamListNode)node;

        return paramsNode.Children.Select(paramNode => (uint)Visit(paramNode)).ToList();
    }

    private static float ToFloat(object value) => value switch
    {
        float fValue => fValue,
        int iValue => iValue,
        bool bValue => bValue ? 1.0f : 0.0f,
        _ => throw new Exception($"Expected a number, got {value}")
    };

    private static int ToInt(object value) => value switch
    {
        int iValue => iValue,
        float fValue => (int)fValue,
        bool bValue => bValue ? 1 : 0,
        _ => throw new Exception($"Expected a number, got {value}")
    };

    /// <summary>
    /// The bits an argument's value takes in the command, going by the parameter's type. Hex literals are the bits themselves
    /// </summary>
    private uint EncodeArgument(IAgentLabTreeNode valueNode, AgentLabParamSymbol parameter)
    {
        if (valueNode is FieldGroupNode group)
        {
            return PackFields(group, parameter);
        }

        var parameterType = parameter?.Type?.Name;
        var taggedType = parameterType switch
        {
            nameof(AgentLabToken.TokenType.TaggedFloatType) => TaggedValue.TypeFloat,
            nameof(AgentLabToken.TokenType.TaggedIntType) => TaggedValue.TypeInt,
            nameof(AgentLabToken.TokenType.TaggedAngleType) => TaggedValue.TypeAngle,
            _ => (uint?)null
        };
        if (valueNode is TaggedLiteralNode tagged)
        {
            var inner = Visit(tagged.Value);
            return tagged.Name switch
            {
                "Prop" => TaggedValue.FromProperty(ToInt(inner), taggedType ?? TaggedValue.TypeFloat),
                "Raw" => (uint)ToInt(inner),
                "Float" => TaggedValue.FromFloat(ToFloat(inner)),
                "Int" => TaggedValue.FromInt(ToInt(inner)),
                "Angle" => TaggedValue.FromAngle(ToFloat(inner)),
                _ => throw new Exception($"Unknown literal {tagged.Name}")
            };
        }

        var value = Visit(valueNode);
        if (valueNode is NumberNode { Token.IsHexLiteral: true })
        {
            return (uint)(int)value;
        }

        // Scripts written before the tagged values were known have the bits of a property reference printed as a
        // denormal float, which no real value is, so those bits are kept as they are
        if (taggedType != null && value is float tiny && tiny != 0.0f && MathF.Abs(tiny) < 1e-30f)
        {
            return BitConverter.SingleToUInt32Bits(tiny);
        }

        switch (parameterType)
        {
            case nameof(AgentLabToken.TokenType.FloatType):
                return BitConverter.SingleToUInt32Bits(ToFloat(value));
            case nameof(AgentLabToken.TokenType.IntegerType):
                return (uint)ToInt(value);
            case nameof(AgentLabToken.TokenType.TaggedFloatType):
                return TaggedValue.FromFloat(ToFloat(value));
            case nameof(AgentLabToken.TokenType.TaggedIntType):
                return TaggedValue.FromInt(ToInt(value));
            case nameof(AgentLabToken.TokenType.TaggedAngleType):
                return TaggedValue.FromAngle(ToFloat(value));
        }

        return value switch
        {
            float fValue => BitConverter.SingleToUInt32Bits(fValue),
            bool bValue => bValue ? 1U : 0U,
            int iValue => (uint)iValue,
            _ => throw new Exception($"Expected a number, got {value}")
        };
    }

    // The dword a {name = value, ...} argument stands for: every field at its bits, the ones left out 0
    private uint PackFields(FieldGroupNode group, AgentLabParamSymbol parameter)
    {
        var dword = 0u;
        foreach (var (name, valueNode) in group.Fields)
        {
            var field = parameter?.FindField(name.GetValue<string>()) ?? throw new AgentLabSyntaxException($"No field {name.GetValue<string>()}", name.Line, name.Column);
            var value = Visit(valueNode) switch
            {
                bool b => b ? 1L : 0L,
                int i => i,
                float f => (long)f,
                var other => throw new AgentLabSyntaxException($"Field {field.Name} takes a number, not {other}", name.Line, name.Column)
            };
            if (!field.Fits(value))
            {
                var range = field.IsBool ? "true or false" : field.IsSigned ? $"{-(1L << (field.Width - 1))} to {(1L << (field.Width - 1)) - 1}" : $"0 to {(field.Width >= 32 ? uint.MaxValue : (1L << field.Width) - 1)}";
                throw new AgentLabSyntaxException($"Field {field.Name} takes {range}, not {value}", name.Line, name.Column);
            }

            dword = field.Set(dword, value);
        }

        return dword;
    }

    private Object VisitActionNode(IAgentLabTreeNode node)
    {
        var actionNode = (ActionNode)node;
        var action = _options.Command.Construct();
        var actionSymbol = _symbolTable.Lookup<AgentLabActionSymbol>(actionNode.Name);
        action.CommandIndex = (ushort)actionSymbol.Id;
        var parameters = actionSymbol.Parameters?.GetSymbols<AgentLabParamSymbol>().ToList() ?? new List<AgentLabParamSymbol>();
        var arguments = new List<uint>();
        if (actionNode.Parameters != null)
        {
            for (var i = 0; i < actionNode.Parameters.Children.Count; i++)
            {
                var paramNode = (ParamNode)actionNode.Parameters.Children[i];
                arguments.Add(EncodeArgument(paramNode.Value, i < parameters.Count ? parameters[i] : null));
            }
        }

        action.Arguments = arguments;

        return action;
    }

    private Object VisitActionListNode(IAgentLabTreeNode node)
    {
        var actionListNode = (ActionListNode)node;
        var actionList = actionListNode.Children.Select(actionNode => (ITwinBehaviourCommand)Visit(actionNode)).ToList();
        // This is a hack but deal with it
        if (!_result.Contains<ITwinBehaviourCommandPack>())
        {
            var commandPack = _options.CommandPack.Construct();
            commandPack.Commands = actionList;
            _result.Add(commandPack);
        }
        
        return actionList;
    }

    private Object VisitUseObjectSlotAttributeNode(IAgentLabTreeNode node)
    {
        var useObjectSlotAttribute = (UseObjectSlotAttributeNode)node;
        return Enum.Parse<ITwinBehaviourState.ObjectBehaviourSlots>(useObjectSlotAttribute.SlotName.SlotName);
    }

    private Object VisitControlPacketAttributeNode(IAgentLabTreeNode node)
    {
        var controlPacketAttribute = (ControlPacketAttributeNode)node;
        return _memory.Get<TwinBehaviourControlPacket>(controlPacketAttribute.Token.GetValue<string>());
    }

    private Object VisitConditionNode(IAgentLabTreeNode node)
    {
        var conditionNode = (ConditionNode)node;
        var condition = new TwinBehaviourCondition();
        var conditionSymbol = _symbolTable.Lookup<AgentLabConditionSymbol>(conditionNode.Name);
        condition.ConditionIndex = (ushort)conditionSymbol.Id;
        condition.Parameter = (ushort)(int)Visit(conditionNode.Number);
        
        return condition;
    }

    private Object VisitStateBodyListNode(IAgentLabTreeNode node)
    {
        var bodiesNodes = (StateBodyListNode)node;

        return bodiesNodes.Children.Select(bodyNode => (ITwinBehaviourStateBody)Visit(bodyNode)).ToList();
    }

    private Object VisitStateBodyNode(IAgentLabTreeNode node)
    {
        var stateBodyNode = (StateBodyNode)node;
        var stateBody = _options.StateBody.Construct();
        stateBody.Condition = (TwinBehaviourCondition)Visit(stateBodyNode.Condition);
        stateBody.Commands = (List<ITwinBehaviourCommand>)Visit(stateBodyNode.ActionList) ?? new List<ITwinBehaviourCommand>();
        stateBody.JumpToState = Visit(stateBodyNode.StateExecute) is int stateExecute ? stateExecute : -1;
        if (stateBody.JumpToState != -1)
        {
            stateBody.HasStateJump = true;
        }

        var condition = stateBody.Condition;
        condition.Threshold = stateBodyNode.Threshold != null ? ToFloat(Visit(stateBodyNode.Threshold)) : TwinBehaviourCondition.DefaultThreshold;
        condition.TimeWindow = stateBodyNode.Window != null ? ToFloat(Visit(stateBodyNode.Window)) : 0.0f;
        // The game's scripts weigh every body by the inverse of its threshold
        condition.Weight = stateBodyNode.Weight != null ? ToFloat(Visit(stateBodyNode.Weight)) : TwinBehaviourCondition.DefaultWeight(condition.Threshold);
        condition.NotGate = stateBodyNode.IsNot;
        stateBody.RestartsState = stateBodyNode.Restart != null && (bool)Visit(stateBodyNode.Restart);
        stateBody.IsCompletionBody = stateBodyNode.Kind == StateBodyKind.Completion;
        
        return stateBody;
    }

    private Object VisitStateNode(IAgentLabTreeNode node)
    {
        var stateNode = (StateNode)node;
        var state = _options.State.Construct();

        foreach (var attribute in stateNode.Attributes.Children)
        {
            switch (attribute)
            {
                case InterruptingAttributeNode:
                    state.Interrupting = true;
                    break;
                case SkipFirstBodyAttributeNode:
                    state.HasCompletionBody = true;
                    break;
                case UseObjectSlotAttributeNode:
                    state.UsesObjectSlot = true;
                    state.BehaviourIndexOrSlot = (short)(ITwinBehaviourState.ObjectBehaviourSlots)Visit(attribute);
                    break;
                case ControlPacketAttributeNode:
                    state.ControlPacket = (TwinBehaviourControlPacket)Visit(attribute);
                    break;
            }
        }
        
        if (stateNode.BehaviourId is BehaviourReferenceNode reference && !state.UsesObjectSlot)
        {
            try
            {
                state.BehaviourIndexOrSlot = _options.Resolver.GetStateGraphResolver().ResolveGraphReference(reference.Reference);
            }
            catch (Exception e) when (e is not AgentLabSyntaxException)
            {
                throw new AgentLabSyntaxException($"Behaviour {reference.Reference} couldn't be resolved: {e.Message}", reference.Token.Line, reference.Token.Column, e);
            }
        }

        state.Bodies = (List<ITwinBehaviourStateBody>)Visit(stateNode.Bodies) ?? new List<ITwinBehaviourStateBody>();
        if (state.Bodies.Count > 0 && state.Bodies[0].IsCompletionBody)
        {
            state.HasCompletionBody = true;
        }
        
        return state;
    }

    private Object VisitControlPacketSettingNode(IAgentLabTreeNode node)
    {
        var settingNode = (ControlPacketSettingNode)node;
        var controlPacket = _memory.Get<TwinBehaviourControlPacket>(settingNode.ControlPacketOwner);
        var constNameNode = (ConstNode)settingNode.Assign.Left;
        var constSymbol = _symbolTable.Lookup(constNameNode.Name);
        if (constSymbol.Type.Name == _symbolTable.Lookup(nameof(AgentLabToken.TokenType.EnumType)).Name)
        {
            var enumSymbols = (AgentLabEnumSymbol)constSymbol;
            var enumValueNode = (ConstNode)settingNode.Assign.Right;
            var enumValueSymbol = enumSymbols.Enums.Lookup<AgentLabConstSymbol>(enumValueNode.Name);
            var enumType = _enumTypes[constNameNode.Name];
            var enumValue = Enum.Parse(enumType, enumValueSymbol.Name);
            controlPacket.GetType().GetProperty(settingNode.Name)!.SetValue(controlPacket, enumValue);
        }
        else
        {
            controlPacket.GetType().GetProperty(settingNode.Name)!.SetValue(controlPacket, Visit(settingNode.Assign.Right));
        }

        return null;
    }

    private Object VisitControlPacketDataNode(IAgentLabTreeNode node)
    {
        var dataNode = (ControlPacketDataNode)node;
        var controlPacket = _memory.Get<TwinBehaviourControlPacket>(dataNode.ControlPacketOwner);
        var dataEnum = Enum.Parse<TwinBehaviourControlPacket.ControlPacketData>(dataNode.Name);
        if (controlPacket.Bytes.Count <= (int)dataEnum)
        {
            var oldCount = controlPacket.Bytes.Count;
            for (var i = 0; i <= ((int)dataEnum - oldCount); i++)
            {
                controlPacket.Bytes.Add(0xFF);
            }
        }

        var dataValueNode = dataNode.Assign.Right;
        if (dataValueNode is ArrayNode arrayNode)
        {
            controlPacket.Bytes[(int)dataEnum] = (byte)((byte)(int)Visit(arrayNode.Index) + 0x80);
            return null;
        }

        controlPacket.Bytes[(int)dataEnum] = (byte)controlPacket.Floats.Count;
        if (TwinBehaviourControlPacket.IsIntegerPacket(dataEnum))
        {
            controlPacket.Floats.Add((uint)(int)Visit(dataValueNode));
            return null;
        }

        var dataValue = Visit(dataValueNode);
        switch (dataValue)
        {
            case float fDataValue:
                controlPacket.Floats.Add(BitConverter.SingleToUInt32Bits(fDataValue));
                break;
            case int iDataValue:
                controlPacket.Floats.Add(BitConverter.SingleToUInt32Bits(iDataValue));
                break;
        }
        
        
        return null;
    }

    private Object VisitControlPacketBodyNode(IAgentLabTreeNode node)
    {
        var controlPacketBody = (ControlPacketBodyNode)node;
        foreach (var dataNode in controlPacketBody.DataNodes)
        {
            Visit(dataNode);
        }

        foreach (var settingNode in controlPacketBody.SettingsNodes)
        {
            Visit(settingNode);
        }
        
        return null;
    }

    private Object VisitControlPacketNode(IAgentLabTreeNode node)
    {
        var controlPacketNode = (ControlPacketNode)node;
        var controlPacket = new TwinBehaviourControlPacket();
        _memory.Add(controlPacketNode.Name, controlPacket);

        Visit(controlPacketNode.Body);
        
        return null;
    }

    private Object VisitConstNode(IAgentLabTreeNode node)
    {
        var constNode = (ConstNode)node;
        return _memory.Get<object>(constNode.Name);
    }

    private Object VisitStringNode(IAgentLabTreeNode node)
    {
        var strNode = (StringNode)node;
        return strNode.Value;
    }

    private Object VisitNumberNode(IAgentLabTreeNode node)
    {
        var numNode = (NumberNode)node;
        return numNode.Value;
    }

    private Object VisitConstDeclarationNode(IAgentLabTreeNode node)
    {
        var constDecl = (ConstDeclarationNode)node;
        var constNode = (ConstNode)constDecl.Assign.Left;
        _memory.Add(constNode.Name, Visit(constDecl.Assign.Right));
        
        return null;
    }

    private Object VisitStarterAssignerNode(IAgentLabTreeNode node)
    {
        var assignerNode = (StarterAssignerNode)node;
        var starter = _result.Get<TwinBehaviourStarter>();
        var behaviour = _result.Get<ITwinBehaviourGraph>();
        var assigner = new TwinBehaviourAssigner();
        starter.Assigners.Add(assigner);
        if (starter.Assigners.Count == 1)
        {
            assigner.Behaviour = _options.Resolver.GetGraphResolver().ResolveGraphReference(behaviour.Name) + 1;
        }

        foreach (var child in assignerNode.Children)
        {
            Visit(child);
        }
        
        return null;
    }

    private Object VisitStarterAssignNode(IAgentLabTreeNode node)
    {
        var assign = (StarterAssignNode)node;
        var starter = _result.Get<TwinBehaviourStarter>();
        var assigner = starter.Assigners[^1];
        var constNameNode = (ConstNode)assign.Assign.Left;
        var constSymbol = _symbolTable.Lookup(constNameNode.Name);
        if (constSymbol.Type.Name == _symbolTable.Lookup(nameof(AgentLabToken.TokenType.EnumType)).Name)
        {
            var enumSymbols = (AgentLabEnumSymbol)constSymbol;
            var enumValueNode = (ConstNode)assign.Assign.Right;
            var enumValueSymbol = enumSymbols.Enums.Lookup<AgentLabConstSymbol>(enumValueNode.Name);
            var enumType = _enumTypes[constNameNode.Name];
            var enumValue = Enum.Parse(enumType, enumValueSymbol.Name);
            assigner.GetType().GetProperty(enumSymbols.Name)!.SetValue(assigner, enumValue);
        }
        else
        {
            // GlobalObjectId is the old name, when the index still got resolved as an object
            var value = Visit(assign.Assign.Right);
            if (value is string stringValue)
            {
                assigner.RefListIndex = string.IsNullOrEmpty(stringValue) ? TwinBehaviourAssigner.NoRefListIndex : _options.Resolver.GetObjectIdResolver().ResolveGlobalObjectId(stringValue);
            }
            else
            {
                assigner.RefListIndex = (ushort)ToInt(value);
            }
        }
        
        return null;
    }

    private Object VisitListNode(IAgentLabTreeNode node)
    {
        var list = (IAgentLabListNode)node;
        foreach (var child in list.Children)
        {
            Visit(child);
        }
        
        return null;
    }

    private Object VisitStarterNode(IAgentLabTreeNode node)
    {
        var behaviourStarter = new TwinBehaviourStarter
        {
            Priority = _result.Get<ITwinBehaviourGraph>().Priority
        };
        _result.Add(behaviourStarter);
        
        var starter = (StarterNode)node;
        Visit(starter.Body);
        
        return null;
    }

    private Object VisitStartFromAttributeNode(IAgentLabTreeNode node)
    {
        var startFrom = (StartFromAttributeNode)node;
        var stateSymbol = _symbolTable.Lookup<AgentLabStateSymbol>(startFrom.StateName);
        var behaviour = _result.Get<ITwinBehaviourGraph>();
        behaviour.StartState = stateSymbol.Id;
        
        return null;
    }

    private Object VisitPriorityAttributeNode(IAgentLabTreeNode node)
    {
        var priority = (PriorityAttributeNode)node;
        var result = (byte)(int)Visit(priority.Priority);
        var behaviour = _result.Get<ITwinBehaviourGraph>();
        behaviour.Priority = result;
        
        return null;
    }
    
    private Object VisitGraphPriorityAttributeNode(IAgentLabTreeNode node)
    {
        var priority = (GraphPriorityAttributeNode)node;
        return (byte)(int)Visit(priority.Priority);
    }

    private Object VisitBehaviourBodyNode(IAgentLabTreeNode node)
    {
        var behaviourBody = (BehaviourBodyNode)node;
        Visit(behaviourBody.Consts);
        Visit(behaviourBody.Starter);
        Visit(behaviourBody.ControlPackets);
        var states = (List<ITwinBehaviourState>)Visit(behaviourBody.States) ?? new List<ITwinBehaviourState>();
        return states;
    }

    private Object VisitBehaviourNode(IAgentLabTreeNode node)
    {
        var compiledBehaviour = _options.Graph.Construct();
        _result.Add(compiledBehaviour);
        var behaviour = (BehaviourNode)node;
        compiledBehaviour.Name = behaviour.Name;
        var behaviourScope = _symbolTable.Lookup<AgentLabBehaviourSymbol>(behaviour.Name);
        _symbolTable = behaviourScope.BehaviourSymbolTable;
        Visit(behaviour.Priority);
        compiledBehaviour.ScriptStates = (List<ITwinBehaviourState>)Visit(behaviour.Body) ?? new List<ITwinBehaviourState>();
        Visit(behaviour.StartFrom);
        // With a starter the Priority attribute is the starter's, the graph keeps its own separately
        if (_result.Contains<TwinBehaviourStarter>())
        {
            compiledBehaviour.Priority = behaviour.GraphPriority != null ? (byte)Visit(behaviour.GraphPriority) : (byte)0;
        }
        
        return null;
    }
}