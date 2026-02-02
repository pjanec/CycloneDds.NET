namespace FeatureDemo.Orchestration;

/// <summary>
/// Operating mode for the demo application.
/// </summary>
public enum DemoMode
{
    /// <summary>
    /// Interactive mode - user selects mode at startup.
    /// </summary>
    Interactive,

    /// <summary>
    /// Standalone mode - runs both publisher and subscriber in same process.
    /// </summary>
    Standalone,

    /// <summary>
    /// Master mode - controls scenario selection and runs publisher side.
    /// </summary>
    Master,

    /// <summary>
    /// Slave mode - waits for commands and runs subscriber side.
    /// </summary>
    Slave,

    /// <summary>
    /// Autonomous mode - automatically runs all scenarios in sequence.
    /// </summary>
    Autonomous
}
