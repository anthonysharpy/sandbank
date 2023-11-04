using Sandbox;
using System;
using System.Collections.Generic;

namespace NSSandbank;

static class Initialisation
{
	public static bool IsInitialised { get; private set; }

	private static bool _initialisationAttempted = false;

	private static void Initialise()
	{
		Log.Info( "==================================" );
		Log.Info( "Initialising Sandbank..." );

		try
		{
			// Sometimes this doesn't get cleared between games somehow so we wipe
			// it manually.
			Cache.WipeCollectionsCache();

			EnsureFilesystemSetup();
			LoadCollections();
			IsInitialised = true;
		}
		catch (Exception e)
		{
			Logging.Error( $"failed to initialise database - the database will now not start: {e.Message}" );
		}
		finally
		{
			Log.Info( "Sandbank initialisation finished" );
			Log.Info( "==================================" );
		}
	}

	private static void EnsureFilesystemSetup()
	{
		int attempt = 0;
		string error = "";

		while ( true )
		{
			if ( attempt++ >= 10 )
				throw new Exception( "failed to ensure filesystem is setup after 10 tries: " + error );

			error = FileIO.EnsureFileSystemSetup();

			if (error == null)
				return;

			GameTask.Delay( 50 );
		}
	}

	private static void LoadCollections()
	{
		int attempt = 0;
		string error = "";
		List<string> collectionNames;

		while ( true )
		{
			if ( attempt++ >= 10 )
				throw new Exception( "failed to load collection list after 10 tries: " + error );

			(collectionNames, error) = FileIO.ListCollectionNames();

			if (error == null)
				break;

			GameTask.Delay( 50 );
		}

		foreach ( var collectionName in collectionNames )
		{
			Logging.Log( $"attempting to load collection \"{collectionName}\"" );

			while ( true )
			{
				if ( attempt++ >= 10 )
					throw new Exception( $"failed to load collection {collectionName} after 10 tries: " + error );

				error = LoadCollection( collectionName );

				if ( error == null )
					break;

				GameTask.Delay( 50 );
			}
		}
	}

	/// <summary>
	/// Returns null on success or the error message on failure.
	/// </summary>
	private static string LoadCollection(string name)
	{
		var (definition, error) = FileIO.LoadCollectionDefinition( name );

		if ( error != null )
			return $"failed loading collection definition for collection \"{name}\": {error}";

		if (definition == null)
			return $"found a folder for collection {name} but the definition.txt was missing in that folder or failed to load";

		(var documents, error) = FileIO.LoadAllCollectionsDocuments( definition );

		if ( error != null )
			return $"failed loading documents for collection \"{name}\": {error}";

		Cache.CreateCollection( name, definition.DocumentClassType );
		Cache.InsertDocumentsIntoCollection( name, documents );

		Log.Info( $"Loaded collection {name} with {documents.Count} documents" );

		return null;
	}

	[GameEvent.Tick.Server]
	private static void Tick()
	{
		// I don't like this but it looks like s&box doesn't have an
		// on-server-start event yet so it'll have to do for now.
		if ( !IsInitialised && !_initialisationAttempted )
		{
			_initialisationAttempted = true;
			Initialise();
		}
	}
}
