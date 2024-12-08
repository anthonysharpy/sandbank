using Sandbox;
using System.Threading.Tasks;

namespace SandbankDatabase;

/// <summary>
/// Polls stuff in the database at short intervals.
/// </summary>
internal static class Ticker
{
	private static TimeSince TimeSinceTickedBackups = 0;
	private static TimeSince TimeSinceTickedCache = 0;
	private static TimeSince TimeSinceTickedPool = 0;

	public static void Initialise()
	{
		// It's really important that this gets its own thread. Putting it in an async task would block up the
		// default worker threads, which could cause freezes if it gets in the way of user code.
		GameTask.RunInThreadAsync( async () =>
		{
			BackgroundTicker();
		} );
	}

	private static async Task BackgroundTicker()
	{
		Logging.Log( "Initialising ticker..." );

		while ( Initialisation.CurrentDatabaseState == DatabaseState.Initialised )
		{
			if (TimeSinceTickedBackups >= 10)
			{
				TimeSinceTickedBackups = 0;
				TickBackups();
			}
			if (TimeSinceTickedCache >= Config.TICK_DELTA)
			{
				TimeSinceTickedCache = 0;
				TickCache();
			}
			if ( TimeSinceTickedPool >= 1 )
			{
				TimeSinceTickedPool = 0;
				TickPool();
			}

			await Task.Delay( 100 );
		}

		OnBackgroundTickerFinished();
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

	private static void OnBackgroundTickerFinished()
	{
		// We also try to "reset" the database when calling Initialise(). However, this doesn't
		// work right now, because static fields don't wipe on stop/play, so if someone does a 
		// request before Initialise() is called after playing the game for a second time, it will
		// think it's initialised when it's actually not. This can lead to subtle errors. So, let's
		// shutdown the database here.
		//
		// Also, going forward, this will probably be the place where shutdown is performed. The ticker
		// shuts down the database when it gets the signal that the database is no longer initialised.
		Shutdown.ShutdownDatabase();
	}
}
