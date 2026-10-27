using System;
using Twinsanity.AgentLab.AbstractSyntaxTree;

namespace Twinsanity.AgentLab;

// The arithmetic of a script's values, which the compiler works out: integers stay integers (an operation going past 32 bits or a
// division by zero is an error), a float on either side makes a float, strings add up. Every value of a script is known when it's
// compiled, its literals and consts
internal static class AgentLabValues
{
    public static object Apply(BinaryOperationNode operation, object left, object right)
    {
        var token = operation.Token;
        if (left is string leftText && right is string rightText && token.Type == AgentLabToken.TokenType.AddOperator)
        {
            return leftText + rightText;
        }

        if (left is int a && right is int b)
        {
            if (token.Type == AgentLabToken.TokenType.DivideOperator && b == 0)
            {
                throw Error("Division by zero", operation);
            }

            try
            {
                return token.Type switch
                {
                    AgentLabToken.TokenType.AddOperator => checked(a + b),
                    AgentLabToken.TokenType.SubtractOperator => checked(a - b),
                    AgentLabToken.TokenType.MultiplyOperator => checked(a * b),
                    _ => checked(a / b)
                };
            }
            catch (OverflowException)
            {
                throw Error($"{operation} is past what a 32 bit integer holds", operation);
            }
        }

        if (left is int or float && right is int or float)
        {
            var x = ToFloat(left);
            var y = ToFloat(right);
            return token.Type switch
            {
                AgentLabToken.TokenType.AddOperator => x + y,
                AgentLabToken.TokenType.SubtractOperator => x - y,
                AgentLabToken.TokenType.MultiplyOperator => x * y,
                _ => x / y
            };
        }

        throw Error($"Binary operation {token} of {Describe(left)} and {Describe(right)} isn't supported", operation);
    }

    private static float ToFloat(object value) => value is int i ? i : (float)value;

    private static string Describe(object value) => value == null ? "nothing" : value.GetType().Name;

    private static AgentLabSyntaxException Error(string message, BinaryOperationNode operation)
    {
        return new AgentLabSyntaxException(message, operation.Token.Line, operation.Token.Column);
    }
}
