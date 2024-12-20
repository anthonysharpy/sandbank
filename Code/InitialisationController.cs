using Sandbox;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SandbankDatabase;

sealed class InitialisationController : GameObjectSystem, ISceneStartup
{
	public static object DatabaseStateLock = new();
	public static DatabaseState CurrentDatabaseState;

	/// <summary>
	/// Prevents multiple threads trying to initialise/shut down the database at the same time.
	/// </summary>
	public static object InitialisationLock = new();

	public InitialisationController( Scene scene ) : base( scene ) { }

	void ISceneStartup.OnHostInitialize()
	{
		LoadHost();
	}

	void ISceneStartup.OnClientInitialize()
	{
		if ( Networking.IsClient && Config.CLIENTS_CAN_USE )
			LoadClient();
	}

	private void LoadHost()
	{
		ShutdownController.ShutdownHasBegun = false;
		Initialise();
	}

	private void LoadClient()
	{
		ShutdownController.ShutdownHasBegun = false;
		Initialise();
	}

	public static void Initialise()
	{
		if ( ShutdownController.ShutdownHasBegun )
			return;

		// The ticker, which runs in a background thread, is responsible for setting the database back to
		// an uninitialised state. If it's still shutting down for some reason, we should wait until it's
		// finished.
		while ( InitialisationController.CurrentDatabaseState != DatabaseState.Uninitialised )
		{
			if ( InitialisationController.CurrentDatabaseState == DatabaseState.Initialised )
				return;

			Task.Delay( 10 ).GetAwaiter().GetResult();
		}

		lock ( InitialisationLock )
		{
			if ( !Config.MERGE_JSON )
				Logging.ScaryWarn( "Config.MERGE_JSON is set to false - this will delete data if you rename or remove a data field" );

			if ( Config.STARTUP_SHUTDOWN_MESSAGES )
			{
				Log.Info( "==================================" );
				Log.Info( "Initialising Sandbank..." );
			}

			try
			{
				ShutdownController.WipeStaticFields();
				FileController.Initialise();
				FileController.EnsureFileSystemSetup();
				LoadCollections();

				lock ( DatabaseStateLock )
				{
					// Must set this before starting the ticker because the ticker kills itself when the database
					// is no longer initialised.
					CurrentDatabaseState = DatabaseState.Initialised;
				}

				Ticker.Initialise();

				if ( Config.STARTUP_SHUTDOWN_MESSAGES )
				{
					Log.Info( "Sandbank initialisation finished successfully" );
					Log.Info( "==================================" );
				}
			}
			catch ( Exception e )
			{
				Logging.Error( $"failed to initialise database: {Logging.ExtractExceptionString( e )}" );

				if ( Config.STARTUP_SHUTDOWN_MESSAGES )
				{
					Log.Info( "Sandbank initialisation finished unsuccessfully" );
					Log.Info( "==================================" );
				}
			}
		}
	}

	private static void LoadCollections()
	{
		int attempt = 0;
		string error = null;
		List<string> collectionNames;

		while ( true )
		{
			if ( attempt++ >= 10 )
				throw new SandbankException( $"failed to load collection list after 10 tries: {error}" );

			(collectionNames, error) = FileController.ListCollectionNames();

			if (error == null)
				break;
		}

		attempt = 0;

		foreach ( var collectionName in collectionNames )
		{
			Logging.Log( $"attempting to load collection \"{collectionName}\"" );

			while ( true )
			{
				if ( attempt++ >= 10 )
					throw new SandbankException( $"failed to load collection {collectionName} after 10 tries: {error}");

				error = LoadCollection( collectionName );

				if ( error == null )
					break;
			}
		}
	}

	/// <summary>
	/// Returns null on success or the error message on failure.
	/// </summary>
	private static string LoadCollection(string name)
	{
		var (definition, error) = FileController.LoadCollectionDefinition( name );

		if ( error != null )
			return $"failed loading collection definition for collection \"{name}\": {error}";

		if (definition == null)
			return $"found a folder for collection {name} but the definition.txt was missing in that folder or failed to load";

		(var documents, error) = FileController.LoadAllCollectionsDocuments( definition );

		if ( error != null )
			return $"failed loading documents for collection \"{name}\": {error}";

		Cache.CreateCollection( name, definition.DocumentClassType );
		Cache.InsertDocumentsIntoCollection( name, documents );

		Log.Info( $"Loaded collection {name} with {documents.Count} documents" );

		return null;
	}
}

internal enum DatabaseState
{
	Uninitialised,
	Initialised,
	ShuttingDown
}
