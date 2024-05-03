using Sandbox;
using System.Threading.Tasks;

namespace SandbankDatabase;

internal class Ticker
{
	public static void Initialise()
	{
		GameTask.RunInThreadAsync( async () =>
		{
			Logging.Log( "Initialising ticker..." );

			while(Game.IsPlaying || TestHelpers.IsUnitTests )
			{
				Cache.Tick();
				ObjectPool.TryCheckPool();

				if ( Config.ENABLE_LOGGING )
					Logging.PrintStatus();

				await Task.Delay( Config.TICK_DELTA );
			}
		} );
	}
}
