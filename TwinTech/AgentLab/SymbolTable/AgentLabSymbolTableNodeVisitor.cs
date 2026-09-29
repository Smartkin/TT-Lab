using System;
using System.Collections.Generic;
using System.Linq;
using Twinsanity.AgentLab.AbstractSyntaxTree;
using Twinsanity.AgentLab.AbstractSyntaxTree.Attributes;
using Twinsanity.AgentLab.AbstractSyntaxTree.ControlPacket;
using Twinsanity.AgentLab.Analyzers;

namespace Twinsanity.AgentLab.SymbolTable;

internal class AgentLabSymbolTableNodeVisitor : NodeVisitor
{
    private Func<int> stateIdGenerator;
    private Func<int, int> actionIdGenerator;
    private Func<int, int> conditionIdGenerator;
    
    public AgentLabSymbolTable SymbolTable { get; private set; }

    /// <summary>
    /// Tells whether a state's behaviour reference (a name or a string) exists, null skips the check
    /// </summary>
    public Func<string, bool> BehaviourExists { get; set; }
    
    public AgentLabSymbolTableNodeVisitor()
    {
        stateIdGenerator = GetIdGenerator();
        actionIdGenerator = GetEditableIdGenerator();
        conditionIdGenerator = GetEditableIdGenerator();
        
        SymbolTable = new AgentLabSymbolTable();
        Visitors.Add(typeof(ConstNode), VisitConst);
        Visitors.Add(typeof(ArrayNode), VisitArrayNode);
        Visitors.Add(typeof(BehaviourNode), VisitBehaviour);
        Visitors.Add(typeof(BehaviourBodyNode), VisitBehaviourBody);
        Visitors.Add(typeof(StarterNode), VisitStarterNode);
        Visitors.Add(typeof(StarterBodyNode), VisitStarterBodyNode);
        Visitors.Add(typeof(StarterAssignerNode), VisitStarterAssignerNode);
        Visitors.Add(typeof(StarterAssignNode), VisitStarterAssignNode);
        Visitors.Add(typeof(ConstDeclarationListNode), VisitConstList);
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
        Visitors.Add(typeof(GraphPriorityAttributeNode), VisitGraphPriorityAttributeNode);
        Visitors.Add(typeof(StartFromAttributeNode), VisitStartFromAttributeNode);
        Visitors.Add(typeof(UseObjectSlotAttributeNode), VisitUseObjectSlotAttribute);
        Visitors.Add(typeof(ObjectSlotNameNode), VisitObjectSlotName);
        Visitors.Add(typeof(ControlPacketAttributeNode), VisitControlPacketAttribute);
        Visitors.Add(typeof(InterruptingAttributeNode), VisitNoop);
        Visitors.Add(typeof(TaggedLiteralNode), VisitTaggedLiteral);
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
        Visitors.Add(typeof(FieldGroupNode), VisitFieldGroupNode);
        Visitors.Add(typeof(ParamDefinitionListNode), VisitParamDefinitionListNode);
        Visitors.Add(typeof(ActionDefinitionListNode), VisitActionDefinitionListNode);
        Visitors.Add(typeof(ActionDefinitionNode), VisitActionDefinitionNode);
        Visitors.Add(typeof(ConditionDefinitionListNode), VisitConditionDefinitionListNode);
        Visitors.Add(typeof(ConditionDefinitionNode), VisitConditionDefinitionNode);
        Visitors.Add(typeof(AliasNode), VisitAlias);
    }

    private Func<int, int> GetEditableIdGenerator()
    {
        var id = 0;
        return (newId) =>
        {
            if (newId == -1)
            {
                return id++;
            }
            
            id = newId;
            return id;

        };
    }

    private Func<int> GetIdGenerator()
    {
        var id = 0;
        return () => id++;
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

    private Object VisitGraphPriorityAttributeNode(IAgentLabTreeNode node)
    {
        var priority = (GraphPriorityAttributeNode)node;
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
        
        for (var i = 0; i < stateBodyList.Children.Count; i++)
        {
            var body = (StateBodyNode)stateBodyList.Children[i];
            if (body.Kind == StateBodyKind.Completion && i != 0)
            {
                throw new AgentLabSyntaxException("The completion block has to be the first body of its state", body.Token.Line, body.Token.Column);
            }

            Visit(body);
        }
        
        return null;
    }

    private Object VisitTaggedLiteral(IAgentLabTreeNode node)
    {
        var literal = (TaggedLiteralNode)node;
        var valueType = Visit(literal.Value) as AgentLabSymbol;
        var integerType = SymbolTable.Lookup(nameof(AgentLabToken.TokenType.IntegerType));
        var floatType = SymbolTable.Lookup(nameof(AgentLabToken.TokenType.FloatType));
        if (valueType != integerType && valueType != floatType)
        {
            throw new AgentLabSyntaxException($"{literal.Name}() takes a number", literal.Token.Line, literal.Token.Column);
        }

        if (literal.Name is "Prop" or "Raw" or "Int" && valueType != integerType)
        {
            throw new AgentLabSyntaxException($"{literal.Name}() takes an integer", literal.Token.Line, literal.Token.Column);
        }

        return SymbolTable.Lookup(AgentLabSymbolTable.TaggedLiteralTypeName);
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

        int conditionIndex;
        if (condDef.Index != null && condDef.Index is not NoOpNode)
        {
            var numberNode = (NumberNode)condDef.Index;
            var numType = Visit(condDef.Index) as AgentLabSymbol;
            AssertType(SymbolTable.Lookup(nameof(AgentLabToken.TokenType.IntegerType)), numType);
            
            conditionIndex = conditionIdGenerator((int)numberNode.Value);
        }
        else
        {
            conditionIndex = conditionIdGenerator(-1);
        }
        
        var conditionSymbol = new AgentLabConditionSymbol(condDef.Name, conditionIndex, SymbolTable.Lookup(nameof(AgentLabToken.TokenType.Condition)),
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
            
            SymbolTable.Define(new AgentLabConditionSymbol(alias, condition.Id, SymbolTable.Lookup(nameof(AgentLabToken.TokenType.Condition)), SymbolTable.Lookup(nameof(AgentLabToken.TokenType.FloatType)), condition.ParameterType));
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
        if (paramDef.Fields == null)
        {
            return new AgentLabParamSymbol(paramDef.Name, SymbolTable.Lookup(paramDef.Type.Type.ToString()));
        }

        // the fields follow each other from bit 0 and have to fill the dword, so every value of the game's scripts has a form
        var fields = new List<AgentLabFieldDefinition>();
        var bit = 0;
        foreach (var field in paramDef.Fields)
        {
            if (fields.Any(other => other.Name == field.Name))
            {
                throw new AgentLabSyntaxException($"Field {field.Name} of {paramDef.Name} is defined twice", field.Token.Line, field.Token.Column);
            }

            fields.Add(new AgentLabFieldDefinition(field.Name, bit, field.Width, field.Type == AgentLabToken.TokenType.BooleanType, field.Type == AgentLabToken.TokenType.SignedIntegerType));
            bit += field.Width;
        }

        if (bit != 32)
        {
            var token = paramDef.Fields[^1].Token;
            throw new AgentLabSyntaxException($"The fields of {paramDef.Name} take {bit} bits, they have to take 32", token.Line, token.Column);
        }

        return new AgentLabParamSymbol(paramDef.Name, SymbolTable.Lookup(nameof(AgentLabToken.TokenType.PackedType)), fields);
    }

    private Object VisitFieldGroupNode(IAgentLabTreeNode node)
    {
        var group = (FieldGroupNode)node;
        foreach (var (_, value) in group.Fields)
        {
            Visit(value);
        }

        return SymbolTable.Lookup(AgentLabSymbolTable.FieldGroupTypeName);
    }

    // The names and types of a {name = value, ...} argument against the parameter's fields
    private void CheckFieldGroup(FieldGroupNode group, AgentLabParamSymbol parameter, string actionName)
    {
        if (parameter.Fields == null)
        {
            throw new AgentLabSyntaxException($"Parameter {parameter.Name} of {actionName} is a value, not fields", group.Token.Line, group.Token.Column);
        }

        var seen = new HashSet<string>();
        foreach (var (name, value) in group.Fields)
        {
            var fieldName = name.GetValue<string>();
            var field = parameter.FindField(fieldName);
            if (field == null)
            {
                throw new AgentLabSyntaxException($"Parameter {parameter.Name} of {actionName} has no field {fieldName}, it has {string.Join(", ", parameter.Fields.Select(f => f.Name))}", name.Line, name.Column);
            }

            if (!seen.Add(fieldName))
            {
                throw new AgentLabSyntaxException($"Field {fieldName} is given twice", name.Line, name.Column);
            }

            var valueType = (Visit(value) as AgentLabSymbol)?.Name;
            var ok = field.IsBool ? valueType is nameof(AgentLabToken.TokenType.BooleanType) or nameof(AgentLabToken.TokenType.IntegerType) : valueType == nameof(AgentLabToken.TokenType.IntegerType);
            if (!ok)
            {
                throw new AgentLabSyntaxException($"Field {fieldName} takes {(field.IsBool ? "a bool" : "an integer")}, not {valueType}", name.Line, name.Column);
            }

            // literals are checked against the field's range here, so the editor's check reports them, expressions when compiled
            if (TryLiteral(value, out var literal) && !field.Fits(literal))
            {
                var range = field.IsBool ? "true or false" : field.IsSigned ? $"{-(1L << (field.Width - 1))} to {(1L << (field.Width - 1)) - 1}" : $"0 to {(field.Width >= 32 ? uint.MaxValue : (1L << field.Width) - 1)}";
                throw new AgentLabSyntaxException($"Field {fieldName} takes {range}, not {literal}", name.Line, name.Column);
            }
        }
    }

    private static bool TryLiteral(IAgentLabTreeNode node, out long value)
    {
        switch (node)
        {
            case NumberNode { Value: int i }:
                value = i;
                return true;
            case BooleanNode b:
                value = b.Value ? 1 : 0;
                return true;
            case UnaryOperationNode { Token.Type: AgentLabToken.TokenType.SubtractOperator } negative when TryLiteral(negative.Expression, out var inner):
                value = -inner;
                return true;
            default:
                value = 0;
                return false;
        }
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
            
            var actionCopy = new AgentLabActionSymbol(alias, action.Id, SymbolTable.Lookup(nameof(AgentLabToken.TokenType.Action)))
            {
                Parameters = action.Parameters
            };
            SymbolTable.Define(actionCopy);
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
        
        int actionIndex;
        if (actionDef.Index != null)
        {
            var numberNode = (NumberNode)actionDef.Index;
            var numType = Visit(actionDef.Index) as AgentLabSymbol;
            AssertType(SymbolTable.Lookup(nameof(AgentLabToken.TokenType.IntegerType)), numType);
            
            actionIndex = actionIdGenerator((int)numberNode.Value);
        }
        else
        {
            actionIndex = actionIdGenerator(-1);
        }
        
        var parameters = Visit(actionDef.Parameters) as AgentLabSymbol[];
        var actionSymbol = new AgentLabActionSymbol(actionDef.Name, actionIndex, SymbolTable.Lookup(nameof(AgentLabToken.TokenType.Action)), parameters);
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
        var expectedSymbols = actionSymbol.Parameters?.GetSymbols<AgentLabParamSymbol>().ToList();
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
            if (((ParamNode)action.Parameters!.Children[i]).Value is FieldGroupNode group)
            {
                CheckFieldGroup(group, expectedArgument, action.Name);
                continue;
            }

            if (!AgentLabSymbolTable.IsAssignable(expectedArgument.Type.Name, providedType.Name))
            {
                var paramNode = (ParamNode)action.Parameters!.Children[i];
                var at = paramNode.Value is NumberNode number ? number.Token : paramNode.Value is TaggedLiteralNode tagged ? tagged.Token : action.Token;
                throw new AgentLabSyntaxException($"Expected {expectedArgument.Type.Name} but got {providedType.Name} for parameter {expectedArgument.Name} of {action.Name}", at.Line, at.Column);
            }
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
        var integerType = SymbolTable.Lookup(nameof(AgentLabToken.TokenType.IntegerType));
        var floatType = SymbolTable.Lookup(nameof(AgentLabToken.TokenType.FloatType));
        foreach (var (value, name) in new[] { (stateBody.Window, "window"), (stateBody.Threshold, "threshold"), (stateBody.Weight, "weight") })
        {
            if (value == null)
            {
                continue;
            }

            var type = Visit(value) as AgentLabSymbol;
            if (type != integerType && type != floatType)
            {
                throw new AgentLabSyntaxException($"{name} has to be a number", stateBody.Token.Line, stateBody.Token.Column);
            }
        }

        Visit(stateBody.Restart);
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

        if (SymbolTable.Lookup(controlPacket.Name) != null)
        {
            // TODO: Raise redefinition error instead of throwing an exception
            throw new Exception($"Control packet {controlPacket.Token} redefinition!");
        }
        
        var symbol = new AgentLabControlPacketSymbol(controlPacket.Name)
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

        var symbol = new AgentLabStateSymbol(state.Name, stateIdGenerator())
        {
            Type = SymbolTable.Lookup(nameof(AgentLabToken.TokenType.State))
        };
        SymbolTable.Define(symbol);
        if (state.BehaviourId is BehaviourReferenceNode reference && BehaviourExists != null && !state.Attributes.Children.Any(attribute => attribute is UseObjectSlotAttributeNode)
            && !BehaviourExists(reference.Reference))
        {
            throw new AgentLabSyntaxException($"Behaviour {reference.Reference} isn't in this package or the packages it depends on", reference.Token.Line, reference.Token.Column);
        }

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
        if (SymbolTable.Lookup(constNode.Name) != null)
        {
            // TODO: Raise redefinition error instead of throwing an exception
            throw new Exception($"Const {constNode.Name} redefinition!");
        }
        
        var type = SymbolTable.Lookup(constNode.Type.ToString());
        SymbolTable.Define(new AgentLabConstSymbol(constNode.Name, type, SymbolTable.GenerateConstId()));
        
        return null;
    }

    private Object VisitAssign(IAgentLabTreeNode node)
    {
        var assign = (AssignNode)node;
        var leftSymbol = Visit(assign.Left) as AgentLabSymbol;
        var integerSymbol = SymbolTable.Lookup(nameof(AgentLabToken.TokenType.IntegerType));
        var floatSymbol = SymbolTable.Lookup(nameof(AgentLabToken.TokenType.FloatType));
        
        // No, this is not a hack, this is going into Enum symbol scope :^)
        if (leftSymbol != null && leftSymbol.Name == SymbolTable.Lookup(nameof(AgentLabToken.TokenType.EnumType)).Name)
        {
            leftSymbol = SymbolTable.Lookup(((ConstNode)assign.Left).Name);
        }

        var rightSymbol = Visit(assign.Right) as AgentLabSymbol;

        // Can assign integers to floats just fine!
        if (leftSymbol?.Name == floatSymbol.Name && rightSymbol?.Name == integerSymbol.Name)
        {
            return null;
        }
        
        AssertType(leftSymbol, rightSymbol);
        
        return null;
    }

    private Object VisitConstList(IAgentLabTreeNode node)
    {
        var consts = (ConstDeclarationListNode)node;
        foreach (var constDeclNode in consts.Children)
        {
            Visit(constDeclNode);
        }

        var constDeclarationReorder = new ConstDeclarationOrderAnalyzer(SymbolTable);
        constDeclarationReorder.Analyze(consts);
        
        DetermineConstTypes(consts);
        
        return null;
    }

    private void DetermineConstTypes(ConstDeclarationListNode consts)
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
    
    private Object VisitStarterAssignNode(IAgentLabTreeNode node)
    {
        var assign = (StarterAssignNode)node;
        Visit(assign.Assign);
        
        return null;
    }

    private Object VisitStarterAssignerNode(IAgentLabTreeNode node)
    {
        var starterAssigner = (StarterAssignerNode)node;
        foreach (var assign in starterAssigner.Children)
        {
            Visit(assign);
        }
        
        return null;
    }

    private Object VisitStarterBodyNode(IAgentLabTreeNode node)
    {
        var starterBody = (StarterBodyNode)node;
        foreach (var assigner in starterBody.Children)
        {
            Visit(assigner);
        }
        
        return null;
    }
    
    private Object VisitStarterNode(IAgentLabTreeNode node)
    {
        var starterNode = (StarterNode)node;
        Visit(starterNode.Body);
        
        return null;
    }
    
    private Object VisitBehaviourBody(IAgentLabTreeNode node)
    {
        var behaviourBody = (BehaviourBodyNode)node;
        // Define consts then define control packets then define states in that exact order
        Visit(behaviourBody.Consts);
        Visit(behaviourBody.ControlPackets);
        Visit(behaviourBody.States);
        Visit(behaviourBody.Starter);
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
        Visit(behaviour.GraphPriority);
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