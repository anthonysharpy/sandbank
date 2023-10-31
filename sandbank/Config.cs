namespace NSSandbank; 

static class Config
{
	/// <summary>
	/// This controls whether the written JSON files are indented or not.
	/// Indentation makes them more human-readable, but probably makes processing
	/// a little bit slower. You can edit this here or disable it by calling
	/// DisableIndentJSON().
	/// </summary>
	public static bool INDENT_JSON = true;
	/// <summary>
	/// The database will try to make sure that all stale data is written to disk
	/// at most every this many seconds. In the event of a crash, all stale data
	/// is lost, so lower numbers are safer. But lower numbers can lead to decreased
	/// performance under heavy loads due to increased disk writing.
	/// </summary>
	public const float PERSIST_EVERY_N_SECONDS = 10f;
	/// <summary>
	/// We will only try to perform a partial write this many times per second. A partial
	/// write doesn't write everything, so changing this will not really change
	/// your performance. But it does increase performance by ensuring that are we not 
	/// spamming writes every tick. You probably don't want to change this.
	/// </summary>
	public const int PARTIAL_WRITES_PER_SECOND = 4;
	/// <summary>
	/// Enables logging for helping to diagnose issues.
	/// </summary>
	public const bool ENABLE_LOGGING = false;
	/// <summary>
	/// If this is enabled then you can run risky parts of the API like functions that can
	/// wipe the database. This is off by default to stop people shooting themselves in
	/// the foot. You can edit this here or you can call EnableUnsafeMode() to enable it.
	/// </summary>
	public static bool UNSAFE_MODE = false;
}
