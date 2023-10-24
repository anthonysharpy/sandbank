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
	/// Only let one thread write/read the disk at a time using this lock.
	/// </summary>
	public static object DiskInUseLock = new();

	/// <summary>
	/// Returns true on success.
	/// </summary>
	public static bool DeleteDocument( string collection, string documentID )
	{
		try
		{
			lock (DiskInUseLock ) 
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

			lock ( DiskInUseLock )
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
			lock ( DiskInUseLock )
			{
				return (FileSystem.Data.FindDirectory( "sandbank" ).ToList(), true);
			}
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

			lock ( DiskInUseLock )
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
			List<string> files = new();

			lock ( DiskInUseLock )
			{
				files = FileSystem.Data.FindFile( $"sandbank/{collection.CollectionName}/" )
					.Where( x => x != "definition.txt" )
					.ToList();
			}

			List<Document> output = new();

			foreach ( var file in files )
			{
				string contents;

				lock ( DiskInUseLock )
				{
					contents = FileSystem.Data.ReadAllText( $"sandbank/{collection.CollectionName}/{file}" );
				}

				var document = new Document( Serialisation.DeserialiseClass( contents, collection.DocumentClassType ), null, false );
				output.Add( document );
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
			 
			lock ( DiskInUseLock )
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
			lock ( DiskInUseLock )
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

			foreach ( var collection in collections )
			{
				if ( !DeleteCollection( collection ) )
					return false;
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
			lock ( DiskInUseLock )
			{
				if ( !FileSystem.Data.DirectoryExists( "sandbank" ) )
					FileSystem.Data.CreateDirectory( "sandbank" );
			}

			return true;
		}
		catch
		{
			return false;
		}
	}
}
