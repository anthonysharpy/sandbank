using System;

namespace SandbankDatabase;

internal static class ShutdownController
{
	/// <summary>
	/// Once shutdown has been triggered, the database cannot be initialised again.
	/// </summary>
	public static bool ShutdownHasBegun = false;

	/// <summary>
	/// S&amp;box doesn't automatically wipe static fields yet so we have to do this
	/// ourselves.
	/// </summary>
	public static void WipeStaticFields()
	{
		Cache.WipeStaticFields();
		ObjectPool.WipeStaticFields();
		PropertyDescriptionsCache.WipeStaticFields();
		SandbankAutoSavedEventHandler.WipeStaticFields();
	}

	public static void ShutdownDatabase()
	{
		// Theoretical possibility that the database could be shut down during initialistion,
		// if closed quickly enough.
		lock ( InitialisationController.InitialisationLock )
		{
			if ( Config.STARTUP_SHUTDOWN_MESSAGES )
			{
				Log.Info( "==================================" );
				Log.Info( "Shutting down Sandbank..." );
			}

			try
			{
				Logging.Info( "shutting down database..." );

				Cache.ForceFullWrite();
				WipeStaticFields();
			}
			catch ( Exception e )
			{
				Logging.Error( $"failed to shutdown database properly - some data may have been lost: {Logging.ExtractExceptionString( e )}" );
			}
			finally
			{
				InitialisationController.CurrentDatabaseState = DatabaseState.Uninitialised;
			}

			if ( Config.STARTUP_SHUTDOWN_MESSAGES )
			{
				Log.Info( "Shutdown completed" );
				Log.Info( "==================================" );
			}
		}
	}
}
