using System.Threading;
using System.Threading.Tasks;

namespace FeatureDemo.Scenarios;

/// <summary>
/// Defines the contract for all feature demonstration scenarios.
/// </summary>
public interface IDemoScenario
{
    /// <summary>
    /// Gets the display name of the scenario.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets a short description of what the scenario demonstrates.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Runs the scenario in standalone mode (both publisher and subscriber in same process).
    /// </summary>
    /// <param name="ct">Cancellation token to stop the scenario.</param>
    Task RunStandaloneAsync(CancellationToken ct);

    /// <summary>
    /// Runs the publisher side of the scenario (Master node).
    /// </summary>
    /// <param name="ct">Cancellation token to stop the scenario.</param>
    Task RunPublisherAsync(CancellationToken ct);

    /// <summary>
    /// Runs the subscriber side of the scenario (Slave node).
    /// </summary>
    /// <param name="ct">Cancellation token to stop the scenario.</param>
    Task RunSubscriberAsync(CancellationToken ct);

    /// <summary>
    /// Displays instructions or information about the scenario to the user.
    /// </summary>
    void DisplayInstructions();
}
