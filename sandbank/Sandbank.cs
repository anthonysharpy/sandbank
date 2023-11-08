using NSSandbank;
using Sandbox;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

static class Sandbank
{
	public static bool IsInitialised => Initialisation.IsInitialised;

	/// <summary>
	/// Helper task that completes when the database is initialised. Useful for
	/// blocking initialisation-related setup tasks.
	/// </summary>
	public static async Task WaitForInitialisationAsync()
	{
		while ( !IsInitialised )
			await GameTask.Delay( 50 );

		return;
	}

	/// <summary>
	/// Insert a document into the database. The document will have its ID set
	/// if it is empty.
	/// </summary>
	public static Task Insert<T>( string collection, T document ) where T : class
	{
		if ( !IsInitialised )
		{
			Logging.Warn( "operation failed as the database is not yet initialised - check IsInitialised before making any requests" );
			return Task.CompletedTask;
		}

		var relevantCollection = Cache.GetCollectionByName<T>( collection, true );

		Document newDocument = new( document, typeof(T), true );
		relevantCollection.CachedDocuments[newDocument.ID] = newDocument;
		return Task.CompletedTask;
	}

	/// <summary>
	/// Insert multiple documents into the database. The documents will have their IDs
	/// set if they are empty.
	/// </summary>
	public static Task InsertMany<T>( string collection, IEnumerable<T> documents ) where T : class
	{
		if ( !IsInitialised )
		{
			Logging.Warn( "operation failed as the database is not yet initialised - check IsInitialised before making any requests" );
			return Task.CompletedTask;
		}

		var relevantCollection = Cache.GetCollectionByName<T>( collection, true );

		foreach (var document in documents)
		{
			Document newDocument = new Document( document, typeof(T), true );
			relevantCollection.CachedDocuments[newDocument.ID] = newDocument;
		}

		return Task.CompletedTask;
	}

	/// <summary>
	/// Fetch a single document from the database where selector evaluates to true.
	/// </summary>
	public static Task<T> SelectOne<T>( string collection, Func<T, bool> selector ) where T : class
	{
		if ( !IsInitialised )
		{
			Logging.Warn( "operation failed as the database is not yet initialised - check IsInitialised before making any requests" );
			return Task.FromResult<T>( null );
		}

		var relevantCollection = Cache.GetCollectionByName<T>( collection, false );

		if ( relevantCollection == null )
			return Task.FromResult<T>( null );

		foreach ( var pair in relevantCollection.CachedDocuments )
		{
			if ( selector.Invoke( (T)pair.Value.Data ) )
				return Task.FromResult( Serialisation.CloneObject( (T)pair.Value.Data ) );
		}

		return Task.FromResult<T>( null );
	}

	/// <summary>
	/// The same as SelectOne except slightly faster since we can look it up by ID.
	/// </summary>
	public static Task<T> SelectOneWithID<T>( string collection, string id ) where T : class
	{
		if ( !IsInitialised )
		{
			Logging.Warn( "operation failed as the database is not yet initialised - check IsInitialised before making any requests" );
			return Task.FromResult<T>( null );
		}

		var relevantCollection = Cache.GetCollectionByName<T>( collection, false );

		if ( relevantCollection == null )
			return Task.FromResult<T>( null );

		relevantCollection.CachedDocuments.TryGetValue(id, out Document document);

		return document == null ? 
			Task.FromResult<T>( null ) 
			: Task.FromResult( Serialisation.CloneObject( (T)document.Data ) );
	}

	/// <summary>
	/// Select all documents from the database where selector evaluates to true.
	/// </summary>
	public static Task<List<T>> Select<T>( string collection, Func<T, bool> selector ) where T : class
	{
		if ( !IsInitialised )
		{
			Logging.Warn( "operation failed as the database is not yet initialised - check IsInitialised before making any requests" );
			return Task.FromResult( new List<T>() );
		}

		var relevantCollection = Cache.GetCollectionByName<T>( collection, false );
		List<T> output = new();

		if ( relevantCollection == null )
			return Task.FromResult( output );

		foreach ( var pair in relevantCollection.CachedDocuments )
		{
			if ( selector.Invoke( (T)pair.Value.Data ) )
				output.Add( Serialisation.CloneObject( (T)pair.Value.Data ) );
		}

		return Task.FromResult( output );
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
	/// the returned objects in any way, only read them. You are guaranteed however that the
	/// cache will not change the object after you have requested it (because all inserts
	/// are new objects).
	/// </summary>
	public static Task<List<T>> SelectUnsafeReferences<T>( string collection, Func<T, bool> selector ) where T : class
	{
		if ( !IsInitialised )
		{
			Logging.Warn( "operation failed as the database is not yet initialised - check IsInitialised before making any requests" );
			return Task.FromResult( new List<T>() );
		}

		var relevantCollection = Cache.GetCollectionByName<T>( collection, false );
		List<T> output = new();

		if ( relevantCollection == null )
			return Task.FromResult( output );

		foreach ( var pair in relevantCollection.CachedDocuments )
		{
			if ( selector.Invoke( (T)pair.Value.Data ) )
				output.Add( (T)pair.Value.Data );
		}

		return Task.FromResult( output );
	}

	/// <summary>
	/// Delete all documents from the database where selector evaluates to true.
	/// </summary>
	public static Task Delete<T>( string collection, Predicate<T> selector ) where T : class
	{
		if ( !IsInitialised )
		{
			Logging.Warn( "operation failed as the database is not yet initialised - check IsInitialised before making any requests" );
			return Task.CompletedTask;
		}

		var relevantCollection = Cache.GetCollectionByName<T>( collection, false );

		if ( relevantCollection == null )
			return Task.CompletedTask;

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

		return Task.CompletedTask;
	}

	/// <summary>
	/// The same as Delete except slightly faster since we can look it up by ID.
	/// </summary>
	public static Task DeleteWithID<T>( string collection, string id) where T : class
	{
		if ( !IsInitialised )
		{
			Logging.Warn( "operation failed as the database is not yet initialised - check IsInitialised before making any requests" );
			return Task.CompletedTask;
		}

		var relevantCollection = Cache.GetCollectionByName<T>( collection, false );

		if ( relevantCollection == null )
			return Task.CompletedTask;

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

		return Task.CompletedTask;
	}

	/// <summary>
	/// Return whether there are any documents in the datbase where selector evalutes
	/// to true.
	/// </summary>
	public static Task<bool> Any<T>( string collection, Func<T, bool> selector ) where T : class
	{
		if ( !IsInitialised )
		{
			Logging.Warn( "operation failed as the database is not yet initialised - check IsInitialised before making any requests" );
			return Task.FromResult( false );
		}

		var relevantCollection = Cache.GetCollectionByName<T>( collection, false );
				
		if ( relevantCollection == null )
			return Task.FromResult( false );

		foreach ( var pair in relevantCollection.CachedDocuments )
		{
			if ( selector.Invoke( (T)pair.Value.Data ) )
				return Task.FromResult( true );
		}

		return Task.FromResult( false );
	}

	/// <summary>
	/// The same as Any except slightly faster since we can look it up by ID.
	/// </summary>
	public static Task<bool> AnyWithID<T>( string collection, string id )
	{
		if ( !IsInitialised )
		{
			Logging.Warn( "operation failed as the database is not yet initialised - check IsInitialised before making any requests" );
			return Task.FromResult( false );
		}

		var relevantCollection = Cache.GetCollectionByName<T>( collection, false );

		if ( relevantCollection == null )
			return Task.FromResult( false );

		return Task.FromResult( relevantCollection.CachedDocuments.ContainsKey( id ) );
	}

	/// <summary>
	/// Wipe everything, forever. Requires unsafe mode to be enabled.
	/// </summary>
	public static Task WipeAllData()
	{
		if ( !IsInitialised )
		{
			Logging.Warn( "operation failed as the database is not yet initialised - check IsInitialised before making any requests" );
			return Task.CompletedTask;
		}

		if ( !Config.UNSAFE_MODE)
		{
			Log.Warning( "must enable unsafe mode to call WipeAllData() - see Config.cs" );
			return Task.CompletedTask;
		}

		Cache.WipeCollectionsCache();

		int attempt = 0;

		while ( true )
		{
			if ( attempt++ >= 10 )
				throw new Exception( "failed to load collections after 10 tries - are the files in use by something else?" );

			if ( FileIO.WipeFilesystem() == null )
				return Task.CompletedTask;

			GameTask.Delay( 50 );
		}

		return Task.CompletedTask;
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
	/// Any inserts or deletions ongoing at the time ForceWriteCache is called are not
	/// guaranteed to be written to disk by ForceWriteCache. If that matters to you,
	/// then don't make inserts or deletions while calling this.
	/// </summary>
	public static Task ForceWriteCache()
	{
		if ( !IsInitialised )
		{
			Logging.Warn( "operation failed as the database is not yet initialised - check IsInitialised before making any requests" );
			return Task.CompletedTask;
		}

		Cache.ForceFullWrite();
		return Task.CompletedTask;
	}
}
