using Sandbox;
using System.Security.Cryptography;

namespace NSSandbank;

static class Initialisation
{
	public static bool IsInitialised { get; private set; }

	private static void Initialise()
	{
		Log.Info( "==================================" );
		Log.Info( "Initialising Sandbank..." );

		// Sometimes this doesn't get cleared between games somehow so we wipe
		// it manually.
		Cache.WipeCollectionsCache();

		EnsureFilesystemSetup();
		LoadCollections();

		Log.Info( "Sandbank initialisation finished" );
		Log.Info( "==================================" );
	}

	private static void EnsureFilesystemSetup()
	{
		int attempt = 0;

		while ( true )
		{
			if ( attempt++ >= 10 )
				Logging.Throw( "failed to load collections after 10 tries - are the files in use by something else?" );

			var error = FileIO.EnsureFileSystemSetup();

			if (error == null)
				return;

			Logging.Log( $"failed to ensure filesystem setup: {error}" );

			GameTask.Delay( 50 );
		}
	}

	private static void LoadCollections()
	{
		int attempt = 0;

		while ( true )
		{
			if ( attempt++ >= 10 )
				Logging.Throw( "failed to load collections after 10 tries - are the files in use by something else?" );

			var (collectionNames, error) = FileIO.ListCollectionNames();

			if (error != null)
			{
				Logging.Log( $"failed to load collections: {error}" );
				goto retry;
			}

			foreach ( var collectionName in collectionNames )
			{
				Logging.Log( $"attempting to load collection \"{collectionName}\"" );

				if ( !LoadCollection( collectionName ) )
					goto retry;
			}

			break;

			retry:
			GameTask.Delay( 50 );
		}
	}

	/// <summary>
	/// Returns true on success or if the collection failed to load because of a missing
	/// definition file.
	/// </summary>
	private static bool LoadCollection(string name)
	{
		var (definition, error) = FileIO.LoadCollectionDefinition( name );

		if ( error != null)
		{
			Logging.Log( $"failed loading collection definition for \"{name}\": {error}" );
			return false;
		}

		if (definition == null)
		{
			Log.Warning( $"Found a folder for collection {name} but the definition.txt was missing in that folder or failed to load, skipping..." );
			return true;
		}

		(var documents, error) = FileIO.LoadAllCollectionsDocuments( definition );

		if ( error != null )
		{
			Logging.Log( $"failed loading collection documents for \"{name}\": {error}" );
			return false;
		}

		Cache.CreateCollection( name, definition.DocumentClassType );
		Cache.InsertDocumentsIntoCollection( name, documents );

		Log.Info( $"Loaded collection {name} with {documents.Count} documents" );

		return true;
	}

	[GameEvent.Tick.Server]
	private static void Tick()
	{
		// I don't like this but it looks like s&box doesn't have an
		// on-server-start event yet so it'll have to do for now.
		if (!IsInitialised )
		{
			IsInitialised = true;
			Initialise();
		}
	}
}
