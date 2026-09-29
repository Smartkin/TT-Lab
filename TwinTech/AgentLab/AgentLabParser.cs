using System;
using System.Collections.Generic;
using System.Linq;
using Twinsanity.AgentLab.AbstractSyntaxTree;
using Twinsanity.AgentLab.AbstractSyntaxTree.Attributes;
using Twinsanity.AgentLab.AbstractSyntaxTree.ControlPacket;

namespace Twinsanity.AgentLab;


/// <summary>
/// Parses text form AgentLab files in custom TwinTech format and returns the Abstract Syntax Tree (AST)
/// The grammar rules may be incorrect (I've never written ones before) or outdated
/// <code>
///******************************* GRAMMAR RULES ************************************
/// * action_definition_list : action_definition - parser implemented
/// *                         | action_definition_list
/// * action_definition : ACTION action_name LPAREN param_definition_list RPAREN - parser implemented
/// *                               ((ATTRIBUTE_OPEN alias_list ATTRIBUTE_CLOSE) | empty) ((COLON INTEGER) | empty) SEMICOLON
/// * alias_list : alias - parser implemented
/// *            | (alias COMMA alias_list)
/// * param_definition_list : param_definition - parser implemented
/// *                        | (param_definition COLON param_definition_list)
/// * param_definition : (BOOL | INT | FLOAT | TFLOAT | TINT | TANGLE) param_name - parser implemented
/// * condition_definition_list : condition_definition - parser implemented
/// *                            | condition_definition_list
/// * condition_definition : CONDITION condition_name LPAREN (param_definition | empty) RPAREN - parser implemented
/// *                               ((ATTRIBUTE_OPEN alias_list ATTRIBUTE_CLOSE) | empty)  ((COLON INTEGER) | empty) SEMICOLON
/// * behaviour_library : global_index_attribute instance_key_attribute LIBRARY behaviour_library_name OPEN_BRACKET linear_behaviour_list action CLOSE_BRACKET - parser implemented
/// * global_index_attribute : ATTRIBUTE_OPEN GLOBAL_INDEX LPAREN INTEGER RPAREN ATTRIBUTE_CLOSE - parser implemented
/// * instance_key_attribute : ATTRIBUTE_OPEN INSTANCE_KEY LPAREN KEY RPAREN ATTRIBUTE_CLOSE - parser implemented
/// * linear_behaviour_list : linear_behaviour - parser implemented
/// *                        | linear_behaviour_list
/// * linear_behaviour : BEHAVIOUR linear_behaviour_name OPEN_BRACKET action_list CLOSE_BRACKET - parser implemented
/// * behaviour : (priority_attribute | BEHAVIOUR) behaviour_name OPEN_BRACKET behaviour_body CLOSE_BRACKET - parser implemented
/// * priority_attribute : ATTRIBUTE_OPEN PRIORITY LPAREN number RPAREN ATTRIBUTE_CLOSE - parser implemented
/// * behaviour_body : (const_declaration_list | state_declaration_list | control_packet_declaration_list | starter_declaration) - parser implemented
/// * const_declaration_list : const_declaration | const_declaration SEMI const_declaration_list - parser implemented
/// * const_declaration : (CONST const_name ASSIGN factor) | empty - parser implemented
/// * starter_declaration : STARTER OPEN_BRACKET starter_body CLOSE_BRACKET - parser implemented
/// * starter_body : (global_object_id | ASSIGN_TYPE | ASSIGN_LOCALITY | ASSIGN_STATUS | ASSIGN_PREFERENCE) - parser implemented
/// * global_object_id : GLOBAL_OBJECT_ID ASSIGN INTEGER - parser implemented
/// * state_declaration_list : state_declaration - parser implemented
/// *                         | state_declaration_list
/// * state_declaration : ((interrupting_attribute | skip_first_body_attribute | use_object_slot_attribute | control_packet_attribute) - parser implemented
/// *                         STATE state_name LPAREN (behaviour_name | empty) RPAREN OPEN_BRACKET (state_body_list | empty) CLOSE_BRACKET)
/// *                     | empty
/// * state_body_list : state_body | state_body_list - parser implemented
/// * state_body : (IF condition (GREATER | LESS | GEQUAL | LEQUAL) factor | ELSE | COMPLETION) OPEN_BRACKET body_value_list (action_list | empty) (state_execute | empty) CLOSE_BRACKET - parser implemented
/// * body_value_list : ((WINDOW | WEIGHT | THRESHOLD) ASSIGN factor SEMICOLON | RESTART ASSIGN bool SEMICOLON)* - parser implemented
/// *                   (interval and unknown are the old names of window and restart)
/// * condition : CONDITION_ID LPAREN (INTEGER | empty) RPAREN - parser implemented
/// * action_list : action | action_list - parser implemented
/// * action : ACTION_ID LPAREN (param_list | empty) RPAREN SEMICOLON - parser implemented
/// * param_list : param | (param COLON param_list) - parser implemented
/// * param : (factor | bool | tagged_literal) - parser implemented
/// * tagged_literal : (PROP | RAW | FLOAT | INT | ANGLE) LPAREN factor RPAREN - parser implemented
/// * state_execute : EXECUTE state_name - parser implemented
/// * interrupting_attribute : ATTRIBUTE_OPEN (INTERRUPTING | NON_BLOCKING) ATTRIBUTE_CLOSE - parser implemented
/// * skip_first_body_attribute : ATTRIBUTE_OPEN SKIP_FIRST_BODY ATTRIBUTE_CLOSE - parser implemented
/// * use_object_slot_attribute : ATTRIBUTE_OPEN USE_OBJECT_SLOT LPAREN SLOT_NAME RPAREN ATTRIBUTE_CLOSE - parser implemented
/// * control_packet_attribute : ATTRIBUTE_OPEN CONTROL_PACKET LPAREN packet_name RPAREN ATTRIBUTE_CLOSE - parser implemented
/// * control_packet_declaration_list : control_packet_declaration | control_packet_declaration_list - parser implemented
/// * control_packet_declaration : (CONTROL_PACKET packet_name OPEN_BRACKET control_packet_body CLOSE_BRACKET) | empty - parser implemented
/// * control_packet_body : settings_block - parser implemented
/// *                       | packet_data_block
/// *                       | empty
/// * settings_block : SETTINGS OPEN_BRACKET (setting_name ASSIGN factor SEMICOLON)* CLOSE_BRACKET - parser implemented
/// * packet_data_block : DATA OPEN_BRACKET (packet_data_name ASSIGN factor SEMICOLON)* CLOSE_BRACKET - parser implemented
/// * setting_name : (SPACE_TYPE | MOTION_TYPE | ACCELERATION_FUNCTION | DOES_TRANSLATE | DOES_ROTATE | DOES_TRANSLATION_CONTINUE - parser implemented
/// *                       | DOES_INTERPOLATE_ANGLES | DOES_YAW_FACES | DOES_PITCH_FACES | DOES_ORIENT_PREDICTS | KEY_IS_LOCAL
/// *                       | USES_ROTATOR | USES_INTERPOLATOR | USES_PHYSICS | CONTINUOUSLY_ROTATES_IN_WORLD_SPACE | AXES | DOES_STALL)
/// * packet_data_name : (SELECTOR | KEY_INDEX | MOVE_SPEED | TURN_SPEED | RAW_POS_X | RAW_POS_Y | RAW_POS_Z | PITCH | YAW | ROLL - parser implemented
/// *                    | DELAY | DURATION | TUMBLE_DATA | SPIN_DATA | TWIST_DATA | RAND_RANGE | POWER | DAMPING | AC_DIST | DEC_DIST
/// *                    | BOUNCE | SYNC_UNIT | JOINT_INDEX)
/// * factor : ADD_OP factor - parser implemented
/// *          | SUBTRACT_OP factor
/// *          | number
/// *          | const
/// * number : INTEGER | FLOAT - parser implemented
/// * bool : TRUE | FALSE - parser implemented
/// * empty : - parser implemented
/// ******************************************************************************************************/
/// </code>
/// </summary>
public class AgentLabParser : IDisposable
{
    private AgentLabLexer _lexer;
    private AgentLabToken _currentToken;

    /// <summary>
    /// Constructs new instance of a parser
    /// </summary>
    /// <param name="lexer">Previously constructed lexer/tokenizer</param>
    public AgentLabParser(AgentLabLexer lexer)
    {
        _lexer = lexer;
        _currentToken = _lexer.GetNextToken();
    }

    /// <summary>
    /// Perform the parsing
    /// </summary>
    /// <returns>Abstract Syntax Tree (AST)</returns>
    public IAgentLabTreeNode Parse()
    {
        IAgentLabTreeNode result = null;

        if (_currentToken.Type == AgentLabToken.TokenType.Identifier)
        {
            result = ActionList();
        }
        else if (_currentToken.Type == AgentLabToken.TokenType.Behaviour)
        {
            result = Behaviour(new AttributeListNode());
        }
        else switch (_currentToken.Type)
        {
            case AgentLabToken.TokenType.Action:
                result = ActionDefinitionList();
                break;
            case AgentLabToken.TokenType.Condition:
                result = ConditionDefinitionList();
                break;
            default:
            {
                var attributes = AttributeList();
                if (attributes.Children.Any(attrib => attrib is PriorityAttributeNode or GraphPriorityAttributeNode or StartFromAttributeNode))
                {
                    result = Behaviour(attributes);
                }
                else
                {
                    result = BehaviourLibrary(attributes);
                }
                break;
            }
        }

        if (_currentToken.Type != AgentLabToken.TokenType.Eof)
        {
            // TODO: Raise parser error instead of throwing an exception
            throw Error($"Unexpected token {_currentToken.Type}");
        }
        
        return result;
    }

    /// <summary>
    /// Parses a script that can only be a list of commands, like an object's command pack
    /// </summary>
    /// <returns>The commands, null when there aren't any</returns>
    public IAgentLabTreeNode ParseCommands()
    {
        if (_currentToken.Type == AgentLabToken.TokenType.Eof)
        {
            return null;
        }

        if (_currentToken.Type != AgentLabToken.TokenType.Identifier)
        {
            throw Error($"Only commands can be written here, found {_currentToken.Type}");
        }

        var result = ActionList();
        if (_currentToken.Type != AgentLabToken.TokenType.Eof)
        {
            throw Error($"Only commands can be written here, found {_currentToken.Type}");
        }

        return result;
    }

    private IAgentLabTreeNode ParamDefinitionList()
    {
        if (_currentToken.Type == AgentLabToken.TokenType.RightParen)
        {
            return Empty();
        }

        var paramList = new ParamDefinitionListNode(ParamDefinition());
        while (_currentToken.Type == AgentLabToken.TokenType.Comma)
        {
            EatToken(AgentLabToken.TokenType.Comma);
            paramList.Children.Add(ParamDefinition());
        }

        return paramList;
    }

    private ParamDefinitionNode ParamDefinition()
    {
        // name { int a : 4, bool b, sint c : 16 } declares the fields the game packs into one dword
        if (_currentToken.Type == AgentLabToken.TokenType.Identifier)
        {
            var groupName = _currentToken;
            EatToken(AgentLabToken.TokenType.Identifier);
            EatToken(AgentLabToken.TokenType.OpenBracket);
            var fields = new List<FieldDefinitionNode> { FieldDefinition() };
            while (_currentToken.Type == AgentLabToken.TokenType.Comma)
            {
                EatToken(AgentLabToken.TokenType.Comma);
                fields.Add(FieldDefinition());
            }

            EatToken(AgentLabToken.TokenType.CloseBracket);
            return new ParamDefinitionNode(groupName, fields);
        }

        var type = _currentToken;
        switch (_currentToken.Type)
        {
            case AgentLabToken.TokenType.BooleanType:
            case AgentLabToken.TokenType.IntegerType:
            case AgentLabToken.TokenType.FloatType:
            case AgentLabToken.TokenType.TaggedFloatType:
            case AgentLabToken.TokenType.TaggedIntType:
            case AgentLabToken.TokenType.TaggedAngleType:
                EatToken(_currentToken.Type);
                break;
            default:
                // TODO: Raise type error instead of throwing an exception
                throw Error($"Unsupported parameter type {_currentToken.Type}");
        }

        var identifier = _currentToken;
        EatToken(AgentLabToken.TokenType.Identifier);
        return new ParamDefinitionNode(identifier, new TypeNode(type));
    }

    private FieldDefinitionNode FieldDefinition()
    {
        var type = _currentToken;
        if (type.Type is not (AgentLabToken.TokenType.IntegerType or AgentLabToken.TokenType.SignedIntegerType or AgentLabToken.TokenType.BooleanType))
        {
            throw Error($"A field is an int, a sint or a bool, not {type.Type}");
        }

        EatToken(type.Type);
        var name = _currentToken;
        EatToken(AgentLabToken.TokenType.Identifier);
        var width = type.Type == AgentLabToken.TokenType.BooleanType ? 1 : 0;
        if (_currentToken.Type == AgentLabToken.TokenType.Colon)
        {
            EatToken(AgentLabToken.TokenType.Colon);
            var widthToken = _currentToken;
            EatToken(AgentLabToken.TokenType.Integer);
            width = widthToken.GetValue<int>();
        }

        if (width is < 1 or > 32)
        {
            throw Error($"Field {name.GetValue<string>()} needs a width of 1 to 32 bits");
        }

        return new FieldDefinitionNode(name, type, width);
    }

    private ActionDefinitionNode ActionDefinition()
    {
        EatToken(AgentLabToken.TokenType.Action);
        var token = _currentToken;
        EatToken(AgentLabToken.TokenType.Identifier);
        EatToken(AgentLabToken.TokenType.LeftParen);
        var parameters = ParamDefinitionList();
        EatToken(AgentLabToken.TokenType.RightParen);
        
        IAgentLabTreeNode aliases = Empty();
        if (_currentToken.Type == AgentLabToken.TokenType.AttributeOpen)
        {
            aliases = AliasList();
        }

        IAgentLabTreeNode number = Empty();
        if (_currentToken.Type == AgentLabToken.TokenType.Colon)
        {
            EatToken(AgentLabToken.TokenType.Colon);
            number = Number();
        }
        
        EatToken(AgentLabToken.TokenType.Semicolon);
        
        return new ActionDefinitionNode(token, parameters, number, aliases);
    }

    private ActionDefinitionListNode ActionDefinitionList()
    {
        var node = new ActionDefinitionListNode();

        while (_currentToken.Type == AgentLabToken.TokenType.Action)
        {
            node.Children.Add(ActionDefinition());
        }

        return node;
    }

    private AliasListNode AliasList()
    {
        EatToken(AgentLabToken.TokenType.AttributeOpen);
        var aliasList = new AliasListNode(Alias());
        while (_currentToken.Type == AgentLabToken.TokenType.Comma)
        {
            EatToken(AgentLabToken.TokenType.Comma);
            aliasList.Children.Add(Alias());
        }
        EatToken(AgentLabToken.TokenType.AttributeClose);

        return aliasList;
    }

    private AliasNode Alias()
    {
        var token = _currentToken;
        EatToken(AgentLabToken.TokenType.Identifier);
        return new AliasNode(token);
    }

    private ConditionDefinitionNode ConditionDefinition()
    {
        EatToken(AgentLabToken.TokenType.Condition);
        var token = _currentToken;
        EatToken(AgentLabToken.TokenType.Identifier);
        EatToken(AgentLabToken.TokenType.LeftParen);
        IAgentLabTreeNode parameter = Empty();
        if (_currentToken.Type != AgentLabToken.TokenType.RightParen)
        {
            parameter = ParamDefinition();
        }
        EatToken(AgentLabToken.TokenType.RightParen);

        IAgentLabTreeNode aliases = Empty();
        if (_currentToken.Type == AgentLabToken.TokenType.AttributeOpen)
        {
            aliases = AliasList();
        }

        IAgentLabTreeNode number = Empty();
        if (_currentToken.Type == AgentLabToken.TokenType.Colon)
        {
            EatToken(AgentLabToken.TokenType.Colon);
            number = Number();
        }
        
        EatToken(AgentLabToken.TokenType.Semicolon);
        
        return new ConditionDefinitionNode(token, parameter, number, aliases);
    }

    private ConditionDefinitionListNode ConditionDefinitionList()
    {
        var node = new ConditionDefinitionListNode();

        while (_currentToken.Type == AgentLabToken.TokenType.Condition)
        {
            node.Children.Add(ConditionDefinition());
        }

        return node;
    }

    private AttributeListNode AttributeList()
    {
        var attribList = new AttributeListNode();

        while (_currentToken.Type == AgentLabToken.TokenType.AttributeOpen)
        {
            var attribute = Attribute();
            if (attribute != null)
            {
                attribList.Children.Add(attribute);
            }
        }

        return attribList;
    }

    private IAttributeNode Attribute()
    {
        IAttributeNode node;
        EatToken(AgentLabToken.TokenType.AttributeOpen);
        switch (_currentToken.Type)
        {
            case AgentLabToken.TokenType.StartFrom:
                node = StartFromAttribute();
                break;
            case AgentLabToken.TokenType.Priority:
                node = PriorityAttribute();
                break;
            case AgentLabToken.TokenType.GraphPriority:
                node = GraphPriorityAttribute();
                break;
            case AgentLabToken.TokenType.NonBlocking:
            case AgentLabToken.TokenType.Interrupting:
            case AgentLabToken.TokenType.SkipFirstBody:
            case AgentLabToken.TokenType.UseObjectSlot:
            case AgentLabToken.TokenType.ControlPacket:
                node = StateAttribute();
                break;
            case AgentLabToken.TokenType.GlobalIndex:
            case AgentLabToken.TokenType.InstanceType:
                node = BehaviourLibraryAttribute();
                break;
            case AgentLabToken.TokenType.Unknown:
                SkipUnknownAttribute();
                node = null;
                break;
            default:
                // TODO: Raise parser error instead of throwing an exception
                throw Error($"Unknown attribute {_currentToken.Value} {_currentToken.Type}");
        }
        EatToken(AgentLabToken.TokenType.AttributeClose);

        return node;
    }

    private NumberNode Number()
    {
        var number = new NumberNode(_currentToken);
        switch (_currentToken.Type)
        {
            case AgentLabToken.TokenType.Integer:
                EatToken(AgentLabToken.TokenType.Integer);
                break;
            case AgentLabToken.TokenType.FloatingPoint:
                EatToken(AgentLabToken.TokenType.FloatingPoint);
                break;
            default:
                // TODO: Raise parser error instead of throwing an exception
                throw Error($"Unexpected token type: {_currentToken.Type} Expected: {AgentLabToken.TokenType.Integer} or {AgentLabToken.TokenType.FloatingPoint}");
        }
        
        return number;
    }

    private IAgentLabTreeNode Factor()
    {
        var token = _currentToken;
        IAgentLabTreeNode node;
        switch (token.Type)
        {
            case AgentLabToken.TokenType.AddOperator:
                EatToken(AgentLabToken.TokenType.AddOperator);
                node = new UnaryOperationNode(token, Factor());
                break;
            case AgentLabToken.TokenType.SubtractOperator:
                EatToken(AgentLabToken.TokenType.SubtractOperator);
                node = new UnaryOperationNode(token, Factor());
                break;
            case AgentLabToken.TokenType.Integer:
            case AgentLabToken.TokenType.FloatingPoint:
                node = Number();
                break;
            case AgentLabToken.TokenType.LeftParen:
                EatToken(AgentLabToken.TokenType.LeftParen);
                node = Expression();
                EatToken(AgentLabToken.TokenType.RightParen);
                break;
            case AgentLabToken.TokenType.String:
                node = String();
                break;
            case AgentLabToken.TokenType.False:
            case AgentLabToken.TokenType.True:
                node = Boolean();
                break;
            case AgentLabToken.TokenType.Identifier when TaggedLiteralNode.Names.Contains(token.GetValue<string>()):
                EatToken(AgentLabToken.TokenType.Identifier);
                if (_currentToken.Type == AgentLabToken.TokenType.LeftParen)
                {
                    EatToken(AgentLabToken.TokenType.LeftParen);
                    var value = Factor();
                    EatToken(AgentLabToken.TokenType.RightParen);
                    node = new TaggedLiteralNode(token, value);
                }
                else
                {
                    node = new ConstNode(token);
                }
                break;
            default:
                node = Const();
                break;
        }

        return node;
    }

    private StringNode String()
    {
        var stringNode = new StringNode(_currentToken);
        EatToken(AgentLabToken.TokenType.String);
        return stringNode;
    }

    private ControlPacketListNode ControlPacketList(ref ControlPacketListNode controlPackets)
    {
        while (_currentToken.Type == AgentLabToken.TokenType.Packet)
        {
            controlPackets.Children.Add(ControlPacket());
        }

        return controlPackets;
    }

    private ControlPacketNode ControlPacket()
    {
        EatToken(AgentLabToken.TokenType.Packet);
        var token = _currentToken;
        EatToken(AgentLabToken.TokenType.Identifier);
        EatToken(AgentLabToken.TokenType.OpenBracket);
        var body = ControlPacketBody(token);
        EatToken(AgentLabToken.TokenType.CloseBracket);
        return new ControlPacketNode(token, body);
    }

    private ControlPacketBodyNode ControlPacketBody(AgentLabToken owner)
    {
        var settings = new List<ControlPacketSettingNode>();
        var datas = new List<ControlPacketDataNode>();

        if (_currentToken.Type == AgentLabToken.TokenType.Settings)
        {
            EatToken(AgentLabToken.TokenType.Settings);
            EatToken(AgentLabToken.TokenType.OpenBracket);
            while (_currentToken.Type == AgentLabToken.TokenType.Identifier)
            {
                settings.Add(ControlPacketSetting(owner));
            }
            EatToken(AgentLabToken.TokenType.CloseBracket);
        }

        if (_currentToken.Type == AgentLabToken.TokenType.Data)
        {
            EatToken(AgentLabToken.TokenType.Data);
            EatToken(AgentLabToken.TokenType.OpenBracket);
            while (_currentToken.Type == AgentLabToken.TokenType.Identifier)
            {
                datas.Add(ControlPacketData(owner));
            }
            EatToken(AgentLabToken.TokenType.CloseBracket);
        }
        
        return new ControlPacketBodyNode(settings, datas, owner.GetValue<string>());
    }

    private ControlPacketSettingNode ControlPacketSetting(AgentLabToken owner)
    {
        var token = _currentToken;
        var assign = Assign();
        EatToken(AgentLabToken.TokenType.Semicolon);
        return new ControlPacketSettingNode(token, assign, owner.GetValue<string>());
    }

    private ControlPacketDataNode ControlPacketData(AgentLabToken owner)
    {
        var token = _currentToken;
        var assign = Assign();
        EatToken(AgentLabToken.TokenType.Semicolon);
        return new ControlPacketDataNode(token, assign, owner.GetValue<string>());
    }

    private BooleanNode Boolean()
    {
        var token = _currentToken;
        switch (token.Type)
        {
            case AgentLabToken.TokenType.False:
                EatToken(AgentLabToken.TokenType.False);
                break;
            case AgentLabToken.TokenType.True:
                EatToken(AgentLabToken.TokenType.True);
                break;
            default:
                // TODO: Raise parser error instead of throwing an exception
                throw Error($"Expected boolean instead got {token.Type}");
        }
        
        return new BooleanNode(token);
    }

    private IAgentLabTreeNode Term()
    {
        var factor = Factor();
        IAgentLabTreeNode node = null;

        while (_currentToken.Type is AgentLabToken.TokenType.MultiplyOperator or AgentLabToken.TokenType.DivideOperator)
        {
            var token = _currentToken;
            switch (_currentToken.Type)
            {
                case AgentLabToken.TokenType.MultiplyOperator:
                    EatToken(AgentLabToken.TokenType.MultiplyOperator);
                    break;
                case AgentLabToken.TokenType.DivideOperator:
                    EatToken(AgentLabToken.TokenType.DivideOperator);
                    break;
            }

            node = new BinaryOperationNode(factor, token, Factor());
        }
        
        return node ?? factor;
    }

    private IAgentLabTreeNode Expression()
    {
        var term = Term();
        IAgentLabTreeNode node = null;

        while (_currentToken.Type is AgentLabToken.TokenType.AddOperator or AgentLabToken.TokenType.SubtractOperator)
        {
            var token = _currentToken;
            switch (_currentToken.Type)
            {
                case AgentLabToken.TokenType.AddOperator:
                    EatToken(AgentLabToken.TokenType.AddOperator);
                    break;
                case AgentLabToken.TokenType.SubtractOperator:
                    EatToken(AgentLabToken.TokenType.SubtractOperator);
                    break;
            }
            
            node = new BinaryOperationNode(term, token, Term());
        }
        
        return node ?? term;
    }

    private AssignNode Assign()
    {
        var left = Const();
        var token = _currentToken;
        EatToken(AgentLabToken.TokenType.Assign);
        var right = Expression();
        return new AssignNode(left, token, right);
    }

    private ConstNode Const()
    {
        var identifier = _currentToken;
        ConstNode node;
        EatToken(AgentLabToken.TokenType.Identifier);
        if (_currentToken.Type == AgentLabToken.TokenType.AttributeOpen)
        {
            EatToken(AgentLabToken.TokenType.AttributeOpen);
            var index = Factor();
            node = new ArrayNode(identifier, index);
            EatToken(AgentLabToken.TokenType.AttributeClose);
        }
        else
        {
            node = new ConstNode(identifier);
        }
        return node;
    }

    private StarterNode Starter()
    {
        EatToken(AgentLabToken.TokenType.Starter);
        EatToken(AgentLabToken.TokenType.OpenBracket);
        var body = StarterBody();
        EatToken(AgentLabToken.TokenType.CloseBracket);
        return new StarterNode(body);
    }

    private StarterBodyNode StarterBody()
    {
        var body = new StarterBodyNode();
        while (_currentToken.Type == AgentLabToken.TokenType.Assigner)
        {
            body.Children.Add(StarterAssignerNode());
        }

        return body;
    }

    private StarterAssignerNode StarterAssignerNode()
    {
        EatToken(AgentLabToken.TokenType.Assigner);
        EatToken(AgentLabToken.TokenType.Assign);
        EatToken(AgentLabToken.TokenType.OpenBracket);
        var assignerNode = new StarterAssignerNode();
        while (_currentToken.Type == AgentLabToken.TokenType.Identifier)
        {
            assignerNode.Children.Add(StarterAssign());
        }
        EatToken(AgentLabToken.TokenType.CloseBracket);

        return assignerNode;
    }

    private StarterAssignNode StarterAssign()
    {
        var token = _currentToken;
        var assign = Assign();
        EatToken(AgentLabToken.TokenType.Semicolon);
        return new StarterAssignNode(token, assign);
    }

    private ConditionNode Condition()
    {
        var conditionIdentifier = _currentToken;
        
        EatToken(AgentLabToken.TokenType.Identifier);
        EatToken(AgentLabToken.TokenType.LeftParen);
        IAgentLabTreeNode factor = null;
        if (_currentToken.Type != AgentLabToken.TokenType.RightParen)
        {
            factor = Factor();
        }
        EatToken(AgentLabToken.TokenType.RightParen);
        
        var conditionNode = new ConditionNode(conditionIdentifier, factor);
        return conditionNode;
    }

    private StateListNode StateList(ref StateListNode stateList)
    {
        while (_currentToken.Type is AgentLabToken.TokenType.State or AgentLabToken.TokenType.AttributeOpen)
        {
            stateList.Children.Add(State());
        }
        
        return stateList;
    }

    private StateNode State()
    {
        var attributes = AttributeList();
        
        EatToken(AgentLabToken.TokenType.State);
        var stateNameToken = _currentToken;
        EatToken(AgentLabToken.TokenType.Identifier);
        EatToken(AgentLabToken.TokenType.LeftParen);

        // The child behaviour by its name, or as a string for what only the resolver understands (a URI, an index)
        IAgentLabTreeNode behaviourId = null;
        if (_currentToken.Type == AgentLabToken.TokenType.Identifier)
        {
            behaviourId = new BehaviourReferenceNode(_currentToken, true);
            EatToken(AgentLabToken.TokenType.Identifier);
        }
        else if (_currentToken.Type != AgentLabToken.TokenType.RightParen)
        {
            behaviourId = new BehaviourReferenceNode(_currentToken, false);
            EatToken(AgentLabToken.TokenType.String);
        }
        
        EatToken(AgentLabToken.TokenType.RightParen);
        EatToken(AgentLabToken.TokenType.OpenBracket);
        var bodies = StateBodyList();
        EatToken(AgentLabToken.TokenType.CloseBracket);
        
        return new StateNode(stateNameToken, behaviourId, bodies, attributes);
    }

    private StateBodyListNode StateBodyList()
    {
        var bodies = new StateBodyListNode();

        while (_currentToken.Type == AgentLabToken.TokenType.If || IsKeyword("else") || IsKeyword("completion"))
        {
            bodies.Children.Add(StateBody());
        }
        
        return bodies;
    }

    private bool IsKeyword(string keyword)
    {
        return _currentToken.Type == AgentLabToken.TokenType.Identifier && _currentToken.GetValue<string>() == keyword;
    }

    private static ConditionNode BuiltInCondition(AgentLabToken at, string name)
    {
        var token = new AgentLabToken(AgentLabToken.TokenType.Identifier, name).WithPosition(at.Line, at.Column);
        return new ConditionNode(token, new NumberNode(new AgentLabToken(AgentLabToken.TokenType.Integer, 0).WithPosition(at.Line, at.Column)));
    }

    private StateBodyNode StateBody()
    {
        var token = _currentToken;
        var kind = StateBodyKind.If;
        ConditionNode condition;
        IAgentLabTreeNode threshold = null;
        var isNot = false;
        if (IsKeyword("else"))
        {
            EatToken(AgentLabToken.TokenType.Identifier);
            kind = StateBodyKind.Else;
            condition = BuiltInCondition(token, "Else");
        }
        else if (IsKeyword("completion"))
        {
            EatToken(AgentLabToken.TokenType.Identifier);
            kind = StateBodyKind.Completion;
            condition = BuiltInCondition(token, "Next");
        }
        else
        {
            EatToken(AgentLabToken.TokenType.If);
            condition = Condition();
            switch (_currentToken.Type)
            {
                case AgentLabToken.TokenType.Greater:
                case AgentLabToken.TokenType.GreaterEqual:
                    EatToken(_currentToken.Type);
                    break;
                case AgentLabToken.TokenType.Less:
                case AgentLabToken.TokenType.LessEqual:
                    EatToken(_currentToken.Type);
                    isNot = true;
                    break;
                default:
                    throw Error($"Expected a comparison after the condition, found {_currentToken.Type}");
            }

            threshold = Factor();
        }

        EatToken(AgentLabToken.TokenType.OpenBracket);
        IAgentLabTreeNode window = null;
        IAgentLabTreeNode weight = null;
        IAgentLabTreeNode restart = null;
        while (true)
        {
            // interval and unknown are what the values used to be called
            if (_currentToken.Type == AgentLabToken.TokenType.Interval || IsKeyword("window"))
            {
                window = BodyValue(ref window, "window");
            }
            else if (IsKeyword("weight"))
            {
                weight = BodyValue(ref weight, "weight");
            }
            else if (IsKeyword("threshold") && kind != StateBodyKind.If)
            {
                threshold = BodyValue(ref threshold, "threshold");
            }
            else if (_currentToken.Type == AgentLabToken.TokenType.Unknown || IsKeyword("restart"))
            {
                if (restart != null)
                {
                    throw Error("restart is set twice");
                }

                EatToken(_currentToken.Type);
                EatToken(AgentLabToken.TokenType.Assign);
                restart = Boolean();
                EatToken(AgentLabToken.TokenType.Semicolon);
            }
            else
            {
                break;
            }
        }

        ActionListNode actions = null;
        StateExecuteNode execute = null;
        if (_currentToken.Type == AgentLabToken.TokenType.Identifier)
        {
            actions = ActionList();
        }
        
        if (_currentToken.Type == AgentLabToken.TokenType.Execute)
        {
            execute = Execute();
        }

        EatToken(AgentLabToken.TokenType.CloseBracket);

        return new StateBodyNode(token, kind, condition, threshold, isNot, window, weight, restart, actions, execute);
    }

    private IAgentLabTreeNode BodyValue(ref IAgentLabTreeNode current, string name)
    {
        if (current != null)
        {
            throw Error($"{name} is set twice");
        }

        EatToken(_currentToken.Type);
        EatToken(AgentLabToken.TokenType.Assign);
        var factor = Factor();
        EatToken(AgentLabToken.TokenType.Semicolon);
        return factor;
    }

    private NoOpNode Empty()
    {
        return new NoOpNode();
    }

    private StateExecuteNode Execute()
    {
        EatToken(AgentLabToken.TokenType.Execute);
        var token = _currentToken;
        EatToken(AgentLabToken.TokenType.Identifier);
        EatToken(AgentLabToken.TokenType.Semicolon);
        return new StateExecuteNode(token);
    }

    private ActionListNode ActionList()
    {
        var actionList = new ActionListNode();
        actionList.Children.Add(Action());

        while (_currentToken.Type == AgentLabToken.TokenType.Identifier)
        {
            actionList.Children.Add(Action());
        }

        return actionList;
    }

    private ActionNode Action()
    {
        var token = _currentToken;
        EatToken(AgentLabToken.TokenType.Identifier);
        EatToken(AgentLabToken.TokenType.LeftParen);

        if (_currentToken.Type == AgentLabToken.TokenType.RightParen)
        {
            EatToken(AgentLabToken.TokenType.RightParen);
            EatToken(AgentLabToken.TokenType.Semicolon);
            return new ActionNode(token);
        }

        var paramList = ParamList();
        EatToken(AgentLabToken.TokenType.RightParen);
        EatToken(AgentLabToken.TokenType.Semicolon);
        return new ActionNode(token, paramList);
    }

    private ParamListNode ParamList()
    {
        var @params = new ParamListNode();
        @params.Children.Add(new ParamNode(Argument()));
        
        while (_currentToken.Type == AgentLabToken.TokenType.Comma)
        {
            EatToken(AgentLabToken.TokenType.Comma);
            @params.Children.Add(new ParamNode(Argument()));
        }
        
        return @params;
    }

    private IAgentLabTreeNode Argument()
    {
        return _currentToken.Type == AgentLabToken.TokenType.OpenBracket ? FieldGroup() : Factor();
    }

    // {name = value, ...}: the fields of a packed parameter, the ones left out are 0
    private FieldGroupNode FieldGroup()
    {
        var token = _currentToken;
        EatToken(AgentLabToken.TokenType.OpenBracket);
        var fields = new List<(AgentLabToken, IAgentLabTreeNode)>();
        while (_currentToken.Type != AgentLabToken.TokenType.CloseBracket)
        {
            var name = _currentToken;
            EatToken(AgentLabToken.TokenType.Identifier);
            EatToken(AgentLabToken.TokenType.Assign);
            fields.Add((name, Expression()));
            if (_currentToken.Type != AgentLabToken.TokenType.Comma)
            {
                break;
            }

            EatToken(AgentLabToken.TokenType.Comma);
        }

        EatToken(AgentLabToken.TokenType.CloseBracket);
        return new FieldGroupNode(token, fields);
    }

    // [Unknown(n)] of older scripts held the state's bits the game never reads, it's dropped
    private void SkipUnknownAttribute()
    {
        EatToken(AgentLabToken.TokenType.Unknown);
        EatToken(AgentLabToken.TokenType.LeftParen);
        Number();
        EatToken(AgentLabToken.TokenType.RightParen);
    }

    private UseObjectSlotAttributeNode UseObjectSlot()
    {
        EatToken(AgentLabToken.TokenType.UseObjectSlot);
        EatToken(AgentLabToken.TokenType.LeftParen);
        var node = new ObjectSlotNameNode(_currentToken);
        EatToken(AgentLabToken.TokenType.Identifier);
        EatToken(AgentLabToken.TokenType.RightParen);
        return new UseObjectSlotAttributeNode(node);
    }

    private GlobalIndexAttributeNode GlobalIndexAttribute()
    {
        EatToken(AgentLabToken.TokenType.GlobalIndex);
        EatToken(AgentLabToken.TokenType.LeftParen);
        var node = Number();
        EatToken(AgentLabToken.TokenType.RightParen);
        return new GlobalIndexAttributeNode(node);
    }

    private InstanceTypeAttributeNode InstanceTypeAttribute()
    {
        EatToken(AgentLabToken.TokenType.InstanceType);
        EatToken(AgentLabToken.TokenType.LeftParen);
        var node = Const();
        EatToken(AgentLabToken.TokenType.RightParen);
        return new InstanceTypeAttributeNode(node);
    }

    private IAttributeNode BehaviourLibraryAttribute()
    {
        IAttributeNode attribute;
        switch (_currentToken.Type)
        {
            case AgentLabToken.TokenType.GlobalIndex:
                attribute = GlobalIndexAttribute();
                break;
            case AgentLabToken.TokenType.InstanceType:
                attribute = InstanceTypeAttribute();
                break;
            default:
                // TODO: Raise a parser error instead of throwing an exception
                throw Error($"Unexpected attribute type {_currentToken.Type}");
        }

        return attribute;
    }

    private BehaviourLibraryNode BehaviourLibrary(AttributeListNode attributes)
    {
        EatToken(AgentLabToken.TokenType.Library);
        var token = _currentToken;
        EatToken(AgentLabToken.TokenType.Identifier);
        EatToken(AgentLabToken.TokenType.OpenBracket);
        var behaviours = LinearBehaviourList();
        var action = Action();
        EatToken(AgentLabToken.TokenType.CloseBracket);
        
        var globalIndexAttribute = attributes.Children.FirstOrDefault(attrib => attrib is GlobalIndexAttributeNode);
        var instanceTypeAttribute = attributes.Children.FirstOrDefault(attrib => attrib is InstanceTypeAttributeNode);
        return new BehaviourLibraryNode(token, behaviours, action, globalIndexAttribute as IAttributeNode, instanceTypeAttribute as IAttributeNode);
    }

    private LinearBehaviourListNode LinearBehaviourList()
    {
        var node = new LinearBehaviourListNode();

        while (_currentToken.Type == AgentLabToken.TokenType.Behaviour)
        {
            node.Children.Add(LinearBehaviour());
        }

        return node;
    }

    private LinearBehaviourNode LinearBehaviour()
    {
        EatToken(AgentLabToken.TokenType.Behaviour);
        var token = _currentToken;
        EatToken(AgentLabToken.TokenType.Identifier);
        EatToken(AgentLabToken.TokenType.OpenBracket);
        ActionListNode actionList = null;
        if (_currentToken.Type == AgentLabToken.TokenType.Identifier)
        {
            actionList = ActionList();
        }
        EatToken(AgentLabToken.TokenType.CloseBracket);
        
        return new LinearBehaviourNode(token, actionList);
    }

    private IAttributeNode StateAttribute()
    {
        IAttributeNode attribute;
        switch (_currentToken.Type)
        {
            case AgentLabToken.TokenType.NonBlocking:
            case AgentLabToken.TokenType.Interrupting:
                EatToken(_currentToken.Type);
                attribute = new InterruptingAttributeNode();
                break;
            case AgentLabToken.TokenType.SkipFirstBody:
                EatToken(AgentLabToken.TokenType.SkipFirstBody);
                attribute = new SkipFirstBodyAttributeNode();
                break;
            case AgentLabToken.TokenType.UseObjectSlot:
                attribute = UseObjectSlot();
                break;
            case AgentLabToken.TokenType.ControlPacket:
                attribute = ControlPacketAttribute();
                break;
            default:
                // TODO: Raise parse error without throwing an exception
                throw Error($"Unexpected token type {_currentToken.Type}");
        }
        
        return attribute;
    }

    private ControlPacketAttributeNode ControlPacketAttribute()
    {
        EatToken(AgentLabToken.TokenType.ControlPacket);
        EatToken(AgentLabToken.TokenType.LeftParen);
        var node = new ControlPacketAttributeNode(_currentToken);
        EatToken(AgentLabToken.TokenType.Identifier);
        EatToken(AgentLabToken.TokenType.RightParen);
        return node;
    }

    private StartFromAttributeNode StartFromAttribute()
    {
        EatToken(AgentLabToken.TokenType.StartFrom);
        EatToken(AgentLabToken.TokenType.LeftParen);
        var state = _currentToken;
        EatToken(AgentLabToken.TokenType.Identifier);
        EatToken(AgentLabToken.TokenType.RightParen);

        return new StartFromAttributeNode(state);
    }

    private PriorityAttributeNode PriorityAttribute()
    {
        EatToken(AgentLabToken.TokenType.Priority);
        EatToken(AgentLabToken.TokenType.LeftParen);
        var resultNode = Number();
        EatToken(AgentLabToken.TokenType.RightParen);
        
        return new PriorityAttributeNode(resultNode);
    }

    private GraphPriorityAttributeNode GraphPriorityAttribute()
    {
        EatToken(AgentLabToken.TokenType.GraphPriority);
        EatToken(AgentLabToken.TokenType.LeftParen);
        var resultNode = Number();
        EatToken(AgentLabToken.TokenType.RightParen);

        return new GraphPriorityAttributeNode(resultNode);
    }

    private ConstDeclarationListNode ConstList(ref ConstDeclarationListNode consts)
    {
        while (_currentToken.Type == AgentLabToken.TokenType.Const)
        {
            EatToken(AgentLabToken.TokenType.Const);
            consts.Children.Add(new ConstDeclarationNode(Assign()));
            EatToken(AgentLabToken.TokenType.Semicolon);
        }

        return consts;
    }

    private BehaviourBodyNode BehaviourBody()
    {
        var consts = new ConstDeclarationListNode();
        StarterNode starter = null;
        var states = new StateListNode();
        var controlPackets = new ControlPacketListNode();

        while (_currentToken.Type != AgentLabToken.TokenType.CloseBracket)
        {
            switch (_currentToken.Type)
            {
                case AgentLabToken.TokenType.Const:
                    consts = ConstList(ref consts);
                    break;
                case AgentLabToken.TokenType.Starter:
                    if (starter != null)
                    {
                        // TODO: Raise parser error instead of throwing an exception
                        throw Error("Only one Starter definition can exist for a given behaviour");
                    }
                    starter = Starter();
                    break;
                case AgentLabToken.TokenType.State or AgentLabToken.TokenType.AttributeOpen:
                    states = StateList(ref states);
                    break;
                case AgentLabToken.TokenType.Packet:
                    controlPackets = ControlPacketList(ref controlPackets);
                    break;
                default:
                    throw Error($"Unexpected token type {_currentToken.Type}");
            }
        }
        
        return new BehaviourBodyNode(consts, states, controlPackets, starter);
    }

    private IAgentLabTreeNode Behaviour(AttributeListNode attributes)
    {
        EatToken(AgentLabToken.TokenType.Behaviour);
        var behaviourNameToken = _currentToken;
        EatToken(AgentLabToken.TokenType.Identifier);
        EatToken(AgentLabToken.TokenType.OpenBracket);
        var behaviourBody = BehaviourBody();
        EatToken(AgentLabToken.TokenType.CloseBracket);

        var priorityAttribute = attributes.Children.FirstOrDefault(attrib => attrib is PriorityAttributeNode, null);
        var graphPriorityAttribute = attributes.Children.FirstOrDefault(attrib => attrib is GraphPriorityAttributeNode, null);
        var startFrom = attributes.Children.FirstOrDefault(attrib => attrib is StartFromAttributeNode, null);
        return new BehaviourNode(behaviourNameToken, behaviourBody, priorityAttribute as PriorityAttributeNode, startFrom as StartFromAttributeNode, graphPriorityAttribute as GraphPriorityAttributeNode);
    }
    
    private void EatToken(AgentLabToken.TokenType tokenType)
    {
        if (_currentToken.Type != tokenType)
        {
            throw Error($"Unexpected token {_currentToken.Type} ({_currentToken}), expected {tokenType}");
        }
        
        _currentToken = _lexer.GetNextToken();
    }

    private AgentLabSyntaxException Error(string message)
    {
        return new AgentLabSyntaxException(message, _currentToken.Line, _currentToken.Column);
    }

    public void Dispose()
    {
        _lexer?.Dispose();
    }
}