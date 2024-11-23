using Sandbox;
using System.Threading.Tasks;

namespace SandbankDatabase;

internal static class Ticker
{
	public static async void Initialise()
	{
		Logging.Log( "Initialising ticker..." );

		while( Game.IsPlaying || TestHelpers.IsUnitTests )
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
