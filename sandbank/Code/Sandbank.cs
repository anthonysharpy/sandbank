using Sandbox;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SandbankDatabase;

public static class Sandbank
{
	public static bool IsInitialised => Initialisation.IsInitialised;

	/// <summary>
	/// Initialises the database. You must call this once manually on the host when the game starts.
	/// </summary>
	public static async Task InitialiseAsync()
	{
		if ( !Networking.IsHost && !Config.CLIENTS_CAN_USE )
		{
			Logging.Error( "only the host can initialise the database - set CLIENTS_CAN_USE to true in Config.cs" +
				" if you want clients to be able to use the database too" );
		}

		await GameTask.RunInThreadAsync( () =>
		{
			Initialisation.Initialise();
		} );
	}

	/// <summary>
	/// Initialises the database. You must call this once manually on the host when the game starts.
	/// </summary>
	public static void Initialise()
	{
		if ( !Networking.IsHost && !Config.CLIENTS_CAN_USE )
		{
			Logging.Error( "only the host can initialise the database - set CLIENTS_CAN_USE to true in Config.cs" +
				" if you want clients to be able to use the database too" );
		}

		Initialisation.Initialise();
	}

	/// <summary>
	/// Copy the saveable data from one class to another. This is useful for when you load
	/// data from the database and you want to put it in a component or something like that.
	/// </summary>
	public static void CopySavedData<T>(T sourceClass, T destinationClass)
	{
		Cloning.CopyClassData<T>( sourceClass, destinationClass );
	}

	/// <summary>
	/// Insert a document into the database. The document will have its ID set
	/// if it is empty.
	/// </summary>
	public static void Insert<T>( string collection, T document ) where T : class
	{
		if ( !IsInitialised ) 
		{
			Logging.Warn( "operation failed as the database is not yet initialised - check IsInitialised before making any requests" );
			return;
		}

		var relevantCollection = Cache.GetCollectionByName<T>( collection, true );

		Document newDocument = new( document, typeof(T), true, collection );
		relevantCollection.InsertDocument( newDocument );
		return;
	}

	/// <summary>
	/// Insert multiple documents into the database. The documents will have their IDs
	/// set if they are empty.
	/// </summary>
	public static void InsertMany<T>( string collection, IEnumerable<T> documents ) where T : class
	{
		if ( !IsInitialised )
		{
			Logging.Warn( "operation failed as the database is not yet initialised - check IsInitialised before making any requests" );
			return;
		}

		var relevantCollection = Cache.GetCollectionByName<T>( collection, true );

		foreach (var document in documents)
		{
			Document newDocument = new Document( document, typeof(T), true, collection );
			relevantCollection.InsertDocument( newDocument );
		}

		return;
	}

	/// <summary>
	/// Fetch a single document from the database where selector evaluates to true.
	/// </summary>
	public static T SelectOne<T>( string collection, Func<T, bool> selector ) where T : class, new()
	{
		if ( !IsInitialised )
		{
			Logging.Warn( "operation failed as the database is not yet initialised - check IsInitialised before making any requests" );
			return null;
		}

		var relevantCollection = Cache.GetCollectionByName<T>( collection, false );

		if ( relevantCollection == null )
			return null;

		foreach ( var pair in relevantCollection.CachedDocuments )
		{
			if ( selector.Invoke( (T)pair.Value.Data ) )
				return ObjectPool.CloneObject( (T)pair.Value.Data, relevantCollection.DocumentClassType.FullName );
		}

		return null;
	}

	/// <summary>
	/// The same as SelectOne except slightly faster since we can look it up by ID.
	/// </summary>
	public static T SelectOneWithID<T>( string collection, string uid ) where T : class, new()
	{
		if ( !IsInitialised )
		{
			Logging.Warn( "operation failed as the database is not yet initialised - check IsInitialised before making any requests" );
			return null;
		}

		var relevantCollection = Cache.GetCollectionByName<T>( collection, false );

		if ( relevantCollection == null )
			return null;

		relevantCollection.CachedDocuments.TryGetValue(uid, out Document document);

		return document == null ?
			null
			: ObjectPool.CloneObject( (T)document.Data, relevantCollection.DocumentClassType.FullName );
	}

	/// <summary>
	/// Select all documents from the database where selector evaluates to true.
	/// </summary>
	public static List<T> Select<T>( string collection, Func<T, bool> selector ) where T : class, new()
	{
		if ( !IsInitialised )
		{
			Logging.Warn( "operation failed as the database is not yet initialised - check IsInitialised before making any requests" );
			return new List<T>();
		}

		var relevantCollection = Cache.GetCollectionByName<T>( collection, false );
		List<T> output = new();

		if ( relevantCollection == null )
			return output;

		foreach ( var pair in relevantCollection.CachedDocuments )
		{
			if ( selector.Invoke( (T)pair.Value.Data ) )
				output.Add( ObjectPool.CloneObject( (T)pair.Value.Data, relevantCollection.DocumentClassType.FullName ) );
		}

		return output;
	}

	/// <summary>
	/// DO NOT USE THIS FUNCTION UNLESS YOU FULLY UNDERSTAND THE BELOW, AS THERE IS
	/// A RISK YOU COULD CORRUPT YOUR DATA. <br/>
	/// <br/>
	/// This does the exact same thing as Select, except it is about 9x faster.
	/// They work differently, however. <br/>
	/// <br/>
	/// Select copies the data from the cache into new objects and then gives those
	/// new objects to you. That means that any changes you make to those new objects
	/// don't affect anything else - you're free to do what you want with them. The
	/// downside to this is that there is an overhead invovled in creating all those
	/// new objects. <br/>
	/// <br/>
	/// SelectUnsafeReferences on the other hand will give you a reference to the data
	/// that is stored in the cache. This is faster because it means no new copy has to
	/// be made. However, because it's giving you a reference, this means that ANY CHANGES
	/// YOU MAKE TO THE RETURNED OBJECTS WILL BE REFLECTED IN THE CACHE, AND THEREFORE MAY
	/// CHANGE THE VALUES IN THE DATABASE UNEXEPECTEDLY!!! You should therefore not modify
	/// the returned objects in any way, only read them.<br/>
	/// <br/>
	/// You are guaranteed that the cache will not change the object after you have requested
	/// it (because all inserts are new objects).
	/// </summary>
	public static List<T> SelectUnsafeReferences<T>( string collection, Func<T, bool> selector ) where T : class
	{
		if ( !IsInitialised )
		{
			Logging.Warn( "operation failed as the database is not yet initialised - check IsInitialised before making any requests" );
			return new List<T>();
		}

		var relevantCollection = Cache.GetCollectionByName<T>( collection, false );
		List<T> output = new();

		if ( relevantCollection == null )
			return output;

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
		if ( !IsInitialised )
		{
			Logging.Warn( "operation failed as the database is not yet initialised - check IsInitialised before making any requests" );
			return;
		}

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

				if ( FileController.DeleteDocument( collection, id ) == null )
					break;
			}
		}

		return;
	}

	/// <summary>
	/// The same as Delete except slightly faster since we can look it up by ID.
	/// </summary>
	public static void DeleteWithID<T>( string collection, string id) where T : class
	{
		if ( !IsInitialised )
		{
			Logging.Warn( "operation failed as the database is not yet initialised - check IsInitialised before making any requests" );
			return;
		}

		var relevantCollection = Cache.GetCollectionByName<T>( collection, false );

		if ( relevantCollection == null )
			return;

		relevantCollection.CachedDocuments.TryRemove( id, out _ );

		int attempt = 0;

		while ( true )
		{
			if ( attempt++ >= 10 )
				throw new Exception( $"failed to delete document from collection \"{collection}\" after 10 tries - is the file in use by something else?" );

			if ( FileController.DeleteDocument( collection, id ) == null )
				break;
		}

		return;
	}

	/// <summary>
	/// Return whether there are any documents in the datbase where selector evalutes
	/// to true.
	/// </summary>
	public static bool Any<T>( string collection, Func<T, bool> selector ) where T : class
	{
		if ( !IsInitialised )
		{
			Logging.Warn( "operation failed as the database is not yet initialised - check IsInitialised before making any requests" );
			return false;
		}

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
		if ( !IsInitialised )
		{
			Logging.Warn( "operation failed as the database is not yet initialised - check IsInitialised before making any requests" );
			return false;
		}

		var relevantCollection = Cache.GetCollectionByName<T>( collection, false );

		if ( relevantCollection == null )
			return false;

		return relevantCollection.CachedDocuments.ContainsKey( id );
	}

	/// <summary>
	/// Deletes everything, forever.
	/// </summary>
	public static void DeleteAllData()
	{
		if ( !IsInitialised )
		{
			Logging.Warn( "operation failed as the database is not yet initialised - check IsInitialised before making any requests" );
			return;
		}

		Cache.WipeCollectionsCache();

		int attempt = 0;

		while ( true )
		{
			if ( attempt++ >= 10 )
				throw new Exception( "failed to load collections after 10 tries - are the files in use by something else?" );

			if ( FileController.WipeFilesystem() == null )
				return;
		}
	}

	/// <summary>
	/// Enables warnings as exceptions. See comments in Config.cs
	/// for whether this is a good idea for you or not.
	/// </summary>
	public static void EnableWarningsAsExceptions()
	{
		Config.WARNINGS_AS_EXCEPTIONS = true;
	}

	/// <summary>
	/// Call this to force-write all remaining cache. It is recommended to call this
	/// when your server is shutting down to avoid data loss, if that's important to
	/// you. <br/>
	/// <br/>
	/// Any inserts or deletions ongoing at the time ForceWriteCache is called are not
	/// guaranteed to be written to disk by ForceWriteCache. If that matters to you,
	/// then don't make inserts or deletions while calling this.
	/// </summary>
	public static void ForceWriteCache()
	{
		if ( !IsInitialised )
		{
			Logging.Warn( "operation failed as the database is not yet initialised - check IsInitialised before making any requests" );
			return;
		}

		Cache.ForceFullWrite();
		return;
	}
}
