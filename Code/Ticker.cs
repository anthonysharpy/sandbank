using Sandbox;
using System.Threading.Tasks;

namespace SandbankDatabase;

/// <summary>
/// Polls stuff in the database at short intervals.
/// </summary>
internal static class Ticker
{
	public static void Initialise()
	{
		CacheTicker();
		BackupTicker();
	}

	private static async void BackupTicker()
	{
		Logging.Log( "Initialising backup ticker..." );

		while ( Game.IsPlaying || TestHelpers.IsUnitTests )
		{
			Backups.CheckBackupStatus();
			await Task.Delay( 1000 * 10 );
		}
	}

	private static async void CacheTicker()
	{
		Logging.Log( "Initialising cache ticker..." );

		while ( Game.IsPlaying || TestHelpers.IsUnitTests )
		{
			Cache.Tick();
			ObjectPool.TryCheckPool();

			await Task.Delay( (int)(Config.TICK_DELTA * 1000f) );
		}

		// We also try to "reset" the database when calling Initialise(). However, this doesn't
		// work right now, because static fields don't wipe on stop/play, so if someone does a 
		// request before Initialise() is called after playing the game for a second time, it will
		// think it's initialised when it's actually not. This can lead to subtle errors. So, let's
		// shutdown the database here.
		Shutdown.ShutdownDatabase();
	}
}
