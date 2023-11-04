using Sandbox;
using Sandbox.Internal;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace NSSandbank;

static class FileIO
{
	/// <summary>
	/// Only let one thread write/read a collection at a time using this lock.
	/// </summary>
	private static Dictionary<string, object> _collectionWriteLocks = new();

	public static void CreateCollectionLock(string collection)
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
				FileSystem.Data.DeleteFile( $"{Config.DATABASE_NAME}/{collection}/{documentID}" );
			}

			return null;
		}
		catch (Exception e)
		{
			return e.Message;
		}
	}

	/// <summary>
	/// Returns null on success, or the error message on failure.
	/// </summary>
	public static string SaveDocument(string collection, Document document, Type documentClassType)
	{
		try
		{
			string data = Serialisation.SerialiseClass( document.Data, documentClassType );

			lock ( _collectionWriteLocks[collection] )
			{
				FileSystem.Data.WriteAllText( $"{Config.DATABASE_NAME}/{collection}/{document.ID}", data );
			}

			return null;
		}
		catch (Exception e)
		{
			return e.Message;
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
			return ( FileSystem.Data.FindDirectory( Config.DATABASE_NAME ).ToList(), null );
		}
		catch (Exception e)
		{
			return (null, e.Message);
		}
	}

	/// <summary>
	/// The second return value contains the error message (or null if successful).
	/// </summary>
	public static (Collection, string) LoadCollectionDefinition(string collectionName)
	{
		try
		{
			string data;

			if ( !_collectionWriteLocks.ContainsKey(collectionName) )
				CreateCollectionLock(collectionName);

			lock ( _collectionWriteLocks[collectionName] )
			{
				data = FileSystem.Data.ReadAllText( $"{Config.DATABASE_NAME}/{collectionName}/definition.txt" );
			}

			var collection = Serialisation.DeserialiseClass<Collection>( data );

			collection.DocumentClassType = GlobalGameNamespace.TypeLibrary
				.GetType( collection.DocumentClassTypeSerialized )
				.TargetType;

			return (collection, null);

		}
		catch (Exception e)
		{
			return (null, e.Message);
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
				var files = FileSystem.Data.FindFile( $"{Config.DATABASE_NAME}/{collection.CollectionName}/" )
					.Where( x => x != "definition.txt" )
					.ToList();

				foreach ( var file in files )
				{
					string contents;

					contents = FileSystem.Data.ReadAllText( $"{Config.DATABASE_NAME}/{collection.CollectionName}/{file}" );

					var document = new Document( Serialisation.DeserialiseClass( contents, collection.DocumentClassType ), null, false );
					output.Add( document );
				}
			}

			return (output, null);
		}
		catch (Exception e)
		{
			return (null, e.Message);
		}
	}

	/// <summary>
	/// Returns null on success, or the error message on failure.
	/// </summary>
	public static string SaveCollectionDefinition(Collection collection)
	{
		try
		{
			var data = Serialisation.SerialiseClass( collection );

			lock ( _collectionWriteLocks[collection.CollectionName] )
			{
				if ( !FileSystem.Data.DirectoryExists( $"{Config.DATABASE_NAME}/{collection.CollectionName}" ) )
					FileSystem.Data.CreateDirectory( $"{Config.DATABASE_NAME}/{collection.CollectionName}" );

				FileSystem.Data.WriteAllText( $"{Config.DATABASE_NAME}/{collection.CollectionName}/definition.txt", data );
			}

			return null;
		}
		catch (Exception e)
		{
			return e.Message;
		}
	}

	/// <summary>
	/// Returns null on success, or the error message on failure.
	/// </summary>
	private static string DeleteCollection(string name)
	{
		try
		{
			lock ( _collectionWriteLocks[name] )
			{
				FileSystem.Data.DeleteDirectory( $"{Config.DATABASE_NAME}/{name}", true );
			}

			return null;
		}
		catch (Exception e)
		{
			return e.Message;
		}
	}

	/// <summary>
	/// Wipes all sandbank files. Requires unsafe mode. Returns null on success
	/// or if safe mode is not enabled, and the error message on failure.
	/// </summary>
	public static string WipeFilesystem()
	{
		try
		{
			if ( !Config.UNSAFE_MODE )
			{
				Log.Warning( "must enable unsafe mode to call WipeFilesystem() - see Config.cs" );
				return null;
			}

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
		catch (Exception e)
		{
			return e.Message;
		}
	}

	/// <summary>
	/// Returns null on success, or the error message on failure.
	/// </summary>
	public static string EnsureFileSystemSetup()
	{
		try
		{
			if ( !FileSystem.Data.DirectoryExists( Config.DATABASE_NAME ) )
				FileSystem.Data.CreateDirectory( Config.DATABASE_NAME );

			return null;
		}
		catch (Exception e)
		{
			return e.Message;
		}
	}
}
