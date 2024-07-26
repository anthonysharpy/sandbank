using Sandbox.Internal;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace SandbankDatabase;

internal static class FileController
{
	/// <summary>
	/// Only let one thread write/read a collection at a time using this lock.
	/// </summary>
	private static Dictionary<string, object> _collectionWriteLocks = new();
	private static IFileIOProvider IOProvider;  
	
	public static void Initialise()
	{
		// Don't re-create if it already exists. Otherwise in the unit tests we
		// lose all of the files after initialisation.
		if ( IOProvider == null )
		{
			Logging.Log( "recreating file IO provider..." );

			if ( TestHelpers.IsUnitTests )
				IOProvider = new MockFileIOProvider();
			else
				IOProvider = new FileIOProvider();
		}
	}

	public static void CreateCollectionLock( string collection )
	{
		Logging.Log( $"creating collection write lock for collection \"{collection}\"" );

		_collectionWriteLocks[collection] = new();
	}

	/// <summary>
	/// Returns null on success, or the error message on failure.
	/// </summary>
	public static string DeleteDocument( string collection, string documentID )
	{
		try
		{
			lock ( _collectionWriteLocks[collection] )
			{
				IOProvider.DeleteFile( $"{Config.DATABASE_NAME}/{collection}/{documentID}" );
			}

			return null;
		}
		catch ( Exception e )
		{
			return Logging.ExtractExceptionString( e );
		}
	}

	/// <summary>
	/// Returns null on success, or the error message on failure.
	/// </summary>
	public static string SaveDocument( Document document )
	{
		try
		{
			string data = Serialisation.SerialiseClass( document.Data, document.DocumentType );

			lock ( _collectionWriteLocks[document.CollectionName] )
			{
				IOProvider.WriteAllText( $"{Config.DATABASE_NAME}/{document.CollectionName}/{document.UID}", data );
			}

			return null;
		}
		catch ( Exception e )
		{
			return Logging.ExtractExceptionString( e );
		}
	}

	/// <summary>
	/// The second return value is null on success, and contains the error message
	/// on failure.
	/// </summary>
	public static (List<string>, string) ListCollectionNames()
	{
		try
		{
			return (IOProvider.FindDirectory( Config.DATABASE_NAME ).ToList(), null);
		}
		catch ( Exception e )
		{
			return (null, Logging.ExtractExceptionString( e ));
		}
	}

	/// <summary>
	/// The second return value contains the error message (or null if successful).
	/// </summary>
	public static (Collection, string) LoadCollectionDefinition( string collectionName )
	{
		try
		{
			string data;

			if ( !_collectionWriteLocks.ContainsKey( collectionName ) )
				CreateCollectionLock( collectionName );

			lock ( _collectionWriteLocks[collectionName] )
			{
				data = IOProvider.ReadAllText( $"{Config.DATABASE_NAME}/{collectionName}/definition.txt" );
			}

			if ( data == null )
				return (null, $"no definition.txt for collection \"{collectionName}\" found - see RepairGuide.txt");

			Collection collection;

			try
			{
				collection = Serialisation.DeserialiseClass<Collection>( data );
			}
			catch ( Exception e )
			{
				return (null, $"error thrown when deserialising definition.txt for \"{collectionName}\": " + Logging.ExtractExceptionString( e ));
			}

			if ( collection.CollectionName != collectionName )
				return (null, $"failed to load definition.txt for collection \"{collectionName}\" - the CollectionName in the definition.txt differed from the name of the directory ({collectionName} vs {collection.CollectionName}) - see RepairGuide.txt");

			try
			{
				collection.DocumentClassType = GlobalGameNamespace.TypeLibrary
					.GetType( collection.DocumentClassTypeSerialized )
					.TargetType;
			}
			catch ( Exception e )
			{
				return (null, $"couldn't load the type described by the definition.txt for collection \"{collectionName}\" - most probably you renamed or removed your data type - see RepairGuide.txt: " + Logging.ExtractExceptionString( e ));
			}

			return (collection, null);
		}
		catch ( Exception e )
		{
			return (null, Logging.ExtractExceptionString( e ));
		}
	}

	/// <summary>
	/// The second return value contains the error message (or null if successful).
	/// </summary>
	public static (List<Document>, string) LoadAllCollectionsDocuments( Collection collection )
	{
		try
		{
			List<Document> output = new();

			lock ( _collectionWriteLocks[collection.CollectionName] )
			{
				var files = IOProvider.FindFile( $"{Config.DATABASE_NAME}/{collection.CollectionName}/" )
					.Where( x => x != "definition.txt" )
					.ToList();

				foreach ( var file in files )
				{
					var contents = IOProvider.ReadAllText( $"{Config.DATABASE_NAME}/{collection.CollectionName}/{file}" );

					try
					{
						var document = new Document( Serialisation.DeserialiseClass( contents, collection.DocumentClassType ), 
							collection.DocumentClassType, 
							false,
							collection.CollectionName );

						if ( file != document.UID )
							return (null, $"failed loading document \"{file}\": the filename does not match the UID ({file} vs {document.UID}) - see RepairGuide.txt");

						output.Add( document );
					}
					catch ( Exception e )
					{
						return (null, $"failed loading document \"{file}\" - your JSON is probably invalid: " + Logging.ExtractExceptionString( e ) );
					}
				}
			}

			return (output, null);
		}
		catch ( Exception e )
		{
			return (null, Logging.ExtractExceptionString( e ));
		}
	}

	/// <summary>
	/// Returns null on success, or the error message on failure.
	/// </summary>
	public static string SaveCollectionDefinition( Collection collection )
	{
		try
		{
			var data = Serialisation.SerialiseClass( collection );

			lock ( _collectionWriteLocks[collection.CollectionName] )
			{
				if ( !IOProvider.DirectoryExists( $"{Config.DATABASE_NAME}/{collection.CollectionName}" ) )
					IOProvider.CreateDirectory( $"{Config.DATABASE_NAME}/{collection.CollectionName}" );

				IOProvider.WriteAllText( $"{Config.DATABASE_NAME}/{collection.CollectionName}/definition.txt", data );
			}

			return null;
		}
		catch ( Exception e )
		{
			return Logging.ExtractExceptionString( e );
		}
	}

	/// <summary>
	/// Returns null on success, or the error message on failure.
	/// </summary>
	private static string DeleteCollection( string name )
	{
		try
		{
			lock ( _collectionWriteLocks[name] )
			{
				IOProvider.DeleteDirectory( $"{Config.DATABASE_NAME}/{name}", true );
			}

			return null;
		}
		catch ( Exception e )
		{
			return Logging.ExtractExceptionString( e );
		}
	}

	/// <summary>
	/// Wipes all sandbank files. Returns null on success and the error message on failure.
	/// </summary>
	public static string WipeFilesystem()
	{
		try
		{
			var (collections, error) = ListCollectionNames();

			if ( error != null )
				return $"failed to wipe filesystem: {error}";

			// Don't delete collection folders when we are half-way through writing to them.
			lock ( Cache.WriteInProgressLock )
			{
				foreach ( var collection in collections )
				{
					error = DeleteCollection( collection );

					if ( error != null )
						return $"failed to wipe filesystem: {error}";
				}
			}

			return null;
		}
		catch ( Exception e )
		{
			return Logging.ExtractExceptionString( e );
		}
	}

	/// <summary>
	/// Returns null on success, or the error message on failure.
	/// </summary>
	public static string EnsureFileSystemSetup()
	{
		try
		{
			if ( !IOProvider.DirectoryExists( Config.DATABASE_NAME ) )
				IOProvider.CreateDirectory( Config.DATABASE_NAME );

			return null;
		}
		catch ( Exception e )
		{
			return Logging.ExtractExceptionString( e );
		}
	}
}
