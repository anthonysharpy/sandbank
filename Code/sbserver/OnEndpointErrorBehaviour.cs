namespace SandbankDatabase;

/// <summary>
/// Controls what happens when an endpoint is not successful.
/// </summary>
public enum OnEndpointErrorBehaviour
{
	/// <summary>
	/// Does nothing.
	/// </summary>
	DoNothing = 0,

	/// <summary>
	/// Logs a warning to the console.
	/// </summary>
	LogWarning = 1
}
