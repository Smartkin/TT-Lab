try:
    import json5 as json
except ImportError:
    # The definitions only have line comments, which plain JSON can't read
    import json as plain_json
    import re

    class json:
        @staticmethod
        def load(f):
            return plain_json.loads(re.sub(r'//.*', '', f.read()))

# The JSON's argument types in the .lab's terms: floats the game reads as such, ints, and the tagged values
# (a literal or an instance property index) of float, int and angle type. Everything else is an int written in hex
TYPES = {'single': 'float', 'float': 'float', 'int32': 'int', 'tfloat': 'tfloat', 'tint': 'tint', 'tangle': 'tangle'}

def generateFile(labDefs, outputPath):
    commandSizes = labDefs['CommandSizes']
    parametersPerFunc = []
    actionDefinitionResultText = "// Generated using LabActionGenerator.py Edit at your own risk!\n\n"
    for commandSize in commandSizes:
        trueSize = int(commandSize, 16)
        if trueSize == 0:
            parametersPerFunc.append(-1)
            continue
        trueSize -= 0xC
        parametersPerFunc.append(trueSize / 4)
    commands = labDefs['CommandMap']
    for index in range(1024):
        actionDefinition = "action "
        name = "AUnknown_" + str(index)
        command = commands.get(str(index))
        if command is not None:
            name = command['Name']
        if parametersPerFunc[index] == -1:
            name += "_DELETED"
        actionDefinition += name
        actionDefinition += "("
        if parametersPerFunc[index] > 0:
            params = []
            names = command.get('ArgumentNames', []) if command is not None else []
            fields = command.get('ArgumentFields', {}) if command is not None else {}
            for paramIndex in range(int(parametersPerFunc[index])):
                typeStr = 'int'
                if command is not None and paramIndex < len(command['Arguments']):
                    typeStr = TYPES.get(command['Arguments'][paramIndex].lower(), 'int')
                paramName = names[paramIndex] if paramIndex < len(names) and names[paramIndex] else "param" + str(paramIndex + 1)
                packed = fields.get(str(paramIndex))
                if packed:
                    # the fields the game packs into the dword, each with its width in bits
                    params.append(paramName + " { " + ", ".join(
                        ("bool " + f['Name']) if f['Type'] == 'bool' else (f['Type'] + " " + f['Name'] + " : " + str(f['Width'])) for f in packed) + " }")
                else:
                    params.append(typeStr + " " + paramName)
            actionDefinition += ", ".join(params)
        actionDefinition += ")"
        aliases = command.get('Aliases', []) if command is not None else []
        if aliases:
            actionDefinition += " [" + ", ".join(aliases) + "]"
        actionDefinition += " : " + str(index) + ";"
        actionDefinitionResultText += actionDefinition + "\n"
    with open(outputPath, 'w', newline='\r\n') as res:
        res.write(actionDefinitionResultText)

for definitions, output in (('AgentLabDefsPS2.json', 'AgentLab/ActionDefinitionsPs2.lab'), ('AgentLabDefsXbox.json', 'AgentLab/ActionDefinitionsXbox.lab')):
    with open(definitions, 'r', encoding='utf-8-sig') as f:
        agentLabDefs = json.load(f)
        generateFile(agentLabDefs, output)
