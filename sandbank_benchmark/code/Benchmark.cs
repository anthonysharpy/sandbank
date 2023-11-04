using Sandbox;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SandbankBenchmark;

static class Benchmark
{
	public static async Task Run()
	{
		await GameTask.DelaySeconds( 2 );

		Sandbank.EnableUnsafeMode();
		Sandbank.WipeAllData();
		Sandbank.DisableIndentJSON();

		Dictionary<Func<Task<double>>, Tuple<string, double>> benchmarks = new()	{
			{ BenchmarkInsert, new("BenchmarkInsert", 0 ) },
			{ BenchmarkInsertThreaded, new("BenchmarkInsertThreaded", 0 ) },
			{ BenchmarkSelect, new("BenchmarkSelect", 0 ) },
			{ BenchmarkSelectThreaded, new("BenchmarkSelectThreaded", 0 ) },
			{ BenchmarkSelectThreadedFewerRecords, new("BenchmarkSelectThreadedFewerRecords", 0 ) },
			{ BenchmarkSelectUnsafeReferences, new("BenchmarkSelectUnsafeReferences", 0 ) },
			{ BenchmarkSelectUnsafeReferencesThreaded, new("BenchmarkSelectUnsafeReferencesThreaded", 0 ) },
			{ BenchmarkSelectOneWithID, new("BenchmarkSelectOneWithID", 0 ) },
			{ BenchmarkSelectOneWithIDThreaded, new("BenchmarkSelectOneWithIDThreaded", 0 ) },
		};

		int repeats = 6;

		for (int i = 0; i < repeats; i++)
		{
			foreach ( var benchmark in benchmarks )
			{
				var secondsTaken = await benchmark.Key.Invoke();

				benchmarks[benchmark.Key] = new Tuple<string, double>(
					benchmarks[benchmark.Key].Item1,
					benchmarks[benchmark.Key].Item2 + secondsTaken
				);

				Sandbank.WipeAllData();

				// We want a delay to let the PC cool-off.
				await GameTask.DelaySeconds( 2 );
			}
		}

		Log.Info( "======== Benchmark Results ========" );
		foreach ( var benchmark in benchmarks )
		{
			Log.Info( $"{benchmark.Value.Item1}: {benchmark.Value.Item2/repeats}" );
		}
	}

	private static async Task<double> BenchmarkInsert()
	{
		int documents = 100800;

		List<PlayerData> testData = new();

		for ( int i = 0; i < documents; i++ )
		{
			testData.Add( new PlayerData()
			{
				Health = 100,
				Name = "TestPlayer1",
				Level = 10,
				LastPlayTime = DateTime.Now,
				Items = new() { "gun", "frog", "banana" }
			} );
		}

		var startTime = DateTime.Now;

		for ( int i = 0; i < testData.Count; i++ )
		{
			Sandbank.Insert( "players", testData[i] );
		}

		double totalTime = DateTime.Now.Subtract( startTime ).TotalSeconds;

		Log.Info( $"Insert() - {documents} documents inserted in {totalTime} seconds" );
		return totalTime;
	}

	private static async Task<double> BenchmarkInsertThreaded()
	{
		int documents = 100800;
		int threads = 24;
		int documentsPerThread = documents / threads;

		List<PlayerData> testData = new();

		for ( int i = 0; i < documentsPerThread; i++ )
		{
			testData.Add(new PlayerData()
			{
				Health = 100,
				Name = "TestPlayer1",
				Level = 10,
				LastPlayTime = DateTime.Now,
				Items = new() { "gun", "frog", "banana" }
			});
		}

		List<Task> tasks = new();
		var startTime = DateTime.Now;

		for (int t = 0; t < threads; t++ )
		{
			tasks.Add(GameTask.RunInThreadAsync( async () =>
			{
				for ( int i = 0; i < documentsPerThread; i++ )
				{
					Sandbank.Insert<PlayerData>( "players", testData[i] );
				}
			}));
		}

		await GameTask.WhenAll( tasks );

		double totalTime = DateTime.Now.Subtract( startTime ).TotalSeconds;

		Log.Info( $"[multi-threaded] Insert() - {documents} documents inserted in {totalTime} seconds" );
		return totalTime;
	}

	private static async Task<double> BenchmarkSelect()
	{
		int collectionSize = 100800;

		for ( int i = 0; i < collectionSize; i++ )
		{
			Sandbank.Insert( "players", new PlayerData()
			{
				Health = Game.Random.Next( 101 ),
				Name = "TestPlayer1",
				Level = 10,
				LastPlayTime = DateTime.Now,
				Items = new() { "gun", "frog", "banana" }
			} );
		}

		var startTime = DateTime.Now;

		Sandbank.Select<PlayerData>( "players", x => x.Health >= 90 );

		double totalTime = DateTime.Now.Subtract( startTime ).TotalSeconds;

		Log.Info( $"Select() - {collectionSize} documents searched in {totalTime} seconds" );
		return totalTime;
	}

	private static async Task<double> BenchmarkSelectThreaded()
	{
		int collectionSize = 100800;
		int threads = 24;

		for ( int i = 0; i < collectionSize; i++ )
		{
			Sandbank.Insert<PlayerData>( "players", new PlayerData()
			{
				Health = Game.Random.Next( 101 ),
				Name = "TestPlayer1",
				Level = 10,
				LastPlayTime = DateTime.Now,
				Items = new() { "gun", "frog", "banana" }
			} );
		}

		List<Task> tasks = new();
		var startTime = DateTime.Now;

		for ( int t = 0; t < threads; t++ )
		{
			tasks.Add( GameTask.RunInThreadAsync( async () =>
			{
				Sandbank.Select<PlayerData>( "players", x => x.Health >= 90 );
			} ));
		}

		await GameTask.WhenAll( tasks );

		double totalTime = DateTime.Now.Subtract( startTime ).TotalSeconds;

		Log.Info( $"[multi-threaded] Select() - {collectionSize} documents searched {threads} times in {totalTime} seconds (~10,008 records returned)" );
		return totalTime;
	}

	// Same as BenchmarkSelectThreaded except with fewer returned records, making
	// it a bit more realistic.
	private static async Task<double> BenchmarkSelectThreadedFewerRecords()
	{
		int collectionSize = 100800;
		int threads = 24;

		for ( int i = 0; i < collectionSize; i++ )
		{
			Sandbank.Insert<PlayerData>( "players", new PlayerData()
			{
				Health = Game.Random.Next( 101 ),
				Name = "TestPlayer1",
				Level = 10,
				LastPlayTime = DateTime.Now,
				Items = new() { "gun", "frog", "banana" }
			} );
		}

		List<Task> tasks = new();
		var startTime = DateTime.Now;

		for ( int t = 0; t < threads; t++ )
		{
			tasks.Add( GameTask.RunInThreadAsync( async () =>
			{
				Sandbank.Select<PlayerData>( "players", x => x.Health == 100 );
			} ) );
		}

		await GameTask.WhenAll( tasks );

		double totalTime = DateTime.Now.Subtract( startTime ).TotalSeconds;

		Log.Info( $"[multi-threaded] Select() - {collectionSize} documents searched {threads} times in {totalTime} seconds (~1,008 records returned)" );
		return totalTime;
	}

	private static async Task<double> BenchmarkSelectUnsafeReferences()
	{
		int collectionSize = 100800;

		for ( int i = 0; i < collectionSize; i++ )
		{
			Sandbank.Insert( "players", new PlayerData()
			{
				Health = Game.Random.Next( 101 ),
				Name = "TestPlayer1",
				Level = 10,
				LastPlayTime = DateTime.Now,
				Items = new() { "gun", "frog", "banana" }
			} );
		}

		var startTime = DateTime.Now;

		Sandbank.SelectUnsafeReferences<PlayerData>( "players", x => x.Health >= 90 );

		double totalTime = DateTime.Now.Subtract( startTime ).TotalSeconds;

		Log.Info( $"SelectUnsafeReferences() - {collectionSize} documents searched in {totalTime} seconds (~10,008 records returned)" );
		return totalTime;
	}

	private static async Task<double> BenchmarkSelectUnsafeReferencesThreaded()
	{
		int collectionSize = 100800;
		int threads = 24;

		for ( int i = 0; i < collectionSize; i++ )
		{
			Sandbank.Insert<PlayerData>( "players", new PlayerData()
			{
				Health = Game.Random.Next( 101 ),
				Name = "TestPlayer1",
				Level = 10,
				LastPlayTime = DateTime.Now,
				Items = new() { "gun", "frog", "banana" }
			} );
		}

		List<Task> tasks = new();
		var startTime = DateTime.Now;

		for ( int t = 0; t < threads; t++ )
		{
			tasks.Add( GameTask.RunInThreadAsync( async () =>
			{
				Sandbank.SelectUnsafeReferences<PlayerData>( "players", x => x.Health >= 90 );
			} ) );
		}

		await GameTask.WhenAll( tasks );

		double totalTime = DateTime.Now.Subtract( startTime ).TotalSeconds;

		Log.Info( $"[multi-threaded] SelectUnsafeReferences() - {collectionSize} documents searched {threads} times in {totalTime} seconds (~10,008 records returned)" );
		return totalTime;
	}

	private static async Task<double> BenchmarkSelectOneWithID()
	{
		int collectionSize = 100800;
		string id = "";
		int repeats = 100000;

		for ( int i = 0; i < collectionSize; i++ )
		{
			var data = new PlayerData()
			{
				Health = Game.Random.Next( 101 ),
				Name = "TestPlayer1",
				Level = 10,
				LastPlayTime = DateTime.Now,
				Items = new() { "gun", "frog", "banana" }
			};

			Sandbank.Insert( "players", data );

			if (i == collectionSize / 2)
				id = data.ID;
		}

		var watch = new Stopwatch();

		watch.Start();

		for ( int i = 0; i < repeats; i++ )
		{
			Sandbank.SelectOneWithID<PlayerData>( "players", id );
		}

		watch.Stop();

		Log.Info( $"SelectOneWithID() - {collectionSize} documents searched {repeats} times in {watch.Elapsed.TotalSeconds} seconds" );
		return watch.Elapsed.TotalSeconds;
	}

	private static async Task<double> BenchmarkSelectOneWithIDThreaded()
	{
		int collectionSize = 100800;
		int repeats = 100000;
		int threads = 24;
		string id = "";

		for ( int i = 0; i < collectionSize; i++ )
		{
			var data = new PlayerData()
			{
				Health = Game.Random.Next( 101 ),
				Name = "TestPlayer1",
				Level = 10,
				LastPlayTime = DateTime.Now,
				Items = new() { "gun", "frog", "banana" }
			};

			Sandbank.Insert( "players", data );

			if ( i == collectionSize / 2 )
				id = data.ID;
		}

		List<Task> tasks = new();

		var watch = new Stopwatch();
		watch.Start();

		for ( int t = 0; t < threads; t++ )
		{
			tasks.Add( GameTask.RunInThreadAsync( async () =>
			{
				for (int i = 0; i < repeats; i++ )
				{
					Sandbank.SelectOneWithID<PlayerData>( "players", id );
				}
			} ) );
		}

		await GameTask.WhenAll( tasks );

		watch.Stop();

		Log.Info( $"[multi-threaded] SelectOneWithID() - {collectionSize} documents searched {repeats} times in {watch.Elapsed.TotalSeconds} seconds" );
		return watch.Elapsed.TotalSeconds;
	}
}
