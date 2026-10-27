using System;
using Twinsanity.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace TT_Lab.Assets.Factory;

/// <summary>
/// What a build makes of its scripts' compiles
/// </summary>
internal static class ScriptCompiles
{
    /// <summary>
    /// The item the script compiled to, an error naming the script and what's wrong with it when it doesn't compile: the compiler keeps what
    /// it made before it failed (a graph without its states, a sequence cut off), which the build wrote without a word
    /// </summary>
    public static T ItemOf<T>(this AgentLabCompiler.CompilerResult result, IAsset owner) where T : ITwinAgentLab
    {
        if (result.CompilerStatus.IsError || !result.Contains<T>())
        {
            throw new InvalidOperationException($"{owner.Alias}'s script doesn't compile: {result.CompilerStatus.Message}");
        }

        return result.Get<T>();
    }
}
