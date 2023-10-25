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
		if ( Config.ENABLE_LOGGING )
			Log.Info( $"Sandbank: creating collection write lock for collection \"{collection}\"" );

		_collectionWriteLocks[collection] = new();
	}

	/// <summary>
	/// Returns true on success.
	/// </summary>
	public static bool DeleteDocument( string collection, string documentID )
	{
		try
		{
			lock ( _collectionWriteLocks[collection] ) 
			{
				FileSystem.Data.DeleteFile( $"sandbank/{collection}/{documentID}" );
			}

			return true;
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	/// Returns true on success.
	/// </summary>
	public static bool SaveDocument(string collection, Document document, Type documentClassType)
	{
		try
		{
			string data = Serialisation.SerialiseClass( document.Data, documentClassType );

			lock ( _collectionWriteLocks[collection] )
			{
				FileSystem.Data.WriteAllText( $"sandbank/{collection}/{document.ID}", data );
			}

			return true;
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	/// The second return value indicates success.
	/// </summary>
	public static (List<string>, bool) ListCollectionNames()
	{
		try
		{
			return ( FileSystem.Data.FindDirectory( "sandbank" ).ToList(), true );
		}
		catch
		{
			return (null, false);
		}
	}

	/// <summary>
	/// The second return value indicates success.
	/// </summary>
	public static (Collection, bool) LoadCollectionDefinition(string collectionName)
	{
		try
		{
			string data;

			lock ( _collectionWriteLocks[collectionName] )
			{
				data = FileSystem.Data.ReadAllText( $"sandbank/{collectionName}/definition.txt" );
			}

			var collection = Serialisation.DeserialiseClass<Collection>( data );

			collection.DocumentClassType = GlobalGameNamespace.TypeLibrary
				.GetType( collection.DocumentClassTypeSerialized )
				.TargetType;

			return (collection, true);

		}
		catch
		{
			return (null, false);
		}
	}

	/// <summary>
	/// The second return value indicates success.
	/// </summary>
	public static (List<Document>, bool) LoadAllCollectionsDocuments( Collection collection )
	{
		try
		{
			List<Document> output = new();

			lock ( _collectionWriteLocks[collection.CollectionName] )
			{
				var files = FileSystem.Data.FindFile( $"sandbank/{collection.CollectionName}/" )
					.Where( x => x != "definition.txt" )
					.ToList();

				foreach ( var file in files )
				{
					string contents;

					contents = FileSystem.Data.ReadAllText( $"sandbank/{collection.CollectionName}/{file}" );

					var document = new Document( Serialisation.DeserialiseClass( contents, collection.DocumentClassType ), null, false );
					output.Add( document );
				}
			}

			return (output, true);
		}
		catch
		{
			return (null, false);
		}
	}

	/// <summary>
	/// Returns true on success.
	/// </summary>
	public static bool SaveCollectionDefinition(Collection collection)
	{
		try
		{
			var data = Serialisation.SerialiseClass( collection );

			lock ( _collectionWriteLocks[collection.CollectionName] )
			{
				if ( !FileSystem.Data.DirectoryExists( $"sandbank/{collection.CollectionName}" ) )
					FileSystem.Data.CreateDirectory( $"sandbank/{collection.CollectionName}" );

				FileSystem.Data.WriteAllText( $"sandbank/{collection.CollectionName}/definition.txt", data );
			}

			return true;
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	/// Returns true on success.
	/// </summary>
	private static bool DeleteCollection(string name)
	{
		try
		{
			lock ( _collectionWriteLocks[name] )
			{
				FileSystem.Data.DeleteDirectory( $"sandbank/{name}", true );
			}

			return true;
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	/// Wipes all sandbank files. Requires unsafe mode. Returns true
	/// on success or if safe mode is not enabled.
	/// </summary>
	public static bool WipeFilesystem()
	{
		try
		{
			if ( !Config.UNSAFE_MODE )
			{
				Log.Warning( "Sandbank: must enable unsafe mode to call WipeFilesystem() - see Config.cs" );
				return true;
			}

			var (collections, success) = ListCollectionNames();

			if ( success == false )
				return false;

			// Don't delete collection folders when we are half-way through writing to them.
			lock ( Cache.WriteInProgressLock )
			{
				foreach ( var collection in collections )
				{
					if ( !DeleteCollection( collection ) )
						return false;
				}
			}

			return true;
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	/// Returns true on success.
	/// </summary>
	public static bool EnsureFileSystemSetup()
	{
		try
		{
			if ( !FileSystem.Data.DirectoryExists( "sandbank" ) )
				FileSystem.Data.CreateDirectory( "sandbank" );

			return true;
		}
		catch
		{
			return false;
		}
	}
}
