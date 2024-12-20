using Sandbox;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SandbankDatabase;

public static class Sandbank
{
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
		var relevantCollection = Cache.GetCollectionByName<T>( collection, true );

		Document newDocument = new( document, typeof(T), true, collection );
		relevantCollection.InsertDocument( newDocument );
	}

	/// <summary>
	/// Insert a document into the database. The document will have its ID set if it is empty.
	/// 
	/// For internal use.
	/// </summary>
	internal static void Insert( string collection, object document, Type documentType )
	{
		var relevantCollection = Cache.GetCollectionByName( collection, true, documentType );

		Document newDocument = new( document, documentType, true, collection );
		relevantCollection.InsertDocument( newDocument );
	}

	/// <summary>
	/// Insert multiple documents into the database. The documents will have their IDs
	/// set if they are empty.
	/// </summary>
	public static void InsertMany<T>( string collection, IEnumerable<T> documents ) where T : class
	{
		var relevantCollection = Cache.GetCollectionByName<T>( collection, true );

		foreach (var document in documents)
		{
			Document newDocument = new Document( document, typeof(T), true, collection );
			relevantCollection.InsertDocument( newDocument );
		}
	}

	/// <summary>
	/// Fetch a single document from the database where selector evaluates to true.
	/// </summary>
	public static T SelectOne<T>( string collection, Func<T, bool> selector ) where T : class, new()
	{
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
	/// The same as SelectOne except faster since we can look it up by ID.
	/// </summary>
	public static T SelectOneWithID<T>( string collection, string uid ) where T : class, new()
	{
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

			var error = "";

			while ( true )
			{
				if ( attempt++ >= 10 )
					throw new SandbankException( $"failed to delete document from collection \"{collection}\" after 10 tries: " + error );

				error = FileController.DeleteDocument( collection, id );

				if ( error == null)
					break;
			}
		}
	}

	/// <summary>
	/// The same as Delete except faster since we can look it up by ID.
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
				throw new SandbankException( $"failed to delete document from collection \"{collection}\" after 10 tries - is the file in use by something else?" );

			if ( FileController.DeleteDocument( collection, id ) == null )
				break;
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
	/// The same as Any except faster since we can look it up by ID.
	/// </summary>
	public static bool AnyWithID<T>( string collection, string id )
	{
		var relevantCollection = Cache.GetCollectionByName<T>( collection, false );

		if ( relevantCollection == null )
			return false;

		return relevantCollection.CachedDocuments.ContainsKey( id );
	}

	public static void DeleteAllBackups()
	{
		try
		{
			FileController.WipeBackups();
		}
		catch ( Exception e )
		{
			Logging.Warn( $"failed deleting all backups: {Logging.ExtractExceptionString( e )}" );
		}
	}

	/// <summary>
	/// Deletes everything, forever. Does not delete backups.
	/// </summary>
	public static void DeleteAllData()
	{
		Cache.WipeStaticFields();

		int attempt = 0;
		string error = null;

		while ( true )
		{
			if ( attempt++ >= 10 )
				throw new SandbankException( $"failed to wipe data after 10 tries: {error}" );

			error = FileController.WipeFilesystem();

			if ( error == null )
				return;
		}
	}

	/// <summary>
	/// Call this to gracefully shut-down the database. It is recommended to call this
	/// when your server is shutting down to make sure all recently-changed data is saved,
	/// if that's important to you.
	/// <br/> <br/>
	/// Any operations ongoing at the time Shutdown is called are not guaranteed to be
	/// written to disk.
	/// <br/> <br/>
	/// Shutdown takes some time to complete, so you should await this until it's done.
	/// </summary>
	public static async Task Shutdown()
	{
		while ( InitialisationController.CurrentDatabaseState != DatabaseState.Uninitialised )
		{
			// This will signal to the ticker to kill the background threads and complete the shutdown.
			InitialisationController.CurrentDatabaseState = DatabaseState.ShuttingDown;
			await Task.Delay( 10 );
		}
	}
}
