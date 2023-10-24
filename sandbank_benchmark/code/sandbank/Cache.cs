using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Sandbox;

namespace NSSandbank;

static internal class Cache
{
	private static ConcurrentDictionary<string, Collection> _collections = new();
	private static TimeSince _timeSinceLastFullWrite = 0;
	private static int _staleDocumentsFoundAfterLastFullWrite;
	private static int _staleDocumentsWrittenSinceLastFullWrite;
	private static Dictionary<Collection, List<Document>> _staleDocumentsToWrite = new();
	private static int _partialWriteTickInterval = Game.TickRate / Config.PARTIAL_WRITES_PER_SECOND;
	private static bool _writeInProgress = false;
	private static object _writeInProgressLock = new();

	public static Collection GetCollectionByName<T>( string name )
	{
		if ( !_collections.ContainsKey( name ) )
		{
			if ( Config.ENABLE_LOGGING )
				Log.Info( $"Sandbank: creating new collection {name}" );

			var newCollection = new Collection()
			{
				CollectionName = name,
				DocumentClassType = typeof( T ),
				DocumentClassTypeSerialized = typeof( T ).ToString()
			};

			_collections[name] = newCollection;

			int attempt = 0;

			while ( true )
			{
				if ( attempt++ >= 10 )
					throw new Exception( $"Sandbank: failed to save \"{name}\" collection definition after 10 tries - is the file in use by something else?" );

				if ( FileIO.SaveCollectionDefinition( newCollection ) )
					break;

				GameTask.Delay( 50 );
			}
		}

		return _collections[name];
	}

	public static bool CollectionExists( string name )
	{
		return _collections.ContainsKey( name );
	}

	public static void WipeCollectionsCache()
	{
		_collections.Clear();
	}

	public static void CreateCollection( string name, Type documentClassType )
	{
		if ( _collections.ContainsKey( name ) )
			return;

		Collection newCollection = new()
		{
			CollectionName = name,
			DocumentClassType = documentClassType,
			DocumentClassTypeSerialized = documentClassType.ToString()
		};

		_collections[name] = newCollection;
	}

	public static void InsertDocumentsIntoCollection( string collection, List<Document> documents )
	{
		foreach ( var document in documents )
			_collections[collection].CachedDocuments[document.ID] = document;
	}

	[GameEvent.Tick.Server]
	private static void Tick()
	{
		lock ( _writeInProgressLock )
		{
			// Don't try and write anything if we are already doing a write
			// (possibly asynchronously).
			if ( _writeInProgress )
				return;
		}

		if ( _timeSinceLastFullWrite >= Config.PERSIST_EVERY_N_SECONDS )
		{
			GameTask.RunInThreadAsync( async () => FullWriteAsync() );
		}
		else if (Time.Tick % _partialWriteTickInterval == 0)
		{
			GameTask.RunInThreadAsync(async () => PartialWriteAsync() );
		}
	}

	/// <summary>
	/// Figure out how many documents we should write for our next partial write.
	/// </summary>
	private static int GetNumberOfDocumentsToWrite()
	{
		float progressToNextWrite = _timeSinceLastFullWrite / Config.PERSIST_EVERY_N_SECONDS;
		int documentsWeShouldHaveWrittenByNow = (int)(_staleDocumentsFoundAfterLastFullWrite * progressToNextWrite);
		int numberToWrite = documentsWeShouldHaveWrittenByNow - _staleDocumentsWrittenSinceLastFullWrite;

		if ( numberToWrite <= 0 )
			return 0;

		return numberToWrite;
	}

	/// <summary>
	/// Write some (but probably not all) of the stale documents to disk. The longer
	/// it's been since our last partial write, the more documents we will write.
	/// </summary>
	private static void PartialWriteAsync()
	{
		lock ( _writeInProgressLock )
		{
			_writeInProgress = true;
		}

		try
		{
			var numberOfDocumentsToWrite = GetNumberOfDocumentsToWrite();

			if ( numberOfDocumentsToWrite > 0 )
			{
				if ( Config.ENABLE_LOGGING )
					Log.Info( "Sandbank: performing partial write..." );

				PersistStaleDocuments( numberOfDocumentsToWrite );
			}
		}
		catch ( Exception e )
		{
			Log.Error( "Sandbank: partial write failed: " + e.Message );
		}
		finally
		{
			lock ( _writeInProgressLock )
			{
				_writeInProgress = false;
			}
		}
	}

	/// <summary>
	/// Perform a full-write to guarantee we meet our write deadline target. Also,
	/// re-evaluate cache to determine what is now stale.
	/// </summary>
	private static void FullWriteAsync()
	{
		lock ( _writeInProgressLock )
		{
			_writeInProgress = true;
		}

		try
		{
			if ( Config.ENABLE_LOGGING )
				Log.Info( "Sandbank: performing full write..." );

			// Persist any remaining items first.
			PersistStaleDocuments();
			ReevaluateStaleDocuments();

			_timeSinceLastFullWrite = 0;
			_staleDocumentsWrittenSinceLastFullWrite = 0;
		}
		catch (Exception e)
		{
			Log.Error("Sandbank: full write failed: "+e.Message );
		}
		finally
		{
			lock ( _writeInProgressLock )
			{
				_writeInProgress = false;
			}
		}
	}

	/// <summary>
	/// Persist some of the stale documents to disk. We generally don't want to persist
	/// them all at once, as this can cause lag spikes.
	/// </summary>
	private static void PersistStaleDocuments(int number = int.MaxValue)
	{
		if ( Config.ENABLE_LOGGING )
		{
			if (number == int.MaxValue)
				Log.Info( $"Sandbank: persisting {_staleDocumentsFoundAfterLastFullWrite-_staleDocumentsWrittenSinceLastFullWrite} stale documents..." );
			else
				Log.Info( $"Sandbank: persisting {number} stale documents..." );
		}

		if ( number != int.MaxValue )
			_staleDocumentsWrittenSinceLastFullWrite += number;
		else
			_staleDocumentsWrittenSinceLastFullWrite = _staleDocumentsFoundAfterLastFullWrite;

		while ( true)
		{
			if ( _staleDocumentsToWrite.Count <= 0 )
				return;

			var staleCollectionDocuments = _staleDocumentsToWrite.First();

			while ( staleCollectionDocuments.Value.Count > 0)
			{
				var document = staleCollectionDocuments.Value.First();

				document.PersistToDisk( staleCollectionDocuments.Key.CollectionName,
					staleCollectionDocuments.Key.DocumentClassType );

				staleCollectionDocuments.Value.Remove( document );

				number--;

				if ( number <= 0 )
					return;
			}

			_staleDocumentsToWrite.Remove( staleCollectionDocuments.Key );
		}
	}

	/// <summary>
	/// Re-examine the cache and figure out what's stale and so what needs writing to
	/// disk.
	/// </summary>
	private static void ReevaluateStaleDocuments()
	{
		if ( _staleDocumentsToWrite.Count > 0 )
			throw new Exception( "Sandbank: can't re-fetch stale documents when there are still stale documents to be written" );

		_staleDocumentsFoundAfterLastFullWrite = 0;

		foreach (var collectionPair in _collections)
		{
			List<Document> staleDocuments = new();

			foreach (var documentPair in collectionPair.Value.CachedDocuments)
			{
				if ( documentPair.Value.Stale )
					staleDocuments.Add( documentPair.Value );
			}

			_staleDocumentsToWrite.Add( collectionPair.Value, staleDocuments );
			_staleDocumentsFoundAfterLastFullWrite += staleDocuments.Count();
		}

		if ( Config.ENABLE_LOGGING )
			Log.Info( $"Sandbank: found {_staleDocumentsFoundAfterLastFullWrite} stale documents" );
	}
}
