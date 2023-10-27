using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Sandbox;

namespace NSSandbank;

static internal class Cache
{
	/// <summary>
	/// Indicates that a full or partial write to disk is in progress.
	/// </summary>
	public static object WriteInProgressLock = new();

	private static ConcurrentDictionary<string, Collection> _collections = new();
	private static TimeSince _timeSinceLastFullWrite = 0;
	private static object _timeSinceLastFullWriteLock = new();
	private static int _staleDocumentsFoundAfterLastFullWrite;
	private static int _staleDocumentsWrittenSinceLastFullWrite;
	private static Dictionary<Collection, List<Document>> _staleDocumentsToWrite = new();
	private static int _partialWriteTickInterval = Game.TickRate / Config.PARTIAL_WRITES_PER_SECOND;

	public static Collection GetCollectionByName<T>( string name, bool createIfDoesntExist )
	{
		if ( !_collections.ContainsKey( name ) )
		{
			if ( createIfDoesntExist )
			{
				Logging.Log( $"creating new collection \"{name}\"" );
				CreateCollection( name, typeof( T ) );
			}
			else
			{
				return null;
			}
		}

		return _collections[name];
	}

	public static void WipeCollectionsCache()
	{
		lock ( WriteInProgressLock )
		{
			_collections.Clear();
		}
	}

	private static float GetTimeSinceLastFullWrite()
	{
		lock ( _timeSinceLastFullWriteLock )
		{
			return _timeSinceLastFullWrite;
		}
	}

	private static void ResetTimeSinceLastFullWrite()
	{
		lock ( _timeSinceLastFullWriteLock )
		{
			_timeSinceLastFullWrite = 0;
		}
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

		FileIO.CreateCollectionLock( name );
		_collections[name] = newCollection;

		int attempt = 0;

		while ( true )
		{
			if ( attempt++ >= 10 )
				throw new Exception( $"failed to save \"{name}\" collection definition after 10 tries - is the file in use by something else?" );

			if ( FileIO.SaveCollectionDefinition( newCollection ) == null )
				break;

			GameTask.Delay( 50 );
		}
	}

	public static void InsertDocumentsIntoCollection( string collection, List<Document> documents )
	{
		foreach ( var document in documents )
			_collections[collection].CachedDocuments[document.ID] = document;
	}

	[GameEvent.Tick.Server]
	private static void Tick()
	{
		if ( GetTimeSinceLastFullWrite() >= Config.PERSIST_EVERY_N_SECONDS )
		{
			// Do this immediately otherwise when the server is stuttering it can spam
			// full writes.
			ResetTimeSinceLastFullWrite();

			GameTask.RunInThreadAsync( async () =>
			{
				lock ( WriteInProgressLock )
				{
					FullWrite();
				}
			});
		}
		else if ( Time.Tick % _partialWriteTickInterval == 0 )
		{
			GameTask.RunInThreadAsync( async () => 
			{ 
				lock ( WriteInProgressLock )
				{
					PartialWrite();
				}
			});
		}
	}

	/// <summary>
	/// Force the cache to perform a full-write of all stale entries.
	/// </summary>
	public static void ForceFullWrite()
	{
		lock ( WriteInProgressLock )
		{
			Logging.Log( "beginning forced full-write..." );

			ReevaluateStaleDocuments();
			FullWrite();

			Logging.Log( "finished forced full-write..." );
		}
	}

	/// <summary>
	/// Figure out how many documents we should write for our next partial write.
	/// </summary>
	private static int GetNumberOfDocumentsToWrite()
	{
		float progressToNextWrite = GetTimeSinceLastFullWrite() / Config.PERSIST_EVERY_N_SECONDS;
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
	private static void PartialWrite()
	{
		try
		{
			var numberOfDocumentsToWrite = GetNumberOfDocumentsToWrite();

			if ( numberOfDocumentsToWrite > 0 )
			{
				Logging.Log( "performing partial write..." );

				PersistStaleDocuments( numberOfDocumentsToWrite );
			}
		}
		catch ( Exception e )
		{
			Logging.Throw( "partial write failed: " + e.Message );
		}
	}

	/// <summary>
	/// Perform a full-write to (maybe) guarantee we meet our write deadline target.
	/// Also, re-evaluate cache to determine what is now stale.
	/// </summary>
	private static void FullWrite()
	{
		try
		{
			Logging.Log( "performing full write..." );

			// Persist any remaining items first.
			PersistStaleDocuments();
			ReevaluateStaleDocuments();

			_staleDocumentsWrittenSinceLastFullWrite = 0;
		}
		catch (Exception e)
		{
			Logging.Throw( "full write failed: "+e.Message );
		}
	}

	/// <summary>
	/// Persist some of the stale documents to disk. We generally don't want to persist
	/// them all at once, as this can cause lag spikes.
	/// </summary>
	private static void PersistStaleDocuments(int number = int.MaxValue)
	{
		if (number == int.MaxValue)
			Logging.Log( $"persisting {_staleDocumentsFoundAfterLastFullWrite-_staleDocumentsWrittenSinceLastFullWrite} stale documents..." );
		else
			Logging.Log( $"persisting {number} stale documents..." );

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

		Logging.Log( $"found {_staleDocumentsFoundAfterLastFullWrite} stale documents" );
	}
}
