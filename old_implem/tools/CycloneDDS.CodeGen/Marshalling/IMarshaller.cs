namespace CycloneDDS.CodeGen.Marshalling;

/// <summary>
/// Interface for marshalling between a managed user type and a native blittable struct.
/// </summary>
/// <typeparam name="TManaged">The user-facing managed type (class).</typeparam>
/// <typeparam name="TNative">The native blittable struct type.</typeparam>
public interface IMarshaller<TManaged, TNative>
{
    /// <summary>
    /// Marshal data from the managed object to the native struct.
    /// </summary>
    /// <param name="managed">The source managed object.</param>
    /// <param name="native">The destination native struct.</param>
    void Marshal(TManaged managed, ref TNative native);

    /// <summary>
    /// Unmarshal data from the native struct to a new managed object.
    /// </summary>
    /// <param name="native">The source native struct.</param>
    /// <returns>A new managed object containing the data.</returns>
    TManaged Unmarshal(ref TNative native);
}
