using Sandbox;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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

		List<Func<Task>> benchmarks = new()	{
			//BenchmarkInsert,
			//BenchmarkInsertThreaded,
			//BenchmarkSelect,
			BenchmarkSelectThreaded,
			BenchmarkSelectThreaded,
			BenchmarkSelectThreaded,
			BenchmarkSelectThreaded,
			BenchmarkSelectThreaded,
			BenchmarkSelectThreaded,
			BenchmarkSelectThreaded,
			BenchmarkSelectThreaded,
			BenchmarkSelectThreaded,
			BenchmarkSelectThreaded,
			BenchmarkSelectThreaded,
			BenchmarkSelectThreaded,
			BenchmarkSelectThreaded,
		};

		foreach ( var benchmark in benchmarks )
		{
			await benchmark.Invoke();
			Sandbank.WipeAllData();

			// We want a delay to let the PC cool-off.
			await GameTask.DelaySeconds( 2 );
		}
	}

	private static async Task BenchmarkInsert()
	{
		int documents = 100000;

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
			Sandbank.Insert<PlayerData>( "players", testData[i] );
		}

		double totalTime = DateTime.Now.Subtract( startTime ).TotalSeconds;

		Log.Info( $"Insert() - {documents} documents inserted in {totalTime} seconds" );
	}

	private static async Task BenchmarkInsertThreaded()
	{
		int documents = 100000;
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
	}

	private static async Task BenchmarkSelect()
	{
		int collectionSize = 5000;
		int searches = 1200;

		for ( int i = 0; i < collectionSize; i++ )
		{
			TestData.TestData1.ID = "";
			TestData.TestData1.Health = Game.Random.Next( 101 );
			Sandbank.Insert<PlayerData>( "players", TestData.TestData1 );
		}

		var startTime = DateTime.Now;

		for ( int i = 0; i < searches; i++ )
		{
			var results = Sandbank.Select<PlayerData>( "players", x => x.Health >= 90 );
		}

		double totalTime = DateTime.Now.Subtract( startTime ).TotalSeconds;

		Log.Info( $"Select() - {collectionSize} documents searched {searches} times in {totalTime} seconds" );
	}

	private static async Task BenchmarkSelectThreaded()
	{
		int collectionSize = 5000;
		int searches = 1200;
		int threads = 24;
		int searchesPerThread = searches / threads;

		for ( int i = 0; i < collectionSize; i++ )
		{
			TestData.TestData1.ID = "";
			TestData.TestData1.Health = Game.Random.Next( 101 );
			Sandbank.Insert<PlayerData>( "players", TestData.TestData1 );
		}

		List<Task> tasks = new();
		var startTime = DateTime.Now;

		for (int t = 0; t < threads; t++ )
		{
			tasks.Add( GameTask.RunInThreadAsync( async () =>
			{
				for ( int i = 0; i < searchesPerThread; i++ )
				{
					var results = Sandbank.Select<PlayerData>( "players", x => x.Health >= 90 );
				}
			} ));
		}

		await GameTask.WhenAll( tasks );

		double totalTime = DateTime.Now.Subtract( startTime ).TotalSeconds;

		Log.Info( $"[multi-threaded] Select() - {collectionSize} documents searched {searches} times in {totalTime} seconds" );
	}
}
