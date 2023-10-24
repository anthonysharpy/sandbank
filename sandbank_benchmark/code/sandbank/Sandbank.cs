using NSSandbank;
using Sandbox;
using System;
using System.Collections.Generic;

static class Sandbank
{
	/// <summary>
	/// You should not rely on the database until this is true. If this is false,
	/// you may not find any data, or any data you write might be thrown away.
	/// </summary>
	public static bool IsInitialised => Initialisation.IsInitialised;

	/// <summary>
	/// Insert a document into the database. Returns the document with the ID set,
	/// unless it was already set.
	/// </summary>
	public static T Insert<T>( string collection, T document ) where T : class
	{
		var relevantCollection = Cache.GetCollectionByName<T>(collection);

		Document newDocument = new( document, typeof(T), true );
		relevantCollection.CachedDocuments[newDocument.ID] = newDocument;

		return document;
	}

	/// <summary>
	/// Insert a List of documents into the database. Returns the documents with the
	/// IDs set, unless they were already set.
	/// </summary>
	public static List<T> InsertMany<T>( string collection, List<T> documents ) where T : class
	{
		var relevantCollection = Cache.GetCollectionByName<T>( collection );

		foreach (var document in documents)
		{
			Document newDocument = new Document( document, typeof(T), true );
			relevantCollection.CachedDocuments[newDocument.ID] = newDocument;
		}

		return documents;
	}

	/// <summary>
	/// Fetch a single document from the database where selector evaluates to true.
	/// </summary>
	public static T SelectOne<T>( string collection, Func<T, bool> selector ) where T : class
	{
		var relevantCollection = Cache.GetCollectionByName<T>( collection );

		foreach ( var pair in relevantCollection.CachedDocuments )
		{
			if ( selector.Invoke( (T)pair.Value.Data ) )
				return (T)pair.Value.Data;
		}

		return null;
	}

	/// <summary>
	/// The same as SelectOne except slightly faster since we can look it up by ID.
	/// </summary>
	public static T? SelectOneWithID<T>( string collection, string id ) where T : class
	{
		var relevantCollection = Cache.GetCollectionByName<T>( collection );

		if ( !relevantCollection.CachedDocuments.ContainsKey( id ) )
			return null;

		return relevantCollection.CachedDocuments[id].Data as T;
	}

	/// <summary>
	/// Select all documents from the database where selector evaluates to true.
	/// </summary>
	public static List<T> Select<T>( string collection, Func<T, bool> selector ) where T : class
	{
		var relevantCollection = Cache.GetCollectionByName<T>( collection );
		List<T> output = new();

		foreach ( var pair in relevantCollection.CachedDocuments )
		{
			if ( selector.Invoke( (T)pair.Value.Data ) )
				output.Add( (T)pair.Value.Data );
		}

		return output;
	}

	/// <summary>
	/// Delete all documents from the database where selector evaluates to true.
	/// </summary>
	public static void Delete<T>( string collection, Predicate<T> selector ) where T : class
	{
		var relevantCollection = Cache.GetCollectionByName<T>( collection );
		List<string> idsToDelete = new();

		foreach ( var pair in relevantCollection.CachedDocuments )
		{
			if ( selector.Invoke( (T)pair.Value.Data ) )
				idsToDelete.Add( pair.Key );
		}

		foreach ( var id in idsToDelete )
		{
			relevantCollection.CachedDocuments.Remove( id, out _ );

			int attempt = 0;

			while ( true )
			{
				if ( attempt++ >= 10 )
					throw new Exception( $"Sandbank: failed to delete document from collection \"{collection}\" after 10 tries - is the file in use by something else?" );

				if ( FileIO.DeleteDocument( collection, id ) )
					break;

				GameTask.Delay( 50 );
			}
		}
	}

	/// <summary>
	/// The same as Delete except slightly faster since we can look it up by ID.
	/// </summary>
	public static void DeleteWithID<T>( string collection, string id) where T : class
	{
		var relevantCollection = Cache.GetCollectionByName<T>( collection );
		relevantCollection.CachedDocuments.Remove( id, out _ );

		int attempt = 0;

		while ( true )
		{
			if ( attempt++ >= 10 )
				throw new Exception( $"Sandbank: failed to delete document from collection \"{collection}\" after 10 tries - is the file in use by something else?" );

			if ( FileIO.DeleteDocument( collection, id ) )
				break;

			GameTask.Delay( 50 );
		}
	}

	/// <summary>
	/// Return whether there are any documents in the datbase where selector evalutes
	/// to true.
	/// </summary>
	public static bool Any<T>( string collection, Func<T, bool> selector ) where T : class
	{
		var relevantCollection = Cache.GetCollectionByName<T>( collection );

		foreach ( var pair in relevantCollection.CachedDocuments )
		{
			if ( selector.Invoke( (T)pair.Value.Data ) )
				return true;
		}

		return false;
	}

	/// <summary>
	/// The same as Any except slightly faster since we can look it up by ID.
	/// </summary>
	public static bool AnyWithID<T>( string collection, string id )
	{
		var relevantCollection = Cache.GetCollectionByName<T>( collection );
		return relevantCollection.CachedDocuments.ContainsKey( id );
	}

	/// <summary>
	/// Wipe everything, forever. Requires unsafe mode to be enabled.
	/// </summary>
	public static void WipeAllData()
	{
		if (!Config.UNSAFE_MODE)
		{
			Log.Warning( "Sandbank: must enable unsafe mode to call WipeAllData() - see Config.cs" );
			return;
		}

		Cache.WipeCollectionsCache();

		int attempt = 0;

		while ( true )
		{
			if ( attempt++ >= 10 )
				throw new System.Exception( "Sandbank: failed to load collections after 10 tries - are the files in use by something else?" );

			if ( FileIO.WipeFilesystem() )
				return;

			GameTask.Delay( 50 );
		}
	}

	/// <summary>
	/// Enables unsafe mode. Allows you to call risky parts of the API, such as
	/// WipeAllData().
	/// </summary>
	public static void EnableUnsafeMode()
	{
		Config.UNSAFE_MODE = true;
	}   

	/// <summary>
	/// Disables unsafe mode.
	/// </summary>
	public static void DisableUnsafeMode()
	{
		Config.UNSAFE_MODE = false;
	}

	/// <summary>
	/// Enables indenting of JSON. See comment under Config.INDENT_JSON
	/// for whether this is a good idea for you or not.
	/// </summary>
	public static void EnableIndentJSON()
	{
		Config.INDENT_JSON = true;
		Serialisation.UpdateJSONOptions();
	}

	/// <summary>
	/// Disables indenting of JSON. See comment under Config.INDENT_JSON
	/// for whether this is a good idea for you or not.
	/// </summary>
	public static void DisableIndentJSON()
	{
		Config.INDENT_JSON = false;
		Serialisation.UpdateJSONOptions();
	}
}
