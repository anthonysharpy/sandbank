﻿using NSSandbank;
using Sandbox;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

static class Sandbank
{
	/// <summary>
	/// Insert a document into the database. The document will have its ID set
	/// if it is empty.
	/// </summary>
	public static void Insert<T>( string collection, T document ) where T : class
	{
		var relevantCollection = Cache.GetCollectionByName<T>( collection, true );

		Document newDocument = new( document, typeof(T), true );
		relevantCollection.CachedDocuments[newDocument.ID] = newDocument;
	}

	/// <summary>
	/// Insert a List of documents into the database. The documents will have their IDs
	/// set, unless they are empty.
	/// </summary>
	public static void InsertMany<T>( string collection, List<T> documents ) where T : class
	{
		var relevantCollection = Cache.GetCollectionByName<T>( collection, true );

		foreach (var document in documents)
		{
			Document newDocument = new Document( document, typeof(T), true );
			relevantCollection.CachedDocuments[newDocument.ID] = newDocument;
		}
	}

	/// <summary>
	/// Fetch a single document from the database where selector evaluates to true.
	/// </summary>
	public static T SelectOne<T>( string collection, Func<T, bool> selector ) where T : class
	{
		var relevantCollection = Cache.GetCollectionByName<T>( collection, false );

		if ( relevantCollection == null )
			return null;

		foreach ( var pair in relevantCollection.CachedDocuments )
		{
			if ( selector.Invoke( (T)pair.Value.Data ) )
				return Serialisation.CloneObject((T)pair.Value.Data);
		}

		return null;
	}

	/// <summary>
	/// The same as SelectOne except slightly faster since we can look it up by ID.
	/// </summary>
	public static T? SelectOneWithID<T>( string collection, string id ) where T : class
	{
		var relevantCollection = Cache.GetCollectionByName<T>( collection, false );

		if ( relevantCollection == null )
			return null;

		relevantCollection.CachedDocuments.TryGetValue(id, out Document document);

		return document == null ? null : Serialisation.CloneObject((T)document.Data);
	}

	/// <summary>
	/// Select all documents from the database where selector evaluates to true.
	/// </summary>
	public static List<T> Select<T>( string collection, Func<T, bool> selector ) where T : class
	{
		var relevantCollection = Cache.GetCollectionByName<T>( collection, false );
		List<T> output = new();

		if ( relevantCollection == null )
			return output;

		foreach ( var pair in relevantCollection.CachedDocuments )
		{
			if ( selector.Invoke( (T)pair.Value.Data ) )
				output.Add( Serialisation.CloneObject( (T)pair.Value.Data) );
		}

		return output;
	}

	/// <summary>
	/// Delete all documents from the database where selector evaluates to true.
	/// </summary>
	public static void Delete<T>( string collection, Predicate<T> selector ) where T : class
	{
		var relevantCollection = Cache.GetCollectionByName<T>( collection, false );

		if ( relevantCollection == null )
			return;

		List<string> idsToDelete = new();

		foreach ( var pair in relevantCollection.CachedDocuments )
		{
			if ( selector.Invoke( (T)pair.Value.Data ) )
				idsToDelete.Add( pair.Key );
		}

		foreach ( var id in idsToDelete )
		{
			relevantCollection.CachedDocuments.TryRemove( id, out _ );

			int attempt = 0;

			while ( true )
			{
				if ( attempt++ >= 10 )
					throw new Exception( $"failed to delete document from collection \"{collection}\" after 10 tries - is the file in use by something else?" );

				if ( FileIO.DeleteDocument( collection, id ) == null )
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
		var relevantCollection = Cache.GetCollectionByName<T>( collection, false );

		if ( relevantCollection == null )
			return;

		relevantCollection.CachedDocuments.TryRemove( id, out _ );

		int attempt = 0;

		while ( true )
		{
			if ( attempt++ >= 10 )
				throw new Exception( $"failed to delete document from collection \"{collection}\" after 10 tries - is the file in use by something else?" );

			if ( FileIO.DeleteDocument( collection, id ) == null )
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
		var relevantCollection = Cache.GetCollectionByName<T>( collection, false );
				
		if ( relevantCollection == null )
			return false;

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
		var relevantCollection = Cache.GetCollectionByName<T>( collection, false );

		if ( relevantCollection == null )
			return false;

		return relevantCollection.CachedDocuments.ContainsKey( id );
	}

	/// <summary>
	/// Wipe everything, forever. Requires unsafe mode to be enabled.
	/// </summary>
	public static void WipeAllData()
	{
		if (!Config.UNSAFE_MODE)
		{
			Log.Warning( "must enable unsafe mode to call WipeAllData() - see Config.cs" );
			return;
		}

		Cache.WipeCollectionsCache();

		int attempt = 0;

		while ( true )
		{
			if ( attempt++ >= 10 )
				throw new System.Exception( "failed to load collections after 10 tries - are the files in use by something else?" );

			if ( FileIO.WipeFilesystem() == null )
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

	/// <summary>
	/// Call this to force-write all remaining cache. It is recommended to call this
	/// when your server is shutting down to avoid data loss, if that's important to
	/// you. <br/>
	/// <br/>
	/// If anything is being inserted into or deleted from the database when this is
	/// called, then what happens to that data is undefined behaviour; this does not
	/// guarantee any ongoing inserts or deletions will be saved. If that matters to
	/// you, then don't make inserts or deletions while shutting down the server.
	/// </summary>
	public static void ForceWriteCache()
	{
		Cache.ForceFullWrite();
	}
}
