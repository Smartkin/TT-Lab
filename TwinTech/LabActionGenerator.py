import json5 as json

def generateFile(labDefs):
    commandSizes = labDefs['CommandSizes']
    parametersPerFunc = []
    actionDefinitionResultText = ""
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
        if str(index) in commands:
            name = commands[str(index)]['Name']
        if parametersPerFunc[index] == -1:
            name += "_DELETED"
        actionDefinition += name
        actionDefinition += "("
        if parametersPerFunc[index] > 0:
            for paramIndex in range(int(parametersPerFunc[index])):
                typeStr = "int "
                if str(index) in commands:
                    storedType = commands[str(index)]["Arguments"][paramIndex]
                    if storedType == 'single':
                        typeStr = 'float '
                if paramIndex != int(parametersPerFunc[index] - 1):
                    actionDefinition += typeStr + "param" + str(paramIndex + 1) + ", "
                else:
                    actionDefinition += "int param" + str(paramIndex + 1)
        actionDefinition += ") : " + str(index) + ";"
        actionDefinitionResultText += actionDefinition + "\n"
    with open('AgentLab/ActionDefinitionsPs2.lab', 'w') as res:
        res.write(actionDefinitionResultText)

with open('AgentLabDefsPS2.json', 'r') as f:
    agentLabDefs = json.load(f)
    generateFile(agentLabDefs)