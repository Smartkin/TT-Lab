using System;
using System.Collections.Generic;
using System.Linq;
using Twinsanity.AgentLab.AbstractSyntaxTree;
using Twinsanity.AgentLab.AbstractSyntaxTree.Attributes;
using Twinsanity.AgentLab.AbstractSyntaxTree.ControlPacket;

namespace Twinsanity.AgentLab.SymbolTable;

internal class AgentLabSymbolTableNodeVisitor : NodeVisitor
{
    public AgentLabSymbolTable SymbolTable { get; }
    
    public AgentLabSymbolTableNodeVisitor()
    {
        SymbolTable = new AgentLabSymbolTable();
        Visitors.Add(typeof(ConstNode), VisitConst);
        Visitors.Add(typeof(BehaviourNode), VisitBehaviour);
        Visitors.Add(typeof(BehaviourBodyNode), VisitBehaviourBody);
        Visitors.Add(typeof(ConstListNode), VisitConstList);
        Visitors.Add(typeof(AssignNode), VisitAssign);
        Visitors.Add(typeof(ConstDeclarationNode), VisitConstDeclaration);
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
        Visitors.Add(typeof(UseObjectSlotAttributeNode), VisitUseObjectSlotAttribute);
        Visitors.Add(typeof(ObjectSlotNameNode), VisitObjectSlotName);
        Visitors.Add(typeof(ControlPacketAttributeNode), VisitControlPacketAttribute);
        Visitors.Add(typeof(NonBlockingAttributeNode), VisitNoop);
        Visitors.Add(typeof(SkipFirstBodyAttributeNode), VisitNoop);
        Visitors.Add(typeof(StateExecuteNode), VisitStateExecute);
        Visitors.Add(typeof(ControlPacketListNode), VisitControlPacketList);
        Visitors.Add(typeof(ControlPacketNode), VisitControlPacket);
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
                throw new Exception($"Alias already defined {alias}");
            }
            
            SymbolTable.Define(new AgentLabActionSymbol(alias, SymbolTable.Lookup(nameof(AgentLabToken.TokenType.Action)), action.Parameters.ToArray()));
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
        // TODO: Do type checking after boolean types for parameters are introduced
        Visit(param.Value);
        
        return null;
    }

    private Object VisitParamListNode(IAgentLabTreeNode node)
    {
        var paramList = (ParamListNode)node;
        foreach (var param in paramList.Children)
        {
            Visit(param);
        }
        
        return null;
    }

    private Object VisitConditionNode(IAgentLabTreeNode node)
    {
        var condition = (ConditionNode)node;
        var symbol = SymbolTable.Lookup(condition.Value);
        if (symbol == null)
        {
            // TODO: Raise undefined condition error instead of throwing an exception
            throw new Exception($"Undefined condition call {condition.Value}");
        }
        else if (symbol.Type.Name != nameof(AgentLabToken.TokenType.Condition))
        {
            // TODO: Raise type error instead of throwing an exception
            throw new Exception($"Expected condition call got {symbol.Type}");
        }

        Visit(condition.Number);

        return null;
    }

    private Object VisitActionNode(IAgentLabTreeNode node)
    {
        var action = (ActionNode)node;
        var symbol = SymbolTable.Lookup(action.Value);
        if (symbol == null)
        {
            // TODO: Raise undefined action error instead of throwing an exception
            throw new Exception($"Undefined action call {action.Value}");
        }
        else if (symbol.Type.Name != nameof(AgentLabToken.TokenType.Action))
        {
            // TODO: Raise type error instead of throwing an exception
            throw new Exception($"Expected action call got {symbol.Type}");
        }

        Visit(action.Parameters);

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
        var symbol = SymbolTable.Lookup(instanceTypeAttrib.Key.Value);
        if (symbol == null)
        {
            // TODO: Raise undefined error instead of throwing an exception
            throw new Exception($"Undefined instance type: {instanceTypeAttrib.Key.Value}!");
        }

        var symbolType = SymbolTable.Lookup(symbol.Type.Name);
        // TODO: Verify enum type

        return null;
    }

    private Object VisitGlobalIndexAttributeNode(IAgentLabTreeNode node)
    {
        var globalIndexAttrib = (GlobalIndexAttributeNode)node;
        var numberType = (AgentLabToken.TokenType)Visit(globalIndexAttrib.Index);
        if (numberType != AgentLabToken.TokenType.IntegerType)
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
        Visit(behaviourLibrary.GlobalIndex);
        Visit(behaviourLibrary.InstanceType);
        Visit(behaviourLibrary.LinearBehaviours);
        Visit(behaviourLibrary.CreationAction);

        return null;
    }

    private Object VisitLinearBehaviourNode(IAgentLabTreeNode node)
    {
        var linearBehaviour = (LinearBehaviourNode)node;
        if (SymbolTable.Lookup(linearBehaviour.Name) != null)
        {
            // TODO: Raise redefinition error instead of throwing an exception
            throw new Exception($"Redefinition of behaviour {linearBehaviour.Name}!");
        }
        
        SymbolTable.Define(new AgentLabBehaviourSymbol(linearBehaviour.Name));
        Visit(linearBehaviour.Actions);
        
        return null;
    }

    private Object VisitBoolean(IAgentLabTreeNode node)
    {
        return AgentLabToken.TokenType.BooleanType;
    }

    private Object VisitString(IAgentLabTreeNode node)
    {
        return AgentLabToken.TokenType.StringType;
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
        var slotNameId = SymbolTable.Lookup(objectSlotName.SlotName);
        if (slotNameId == null)
        {
            // TODO: Raise undefined identifier error instead of throwing an exception
            throw new Exception($"Undefined object slot name {objectSlotName.SlotName}");
        }
        
        var slotType = SymbolTable.Lookup(slotNameId.Type.Name);
        // TODO: Verify enum type
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
        Visit(state.Bodies);
        foreach (var attribute in state.Attributes)
        {
            Visit(attribute);
        }
        return null;
    }

    private Object VisitStateList(IAgentLabTreeNode node)
    {
        var states = (StateListNode)node;
        foreach (var state in states.Children)
        {
            Visit(state);
        }
        return null;
    }

    private Object VisitNumber(IAgentLabTreeNode node)
    {
        var numberNode = (NumberNode)node;
        return numberNode.Token.Type switch
        {
            AgentLabToken.TokenType.Integer => AgentLabToken.TokenType.IntegerType,
            AgentLabToken.TokenType.FloatingPoint => AgentLabToken.TokenType.FloatType,
            _ => null
        };
    }

    private Object VisitBinaryOperation(IAgentLabTreeNode node)
    {
        var binOp = (BinaryOperationNode)node;
        var left = (AgentLabToken.TokenType)Visit(binOp.Left);
        var right = (AgentLabToken.TokenType)Visit(binOp.Right);
        if ((left == AgentLabToken.TokenType.StringType && right != AgentLabToken.TokenType.StringType) || (left != AgentLabToken.TokenType.StringType && right == AgentLabToken.TokenType.StringType))
        {
            // TODO: Raise type error instead of throwing an exception
            throw new Exception($"Binary operation {left} and {right} are not supported");
        }

        if (left == AgentLabToken.TokenType.BooleanType || right == AgentLabToken.TokenType.BooleanType)
        {
            // TODO: Raise type error instead of throwing an exception
            throw new Exception("No binary operation for booleans are supported");
        }

        if (left == AgentLabToken.TokenType.StringType && right == AgentLabToken.TokenType.StringType)
        {
            if (binOp.Token.Type != AgentLabToken.TokenType.AddOperator)
            {
                // TODO: Raise type error instead of throwing an exception
                throw new Exception($"Binary operation {binOp.Token.Type} for {left} and {right} are not supported");
            }
            
            return AgentLabToken.TokenType.StringType;
        }
        
        if (left == AgentLabToken.TokenType.FloatType || right == AgentLabToken.TokenType.FloatType)
        {
            return AgentLabToken.TokenType.FloatType;
        }
        
        return AgentLabToken.TokenType.IntegerType;
    }

    private Object VisitConstDeclaration(IAgentLabTreeNode node)
    {
        var constDecl = (ConstDeclarationNode)node;
        VisitConst(constDecl.Assign.Left);
        return null;
    }

    private Object VisitAssign(IAgentLabTreeNode node)
    {
        var assign = (AssignNode)node;
        Visit(assign.Left);
        Visit(assign.Right);
        return null;
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
            var symbol = SymbolTable.Lookup(constNode.Value);
            var type = (AgentLabToken.TokenType)Visit(assign.Right);
            symbol.Type = SymbolTable.Lookup(type.ToString());
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
        Visit(behaviour.Body);
        return null;
    }

    private Object VisitConst(IAgentLabTreeNode node)
    {
        var constNode = (ConstNode)node;
        if (SymbolTable.Lookup(constNode.Value) != null)
        {
            // TODO: Raise redefinition error instead of throwing an exception
            throw new Exception($"const {constNode.Value} redefinition!");
        }
        
        // Assume default type for now, we'll determine actual type dynamically later
        var type = SymbolTable.Lookup(constNode.Type.ToString());
        SymbolTable.Define(new AgentLabConstSymbol(constNode.Value, type));
        return constNode.Type;
    }
}