using Sandbox;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SandbankDatabase;

static class Initialisation
{
	public static bool IsInitialised { get; private set; }

	/// <summary>
	/// S&amp;box doesn't automatically wipe static fields yet so we have to do this
	/// ourselves.
	/// </summary>
	private static void WipeStaticFields()
	{
		IsInitialised = false;

		Cache.WipeStaticFields();
		FileController.WipeStaticFields();
		ObjectPool.WipeStaticFields();
		PropertyDescriptionsCache.WipeStaticFields();
	}

	public static void Initialise()
	{
		Log.Info( "==================================" );
		Log.Info( "Initialising Sandbank..." );

		try
		{
			ResetState();
			FileController.Initialise();
			EnsureFilesystemSetup();
			LoadCollections();
			Ticker.Initialise();

			IsInitialised = true;
		}
		catch ( Exception e )
		{
			Logging.Error( $"failed to initialise database - the database will now not start: {Logging.ExtractExceptionString( e )}" );
		}
		finally
		{
			Log.Info( "Sandbank initialisation finished" );
			Log.Info( "==================================" );
		}
	}

	/// <summary>
	/// Reset the database back to the state it was in before it was initialised.
	/// </summary>
	public static void ResetState()
	{
		WipeStaticFields();
	}

	private static void EnsureFilesystemSetup()
	{
		int attempt = 0;
		string error = "";

		while ( true )
		{
			if ( attempt++ >= 10 )
				throw new Exception( "failed to ensure filesystem is setup after 10 tries: " + error );

			error = FileController.EnsureFileSystemSetup();

			if (error == null)
				return;
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

			(collectionNames, error) = FileController.ListCollectionNames();

			if (error == null)
				break;
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
