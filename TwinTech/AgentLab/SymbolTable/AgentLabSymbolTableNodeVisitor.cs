using System;
using System.Collections.Generic;
using System.Linq;
using Twinsanity.AgentLab.AbstractSyntaxTree;
using Twinsanity.AgentLab.AbstractSyntaxTree.Attributes;
using Twinsanity.AgentLab.AbstractSyntaxTree.ControlPacket;

namespace Twinsanity.AgentLab.SymbolTable;

internal class AgentLabSymbolTableNodeVisitor : NodeVisitor
{
    public AgentLabSymbolTable SymbolTable { get; private set; }
    
    public AgentLabSymbolTableNodeVisitor()
    {
        SymbolTable = new AgentLabSymbolTable();
        Visitors.Add(typeof(ConstNode), VisitConst);
        Visitors.Add(typeof(ArrayNode), VisitArrayNode);
        Visitors.Add(typeof(BehaviourNode), VisitBehaviour);
        Visitors.Add(typeof(BehaviourBodyNode), VisitBehaviourBody);
        Visitors.Add(typeof(ConstListNode), VisitConstList);
        Visitors.Add(typeof(AssignNode), VisitAssign);
        Visitors.Add(typeof(ConstDeclarationNode), VisitConstDeclaration);
        Visitors.Add(typeof(UnaryOperationNode), VisitUnaryOperationNode);
        Visitors.Add(typeof(BinaryOperationNode), VisitBinaryOperation);
        Visitors.Add(typeof(NumberNode), VisitNumber);
        Visitors.Add(typeof(StringNode), VisitString);
        Visitors.Add(typeof(BooleanNode), VisitBoolean);
        Visitors.Add(typeof(StateListNode), VisitStateList);
        Visitors.Add(typeof(StateNode), VisitState);
        Visitors.Add(typeof(StateBodyNode), VisitStateBodyNode);
        Visitors.Add(typeof(StateBodyListNode), VisitStateBodyListNode);
        Visitors.Add(typeof(ActionListNode), VisitActionListNode);
        Visitors.Add(typeof(ActionNode), VisitActionNode);
        Visitors.Add(typeof(ParamListNode), VisitParamListNode);
        Visitors.Add(typeof(ParamNode), VisitParamNode);
        Visitors.Add(typeof(ConditionNode), VisitConditionNode);
        Visitors.Add(typeof(AttributeListNode), VisitAttributeListNode);
        Visitors.Add(typeof(PriorityAttributeNode), VisitPriorityAttributeNode);
        Visitors.Add(typeof(StartFromAttributeNode), VisitStartFromAttributeNode);
        Visitors.Add(typeof(UseObjectSlotAttributeNode), VisitUseObjectSlotAttribute);
        Visitors.Add(typeof(ObjectSlotNameNode), VisitObjectSlotName);
        Visitors.Add(typeof(ControlPacketAttributeNode), VisitControlPacketAttribute);
        Visitors.Add(typeof(NonBlockingAttributeNode), VisitNoop);
        Visitors.Add(typeof(SkipFirstBodyAttributeNode), VisitNoop);
        Visitors.Add(typeof(StateExecuteNode), VisitStateExecute);
        Visitors.Add(typeof(ControlPacketListNode), VisitControlPacketList);
        Visitors.Add(typeof(ControlPacketNode), VisitControlPacket);
        Visitors.Add(typeof(ControlPacketBodyNode), VisitControlPacketBodyNode);
        Visitors.Add(typeof(ControlPacketDataNode), VisitControlPacketDataNode);
        Visitors.Add(typeof(ControlPacketSettingNode), VisitControlPacketSettingNode);
        Visitors.Add(typeof(BehaviourLibraryNode), VisitBehaviourLibraryNode);
        Visitors.Add(typeof(GlobalIndexAttributeNode), VisitGlobalIndexAttributeNode);
        Visitors.Add(typeof(InstanceTypeAttributeNode), VisitInstanceTypeAttributeNode);
        Visitors.Add(typeof(LinearBehaviourListNode), VisitLinearBehaviourListNode);
        Visitors.Add(typeof(LinearBehaviourNode), VisitLinearBehaviourNode);
        Visitors.Add(typeof(ParamDefinitionNode), VisitParamDefinitionNode);
        Visitors.Add(typeof(ParamDefinitionListNode), VisitParamDefinitionListNode);
        Visitors.Add(typeof(ActionDefinitionListNode), VisitActionDefinitionListNode);
        Visitors.Add(typeof(ActionDefinitionNode), VisitActionDefinitionNode);
        Visitors.Add(typeof(ConditionDefinitionListNode), VisitConditionDefinitionListNode);
        Visitors.Add(typeof(ConditionDefinitionNode), VisitConditionDefinitionNode);
        Visitors.Add(typeof(AliasNode), VisitAlias);
    }

    private Object VisitStartFromAttributeNode(IAgentLabTreeNode node)
    {
        var startFromAttribute = (StartFromAttributeNode)node;
        var stateSymbol = SymbolTable.Lookup(startFromAttribute.StateName);
        if (stateSymbol == null)
        {
            // TODO: Raise undefined error instead of throwing an exception
            throw new Exception($"Undefined state {startFromAttribute.StateName}");
        }

        AssertType(SymbolTable.Lookup(nameof(AgentLabToken.TokenType.State)), stateSymbol.Type);
        
        return null;
    }

    private Object VisitPriorityAttributeNode(IAgentLabTreeNode node)
    {
        var priority = (PriorityAttributeNode)node;
        var type = Visit(priority.Priority) as AgentLabSymbol;
        AssertType(SymbolTable.Lookup(nameof(AgentLabToken.TokenType.IntegerType)), type);
        
        return null;
    }

    private Object VisitAttributeListNode(IAgentLabTreeNode node)
    {
        var attributes = (AttributeListNode)node;
        foreach (var attribute in attributes.Children)
        {
            Visit(attribute);
        }
        return null;
    }

    private Object VisitStateBodyListNode(IAgentLabTreeNode node)
    {
        var stateBodyList = (StateBodyListNode)node;
        
        foreach (var body in stateBodyList.Children)
        {
            Visit(body);
        }
        
        return null;
    }

    private Object VisitAlias(IAgentLabTreeNode node)
    {
        var alias = (AliasNode)node;
        return alias.Name;
    }

    private Object VisitConditionDefinitionNode(IAgentLabTreeNode node)
    {
        var condDef = (ConditionDefinitionNode)node;

        if (SymbolTable.Lookup(condDef.Name) != null)
        {
            // TODO: Raise redefinition error instead of throwing an exception
            throw new Exception($"Condition {condDef.Name} redefinition!");
        }
        
        var conditionSymbol = new AgentLabConditionSymbol(condDef.Name, SymbolTable.Lookup(nameof(AgentLabToken.TokenType.Condition)),
            SymbolTable.Lookup(nameof(AgentLabToken.TokenType.FloatType)), Visit(condDef.Parameter) as AgentLabSymbol);
        SymbolTable.Define(conditionSymbol);

        AddAliasesToCondition(conditionSymbol, condDef.Aliases as IAgentLabListNode);
        
        return null;
    }

    private void AddAliasesToCondition(AgentLabConditionSymbol condition, IAgentLabListNode aliases)
    {
        if (aliases == null)
        {
            return;
        }
        
        foreach (var aliasNode in aliases.Children)
        {
            var alias = (string)Visit(aliasNode);
            if (SymbolTable.Lookup(alias) != null)
            {
                // TODO: Raise redefinition error instead of throwing an exception
                throw new Exception($"Alias already defined {alias}");
            }
            
            SymbolTable.Define(new AgentLabConditionSymbol(alias, SymbolTable.Lookup(nameof(AgentLabToken.TokenType.Condition)), SymbolTable.Lookup(nameof(AgentLabToken.TokenType.FloatType)), condition.ParameterType));
        }
    }

    private Object VisitConditionDefinitionListNode(IAgentLabTreeNode node)
    {
        var condDefList = (ConditionDefinitionListNode)node;

        foreach (var condDef in condDefList.Children)
        {
            Visit(condDef);
        }
        
        return null;
    }

    private Object VisitParamDefinitionNode(IAgentLabTreeNode node)
    {
        var paramDef = (ParamDefinitionNode)node;
        return new AgentLabParamSymbol(paramDef.Name, SymbolTable.Lookup(paramDef.Type.Type.ToString()));
    }

    private Object VisitParamDefinitionListNode(IAgentLabTreeNode node)
    {
        var paramDefinitions = (ParamDefinitionListNode)node;

        return paramDefinitions.Children.Select(param => (AgentLabSymbol)Visit(param)).ToArray();
    }
    
    private void AddAliasesToAction(AgentLabActionSymbol action, IAgentLabListNode aliases)
    {
        if (aliases == null)
        {
            return;
        }
        
        foreach (var aliasNode in aliases.Children)
        {
            var alias = (string)Visit(aliasNode);
            if (SymbolTable.Lookup(alias) != null)
            {
                // TODO: Raise redefinition error instead of throwing an exception
                throw new Exception($"Alias already defined {alias} for this or another action");
            }
            
            var actionCopy = new AgentLabActionSymbol(alias, SymbolTable.Lookup(nameof(AgentLabToken.TokenType.Action)))
            {
                Parameters = action.Parameters
            };
            SymbolTable.Define(new AgentLabActionSymbol(alias, actionCopy));
        }
    }

    private Object VisitActionDefinitionNode(IAgentLabTreeNode node)
    {
        var actionDef = (ActionDefinitionNode)node;
        if (SymbolTable.Lookup(actionDef.Name) != null)
        {
            // TODO: Raise redefinition error instead of throwing an exception
            throw new Exception($"Action {actionDef.Name} redefinition!");
        }
        
        var parameters = Visit(actionDef.Parameters) as AgentLabSymbol[];
        var actionSymbol = new AgentLabActionSymbol(actionDef.Name, SymbolTable.Lookup(nameof(AgentLabToken.TokenType.Action)), parameters);
        SymbolTable.Define(actionSymbol);

        AddAliasesToAction(actionSymbol, actionDef.Aliases as IAgentLabListNode);
        
        return null;
    }

    private Object VisitActionDefinitionListNode(IAgentLabTreeNode node)
    {
        var actionDefList = (ActionDefinitionListNode)node;
        foreach (var actionDef in actionDefList.Children)
        {
            Visit(actionDef);
        }
        
        return null;
    }

    private Object VisitParamNode(IAgentLabTreeNode node)
    {
        var param = (ParamNode)node;
        var symbolType = Visit(param.Value) as AgentLabSymbol;
        
        return symbolType;
    }

    private Object VisitParamListNode(IAgentLabTreeNode node)
    {
        var paramList = (ParamListNode)node;

        return paramList.Children.Select(param => Visit(param) as AgentLabSymbol).ToList();
    }

    private Object VisitConditionNode(IAgentLabTreeNode node)
    {
        var condition = (ConditionNode)node;
        var symbol = SymbolTable.Lookup(condition.Name);
        if (symbol == null)
        {
            // TODO: Raise undefined condition error instead of throwing an exception
            throw new Exception($"Undefined condition call {condition.Name}");
        }
        
        AssertType(SymbolTable.Lookup(nameof(AgentLabToken.TokenType.Condition)), symbol.Type);

        Visit(condition.Number);

        return null;
    }

    private Object VisitActionNode(IAgentLabTreeNode node)
    {
        var action = (ActionNode)node;
        var symbol = SymbolTable.Lookup(action.Name);
        if (symbol == null)
        {
            // TODO: Raise undefined action error instead of throwing an exception
            throw new Exception($"Undefined action call {action.Name}");
        }
        
        AssertType(SymbolTable.Lookup(nameof(AgentLabToken.TokenType.Action)), symbol.Type);

        var actionSymbol = (AgentLabActionSymbol)symbol;
        var expectedSymbols = actionSymbol.Parameters?.GetAllSymbols().ToList();
        var expectedArguments = expectedSymbols?.Count ?? 0;
        var argumentsProvided = action.Parameters == null ? 0 : action.Parameters.Children.Count;
        if (expectedArguments != argumentsProvided)
        {
            // TODO: Raise insufficient arguments error instead of throwing an exception
            throw new Exception($"Expected {expectedArguments} parameters for {action.Name} but got {argumentsProvided}");
        }

        var parameterTypes = Visit(action.Parameters) as List<AgentLabSymbol>;
        if (parameterTypes?.Count == 0)
        {
            return null;
        }
        
        for (var i = 0; i < argumentsProvided; i++)
        {
            var providedType = parameterTypes![i];
            var expectedArgument = expectedSymbols![i];
            AssertType(expectedArgument.Type, providedType);
        }

        return null;
    }

    private Object VisitActionListNode(IAgentLabTreeNode node)
    {
        var actionList = (ActionListNode)node;
        foreach (var action in actionList.Children)
        {
            Visit(action);
        }
        
        return null;
    }

    private Object VisitStateBodyNode(IAgentLabTreeNode node)
    {
        var stateBody = (StateBodyNode)node;
        Visit(stateBody.Interval);
        Visit(stateBody.Threshold);
        Visit(stateBody.Condition);
        Visit(stateBody.ActionList);
        Visit(stateBody.StateExecute);

        return null;
    }

    private Object VisitInstanceTypeAttributeNode(IAgentLabTreeNode node)
    {
        var instanceTypeAttrib = (InstanceTypeAttributeNode)node;
        var instanceTypesEnumTable = ((AgentLabEnumSymbol)SymbolTable.Lookup("InstanceType")).Enums;
        var symbol = instanceTypesEnumTable.Lookup(instanceTypeAttrib.Key.Name);
        if (symbol == null)
        {
            // TODO: Raise undefined error instead of throwing an exception
            throw new Exception($"Undefined instance type {instanceTypeAttrib.Key.Name}!");
        }

        return null;
    }

    private Object VisitGlobalIndexAttributeNode(IAgentLabTreeNode node)
    {
        var globalIndexAttrib = (GlobalIndexAttributeNode)node;
        var numberType = Visit(globalIndexAttrib.Index) as AgentLabSymbol;
        if (numberType != SymbolTable.Lookup(nameof(AgentLabToken.TokenType.IntegerType)))
        {
            // TODO: Raise type error instead of throwing an exception
            throw new Exception($"Expected integer got {numberType}");
        }

        return null;
    }

    private Object VisitLinearBehaviourListNode(IAgentLabTreeNode node)
    {
        var linearBehaviourList = (LinearBehaviourListNode)node;
        foreach (var linearBehaviour in linearBehaviourList.Children)
        {
            Visit(linearBehaviour);
        }

        return null;
    }

    private Object VisitBehaviourLibraryNode(IAgentLabTreeNode node)
    {
        var behaviourLibrary = (BehaviourLibraryNode)node;
        if (SymbolTable.Lookup(behaviourLibrary.Name) != null)
        {
            // TODO: Raise redefinition error instead of throwing an exception
            throw new Exception($"Behaviour library {behaviourLibrary.Name} redefinition!");
        }

        var librarySymbol = new AgentLabBehaviourLibrarySymbol(behaviourLibrary.Name, SymbolTable);
        var oldSymbolTable = SymbolTable;
        SymbolTable = librarySymbol.BehaviourLibrarySymbolTable;
        Visit(behaviourLibrary.GlobalIndex);
        Visit(behaviourLibrary.InstanceType);
        Visit(behaviourLibrary.LinearBehaviours);
        Visit(behaviourLibrary.CreationAction);
        SymbolTable = oldSymbolTable;
        
        return null;
    }

    private Object VisitLinearBehaviourNode(IAgentLabTreeNode node)
    {
        var linearBehaviour = (LinearBehaviourNode)node;
        if (SymbolTable.Lookup(linearBehaviour.Name) != null)
        {
            // TODO: Raise redefinition error instead of throwing an exception
            throw new Exception($"Behaviour {linearBehaviour.Name} redefinition!");
        }
        
        SymbolTable.Define(new AgentLabLinearBehaviourSymbol(linearBehaviour.Name));
        Visit(linearBehaviour.Actions);
        
        return null;
    }

    private Object VisitBoolean(IAgentLabTreeNode node)
    {
        return SymbolTable.Lookup(nameof(AgentLabToken.TokenType.BooleanType));
    }

    private Object VisitString(IAgentLabTreeNode node)
    {
        return SymbolTable.Lookup(nameof(AgentLabToken.TokenType.StringType));
    }

    private Object VisitControlPacketAttribute(IAgentLabTreeNode node)
    {
        var controlPacketAttribute = (ControlPacketAttributeNode)node;
        var controlPacketSymbol = SymbolTable.Lookup(controlPacketAttribute.Token.GetValue<string>());
        if (controlPacketSymbol == null)
        {
            // TODO: Raise undefined identifier error instead of throwing an exception
            throw new Exception($"Undefined ControlPacket {controlPacketAttribute.Token}");
        }
        
        return null;
    }

    private Object VisitObjectSlotName(IAgentLabTreeNode node)
    {
        var objectSlotName = (ObjectSlotNameNode)node;
        var slotsEnumTable = ((AgentLabEnumSymbol)SymbolTable.Lookup("ObjectBehaviourSlot")).Enums;
        var slotNameId = slotsEnumTable.Lookup(objectSlotName.SlotName);
        if (slotNameId == null)
        {
            // TODO: Raise undefined identifier error instead of throwing an exception
            throw new Exception($"Undefined object slot name {objectSlotName.SlotName}");
        }
        
        return null;
    }

    private Object VisitUseObjectSlotAttribute(IAgentLabTreeNode node)
    {
        var useObjectSlotAttribute = (UseObjectSlotAttributeNode)node;
        Visit(useObjectSlotAttribute.SlotName);
        return null;
    }

    private Object VisitNoop(IAgentLabTreeNode node)
    {
        return null;
    }
    
    private Object VisitControlPacketSettingNode(IAgentLabTreeNode node)
    {
        var settingNode = (ControlPacketSettingNode)node;
        if (SymbolTable.Lookup(settingNode.Name) == null)
        {
            throw new Exception($"Undefined control packet setting {settingNode.Name}");
        }

        Visit(settingNode.Assign);
        
        return null;
    }
    
    private Object VisitControlPacketDataNode(IAgentLabTreeNode node)
    {
        var dataNode = (ControlPacketDataNode)node;
        if (SymbolTable.Lookup(dataNode.Name) == null)
        {
            // TODO: Raise undefined data member error instead of throwing an exception
            throw new Exception($"Undefined control packet data {dataNode.Name}");
        }

        Visit(dataNode.Assign);
        
        return null;
    }
    
    private Object VisitControlPacketBodyNode(IAgentLabTreeNode node)
    {
        var packetBody = (ControlPacketBodyNode)node;
        foreach (var dataNode in packetBody.DataNodes)
        {
            Visit(dataNode);
        }

        foreach (var settingNode in packetBody.SettingsNodes)
        {
            Visit(settingNode);
        }
        
        return null;
    }

    private Object VisitControlPacket(IAgentLabTreeNode node)
    {
        var controlPacket = (ControlPacketNode)node;

        if (SymbolTable.Lookup(controlPacket.Token.GetValue<string>()) != null)
        {
            // TODO: Raise redefinition error instead of throwing an exception
            throw new Exception($"Control packet {controlPacket.Token} redefinition!");
        }
        
        var symbol = new AgentLabControlPacketSymbol(controlPacket.Token.GetValue<string>())
        {
            Type = SymbolTable.Lookup(nameof(AgentLabToken.TokenType.ControlPacket))
        };
        SymbolTable.Define(symbol);
        Visit(controlPacket.Body);
        return null;
    }

    private Object VisitControlPacketList(IAgentLabTreeNode node)
    {
        var controlPackets = (ControlPacketListNode)node;
        foreach (var controlPacket in controlPackets.Children)
        {
            Visit(controlPacket);
        }
        return null;
    }

    private Object VisitStateExecute(IAgentLabTreeNode node)
    {
        var executeNode = (StateExecuteNode)node;
        var stateSymbol = SymbolTable.Lookup(executeNode.StateName);
        if (stateSymbol == null)
        {
            // TODO: Raise undefined identifier error instead of throwing an exception
            throw new Exception($"Undefined state {executeNode.StateName}");
        }

        AssertType(SymbolTable.Lookup(nameof(AgentLabToken.TokenType.State)), stateSymbol.Type);
        
        return null;
    }

    private Object VisitState(IAgentLabTreeNode node)
    {
        var state = (StateNode)node;

        if (SymbolTable.Lookup(state.Name) != null)
        {
            // TODO: Raise redefinition error instead of throwing an exception
            throw new Exception($"State {state.Name} redefinition!");
        }
        
        var symbol = new AgentLabStateSymbol(state.Name)
        {
            Type = SymbolTable.Lookup(nameof(AgentLabToken.TokenType.State))
        };
        SymbolTable.Define(symbol);
        DeferredVisit(state.Bodies);
        DeferredVisit(state.Attributes);
        return null;
    }

    private Object VisitStateList(IAgentLabTreeNode node)
    {
        var states = (StateListNode)node;
        foreach (var state in states.Children)
        {
            Visit(state);
        }
        
        DoDeferredVisits();
        
        return null;
    }

    private Object VisitNumber(IAgentLabTreeNode node)
    {
        var numberNode = (NumberNode)node;
        return numberNode.Token.Type switch
        {
            AgentLabToken.TokenType.Integer => SymbolTable.Lookup(nameof(AgentLabToken.TokenType.IntegerType)),
            AgentLabToken.TokenType.FloatingPoint => SymbolTable.Lookup(nameof(AgentLabToken.TokenType.FloatType)),
            _ => null
        };
    }
    
    private Object VisitUnaryOperationNode(IAgentLabTreeNode node)
    {
        var unaryOp = (UnaryOperationNode)node;
        var type = Visit(unaryOp.Expression) as AgentLabSymbol;
        if (type != SymbolTable.Lookup(nameof(AgentLabToken.TokenType.IntegerType)) && type != SymbolTable.Lookup(nameof(AgentLabToken.TokenType.FloatType)))
        {
            // TODO: Raise type error instead of throwing an exception
            throw new Exception($"Unary operation for {type} are not supported");
        }
        
        return type;
    }

    private Object VisitBinaryOperation(IAgentLabTreeNode node)
    {
        var binOp = (BinaryOperationNode)node;
        var left = Visit(binOp.Left) as AgentLabSymbol;
        var right = Visit(binOp.Right) as AgentLabSymbol;
        var stringSymbol = SymbolTable.Lookup(nameof(AgentLabToken.TokenType.StringType));
        var booleanSymbol = SymbolTable.Lookup(nameof(AgentLabToken.TokenType.BooleanType));
        var floatSymbol = SymbolTable.Lookup(nameof(AgentLabToken.TokenType.FloatType));
        if ((left == stringSymbol && right != stringSymbol) || (left != stringSymbol && right == stringSymbol))
        {
            // TODO: Raise type error instead of throwing an exception
            throw new Exception($"Binary operation {left} and {right} are not supported");
        }

        if (left == booleanSymbol || right == booleanSymbol)
        {
            // TODO: Raise type error instead of throwing an exception
            throw new Exception("No binary operation for booleans are supported");
        }

        if (left == stringSymbol && right == stringSymbol)
        {
            if (binOp.Token.Type != AgentLabToken.TokenType.AddOperator)
            {
                // TODO: Raise type error instead of throwing an exception
                throw new Exception($"Binary operation {binOp.Token.Type} for {left} and {right} are not supported");
            }
            
            return stringSymbol;
        }
        
        if (left == floatSymbol || right == floatSymbol)
        {
            return floatSymbol;
        }
        
        return SymbolTable.Lookup(nameof(AgentLabToken.TokenType.IntegerType));
    }

    private Object VisitConstDeclaration(IAgentLabTreeNode node)
    {
        var constDecl = (ConstDeclarationNode)node;
        var constNode = (ConstNode)constDecl.Assign.Left;
        var type = SymbolTable.Lookup(constNode.Type.ToString());
        SymbolTable.Define(new AgentLabConstSymbol(constNode.Name, type));
        
        return null;
    }

    private Object VisitAssign(IAgentLabTreeNode node)
    {
        var assign = (AssignNode)node;
        var leftSymbol = Visit(assign.Left) as AgentLabSymbol;
        var integerSymbol = SymbolTable.Lookup(nameof(AgentLabToken.TokenType.IntegerType));
        var floatSymbol = SymbolTable.Lookup(nameof(AgentLabToken.TokenType.FloatType));
        
        // No, this is not a hack, this is going into Enum symbol scope :^)
        var oldSymbolTable = SymbolTable;
        if (leftSymbol != null && leftSymbol == SymbolTable.Lookup(nameof(AgentLabToken.TokenType.EnumType)))
        {
            leftSymbol = SymbolTable.Lookup(((ConstNode)assign.Left).Name);
            SymbolTable = GetEnumSymbolTable((ConstNode)assign.Left);
        }
        var rightSymbol = Visit(assign.Right) as AgentLabSymbol;
        SymbolTable = oldSymbolTable;

        // Can assign integers to floats just fine!
        if (leftSymbol == floatSymbol && rightSymbol == integerSymbol)
        {
            return null;
        }
        
        AssertType(leftSymbol, rightSymbol);
        
        return null;
    }

    private AgentLabSymbolTable GetEnumSymbolTable(ConstNode node)
    {
        var symbol = SymbolTable.Lookup(node.Name) as AgentLabEnumSymbol;
        return symbol?.Enums;
    }

    private Object VisitConstList(IAgentLabTreeNode node)
    {
        var consts = (ConstListNode)node;
        foreach (var constDeclNode in consts.Children.Cast<ConstDeclarationNode>())
        {
            Visit(constDeclNode);
        }
        DetermineConstTypes(consts);
        return null;
    }

    private void DetermineConstTypes(ConstListNode consts)
    {
        foreach (var constDeclNode in consts.Children.Cast<ConstDeclarationNode>())
        {
            var assign = constDeclNode.Assign;
            var constNode = (ConstNode)assign.Left;
            var symbol = SymbolTable.Lookup(constNode.Name);
            var type = Visit(assign.Right) as AgentLabSymbol;
            symbol.Type = type;
        }
    }
    
    private Object VisitBehaviourBody(IAgentLabTreeNode node)
    {
        var behaviourBody = (BehaviourBodyNode)node;
        // Define consts then define control packets then define states in that exact order
        Visit(behaviourBody.Consts);
        Visit(behaviourBody.ControlPackets);
        Visit(behaviourBody.States);
        return null;
    }

    private Object VisitBehaviour(IAgentLabTreeNode node)
    {
        var behaviour = (BehaviourNode)node;
        if (SymbolTable.Lookup(behaviour.Name) != null)
        {
            // TODO: Raise redefinition error instead of throwing an exception
            throw new Exception($"Behaviour {behaviour.Name} redefinition!");
        }
        
        var behaviourSymbol = new AgentLabBehaviourSymbol(behaviour.Name, SymbolTable);
        SymbolTable.Define(behaviourSymbol);
        var oldSymbolTable = SymbolTable;
        SymbolTable = behaviourSymbol.BehaviourSymbolTable;
        Visit(behaviour.Body);
        Visit(behaviour.Priority);
        Visit(behaviour.StartFrom);
        SymbolTable = oldSymbolTable;
        return null;
    }
    
    private Object VisitArrayNode(IAgentLabTreeNode node)
    {
        var array = (ArrayNode)node;
        var arraySymbol = SymbolTable.Lookup(array.Name) as AgentLabArraySymbol;
        if (arraySymbol == null)
        {
            // TODO: Raise undefined error instead of throwing an exception
            throw new Exception($"Undefined array {array.Name}");
        }

        // Const types should be determined by this point
        var indexType = Visit(array.Index) as AgentLabSymbol;
        AssertType(SymbolTable.Lookup(nameof(AgentLabToken.TokenType.IntegerType)), indexType);
        
        return arraySymbol.StorageType;
    }

    private Object VisitConst(IAgentLabTreeNode node)
    {
        var constNode = (ConstNode)node;
        var constSymbol = SymbolTable.Lookup(constNode.Name);
        if (constSymbol == null)
        {
            // TODO: Raise undefined error instead of throwing an exception
            throw new Exception($"Undefined const {constNode.Name}!");
        }
        
        return constSymbol.Type;
    }

    private void AssertType(AgentLabSymbol expectedType, AgentLabSymbol actualType)
    {
        if (expectedType.Name != actualType.Name)
        {
            // TODO: Raise type error instead of throwing an exception
            throw new Exception($"Expected {expectedType.Name} but got {actualType.Name}");
        }
    }
}