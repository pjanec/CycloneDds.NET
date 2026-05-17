using System;
using System.Collections.Generic;

namespace DdsMonitor.Engine;

/// <summary>
/// Registry that allows plugins to contribute custom macro functions that can be
/// invoked by name inside filter expressions compiled by <see cref="FilterCompiler"/>.
/// </summary>
/// <example>
/// A plugin registers "DistanceTo" so that users can write filter expressions like:
/// <c>DistanceTo(Payload.X, Payload.Y, 0.0, 0.0) &lt; 5.0</c>
/// </example>
public interface IFilterMacroRegistry
{
    /// <summary>
    /// Registers a named macro function.  The function receives an array of arguments
    /// (the values passed in the filter expression) and returns a result.
    /// Registering with an existing name replaces the previous implementation.
    /// </summary>
    /// <param name="name">
    /// The identifier used in filter expressions (case-sensitive, must be a valid
    /// C# identifier and must not collide with LINQ method names).
    /// </param>
    /// <param name="impl">The implementation invoked at filter evaluation time.</param>
    void RegisterMacro(string name, Func<object?[], object?> impl);

    /// <summary>
    /// Returns a snapshot of all currently registered macros keyed by name.
    /// </summary>
    IReadOnlyDictionary<string, Func<object?[], object?>> GetMacros();
}
