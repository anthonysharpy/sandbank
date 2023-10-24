using Sandbox;
using System;
using System.Collections.Generic;
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

		List<Action> benchmarks = new()	{
			BenchmarkInsert,
			BenchmarkInsertThreaded,
			BenchmarkSelect,
			BenchmarkSelectThreaded,
		};

		foreach ( var benchmark in benchmarks )
		{
			benchmark.Invoke();
			Sandbank.WipeAllData();

			// We want a delay to let the PC cool-off.
			await GameTask.DelaySeconds( 2 );
		}
	}

	private static void BenchmarkInsert()
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

	private static async void BenchmarkInsertThreaded()
	{
		int documents = 100000;
		int threads = 100;
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

	private static void BenchmarkSelect()
	{
		int repeats = 5000;

		for ( int i = 0; i < repeats; i++ )
		{
			TestData.TestData1.ID = "";
			TestData.TestData1.Health = Game.Random.Next( 101 );
			Sandbank.Insert<PlayerData>( "players", TestData.TestData1 );
		}

		var startTime = DateTime.Now;

		for ( int i = 0; i < repeats; i++ )
		{
			var results = Sandbank.Select<PlayerData>( "players", x => x.Health >= 90 );
		}

		double totalTime = DateTime.Now.Subtract( startTime ).TotalSeconds;

		Log.Info( $"Select() - {repeats} documents searched {repeats} times in {totalTime} seconds" );
	}

	private static async void BenchmarkSelectThreaded()
	{
		int repeats = 5000;
		int threads = 100;
		int searchesPerThread = repeats / threads;

		for ( int i = 0; i < repeats; i++ )
		{
			TestData.TestData1.ID = "";
			TestData.TestData1.Health = Game.Random.Next( 101 );
			Sandbank.Insert<PlayerData>( "players", TestData.TestData1 );
		}

		List<Task> tasks = new();
		var startTime = DateTime.Now;

		for (int t = 0; t < threads; t++ )
		{
			tasks.Add(GameTask.RunInThreadAsync( async () =>
			{
				for ( int i = 0; i < searchesPerThread; i++ )
				{
					var results = Sandbank.Select<PlayerData>( "players", x => x.Health >= 90 );
				}
			} ));
		}

		await GameTask.WhenAll( tasks );

		double totalTime = DateTime.Now.Subtract( startTime ).TotalSeconds;

		Log.Info( $"[multi-threaded] Select() - {repeats} documents searched {repeats} times in {totalTime} seconds" );
	}
}
