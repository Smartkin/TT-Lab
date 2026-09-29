using System;

namespace TT_Lab.Project.Build;

// What a build couldn't write, named after the asset and the chunk it was writing it for, the cause as the inner exception
public sealed class BuildException(string what, Exception inner) : Exception($"{what}: {inner.Message}", inner)
{
    public string What { get; } = what;
}
