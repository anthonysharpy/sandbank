using Sandbox;
using System;
using System.Threading.Tasks;

namespace SandbankDatabase;

/// <summary>
/// Polls stuff in the database at short intervals.
/// </summary>
internal static class Ticker
{
	private static float _timeSinceTickedBackups = 0;
	private static float _timeSinceTickedCache = 0;
	private static float _timeSinceTickedPool = 0;

	public static void Initialise()
	{
		// It's really important that this gets its own thread. Putting it in an async task would block up the
		// default worker threads, which could cause freezes if it gets in the way of user code.
		GameTask.RunInThreadAsync( async () =>
		{
			_ = BackgroundTicker();
		} );
	}

	private static async Task BackgroundTicker()
	{
		Logging.Log( "initialising ticker..." );

		var lastCheckTime = DateTime.UtcNow;

		while ( InitialisationController.CurrentDatabaseState == DatabaseState.Initialised )
		{
			if ( !Game.IsPlaying && !TestHelpers.IsUnitTests )
			{
				InitialisationController.CurrentDatabaseState = DatabaseState.ShuttingDown;
				break;
			}

			if ( _timeSinceTickedBackups >= 10 )
			{
				_timeSinceTickedBackups = 0;
				TickBackups();
			}
			if ( _timeSinceTickedCache >= Config.TICK_DELTA )
			{
				_timeSinceTickedCache = 0;
				TickCache();
			}
			if ( _timeSinceTickedPool >= 1 )
			{
				_timeSinceTickedPool = 0;
				TickPool();
			}

			await Task.Delay( 100 );

			// TimeSince does not work at all. So let's do it manually.
			var difference = (float)(DateTime.UtcNow - lastCheckTime).TotalSeconds;
			_timeSinceTickedPool += difference;
			_timeSinceTickedCache += difference;
			_timeSinceTickedBackups += difference;

			lastCheckTime = DateTime.UtcNow;
		}

		// Try and shut down the database.
		ShutdownController.ShutdownDatabase();
	}

	private static void TickBackups()
	{
		Backups.CheckBackupStatus();
	}

	private static void TickCache()
	{
		Cache.Tick();
	}

	private static void TickPool()
	{
		ObjectPool.CheckPool();
	}
}
