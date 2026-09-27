using System;

namespace TT_Lab.Attributes;

/// <summary>
/// Data chunks don't get values of their own of, like an object's name, which the game never reads (it only goes by objects' IDs)
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class NoChunkOverridesAttribute : Attribute;
