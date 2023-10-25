using Sandbox;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SandbankBenchmark;

/// <summary>
/// Run some unit tests. We probably could create an actual tests project for this
/// but it just feels more realistic doing it within sandbox.
/// </summary>
static class Tests
{
	public static void Run()
	{
		TestEnableUnsafeMode();

		List<Action> tests = new() {
			TestDisableIndentJSON,
			TestInsertAndSelectOne,
			TestInsertManyAndSelect,
			TestSelectOneWithID,
			TestDelete,
			TestDeleteWithID,
			TestAny,
			TestAnyWithID,
			TestReferenceDoesntModifyCache,
			TestEnableIndentJSON,
			TestConcurrencySafety,
			TestForceWriteCache,
			TestDisableUnsafeMode,
		};

		foreach (var test in tests)
		{
			Sandbank.WipeAllData();
			test.Invoke();
		}
	}

	private static void Fail(string message)
	{
		Log.Error( "fail: " + message );
	}

	private static void TestInsertAndSelectOne()
	{
		Sandbank.Insert<PlayerData>( "players", TestData.TestData1 );

		if ( TestData.TestData1.ID.Length <= 10 )
			Fail( "TestInsertAndSelectOne() 1:" + TestData.TestData1.ID );

		var data = Sandbank.SelectOne<PlayerData>("players", x => x.ID == TestData.TestData1.ID );

		if ( data.Name != TestData.TestData1.Name )
			Fail( "TestInsertAndSelectOne() 2:" + data.Name );
	}

	private static void TestInsertManyAndSelect()
	{
		var players = new List<PlayerData> { TestData.TestData1, TestData.TestData2 };
		Sandbank.InsertMany<PlayerData>( "players", players);

		if ( players[0].ID.Length <= 10 )
			Fail( "TestInsertManyAndSelect() 1:" + players[0].ID );
		if ( players[1].ID.Length <= 10 )
			Fail( "TestInsertManyAndSelect() 2:" + players[1].ID );

		var data = Sandbank.Select<PlayerData>( "players", x => true );

		if ( data[0].Name != TestData.TestData1.Name && data[0].Name != TestData.TestData2.Name )
			Fail( "TestInsertManyAndSelect() 3:" + data[0].Name );
		if ( data[1].Name != TestData.TestData1.Name && data[1].Name != TestData.TestData2.Name )
			Fail( "TestInsertManyAndSelect() 4:" + data[1].Name );
	}

	private static void TestSelectOneWithID()
	{
		Sandbank.Insert<PlayerData>( "players", TestData.TestData1 );

		if ( TestData.TestData1.ID.Length <= 10 )
			Fail( "TestSelectOneWithID() 1:" + TestData.TestData1.ID );

		var data = Sandbank.SelectOneWithID<PlayerData>( "players", TestData.TestData1.ID );

		if ( data?.Name != TestData.TestData1.Name )
			Fail( "TestSelectOneWithID() 2:" + data?.Name );
	}

	private static void TestDelete()
	{
		Sandbank.Insert<PlayerData>( "players", TestData.TestData1 );

		Sandbank.Delete<PlayerData>( "players", x => x.ID == TestData.TestData1.ID );

		var data = Sandbank.SelectOneWithID<PlayerData>( "players", TestData.TestData1.ID );

		if ( data != null )
			Fail( "TestDelete() 1:" + data.Name );
	}

	/// <summary>
	/// Ensure that when we receive an object from the database, modifying it doesn't
	/// also modify the cache.
	/// </summary>
	private static void TestReferenceDoesntModifyCache()
	{
		Sandbank.Insert<PlayerData>( "players", TestData.TestData1 );
		var data = Sandbank.SelectOneWithID<PlayerData>( "players", TestData.TestData1.ID );
		data.Level = 999;
		data = Sandbank.SelectOneWithID<PlayerData>( "players", TestData.TestData1.ID );

		if ( data.Level == 999 )
			Fail( "TestReferenceDoesntModifyCache()" );
	}

	private static void TestDeleteWithID()
	{
		Sandbank.Insert<PlayerData>( "players", TestData.TestData1 );

		Sandbank.DeleteWithID<PlayerData>( "players", TestData.TestData1.ID );

		var data = Sandbank.SelectOneWithID<PlayerData>( "players", TestData.TestData1.ID );

		if ( data != null )
			Fail( "TestDeleteWithID() 1:" + data.Name );
	}

	private static void TestAny()
	{
		Sandbank.Insert<PlayerData>( "players", TestData.TestData1 );

		if ( !Sandbank.Any<PlayerData>( "players", x => x.Level == TestData.TestData1.Level ) )
			Fail( "TestAny()" );
	}

	private static void TestAnyWithID()
	{
		Sandbank.Insert<PlayerData>( "players", TestData.TestData1 );

		if ( !Sandbank.AnyWithID<PlayerData>( "players", TestData.TestData1.ID ) )
			Fail( "TestAnyWithID()" );
	}

	private static void TestEnableUnsafeMode()
	{
		Sandbank.EnableUnsafeMode();

		if ( NSSandbank.Config.UNSAFE_MODE != true )
			Fail( "TestEnableUnsafeMode() 1:" + NSSandbank.Config.UNSAFE_MODE.ToString() );
	}

	private static void TestDisableUnsafeMode()
	{
		Sandbank.DisableUnsafeMode();

		if ( NSSandbank.Config.UNSAFE_MODE != false )
			Fail( "TestDisableUnsafeMode() 1:" + NSSandbank.Config.UNSAFE_MODE.ToString() );
	}

	private static void TestEnableIndentJSON()
	{
		Sandbank.EnableIndentJSON();

		if ( NSSandbank.Config.INDENT_JSON != true )
			Fail( "TestEnableIndentJSON() 1:" + NSSandbank.Config.INDENT_JSON.ToString() );
	}

	private static void TestDisableIndentJSON()
	{
		Sandbank.DisableIndentJSON();

		if ( NSSandbank.Config.INDENT_JSON != false )
			Fail( "TestDisableIndentJSON() 1:" + NSSandbank.Config.INDENT_JSON.ToString() );
	}

	private static void TestForceWriteCache()
	{
		TestData.TestData1.ID = "";
		Sandbank.Insert<PlayerData>( "players", TestData.TestData1 );

		var collection = NSSandbank.Cache.GetCollectionByName<PlayerData>( "players", false );

		if ( !collection.CachedDocuments[TestData.TestData1.ID].Stale )
			Fail( "TestForceWriteCache() 1" );

		Sandbank.ForceWriteCache();

		if ( collection.CachedDocuments[TestData.TestData1.ID].Stale )
			Fail( "TestForceWriteCache() 2" );
	}

	private static void TestConcurrencySafety()
	{
		List<Task> tasks = new();

		for (int i = 0; i < 10; i++ )
		{
			tasks.Add(GameTask.RunInThreadAsync( async () =>
			{
				Sandbank.EnableIndentJSON();
				Sandbank.DisableIndentJSON();

				var data = new PlayerData()
				{
					ID = "",
					Health = Game.Random.Next(101),
					Name = "TestPlayer1",
					Level = 10,
					LastPlayTime = DateTime.Now,
					Items = new() { "gun", "frog", "banana" }
				};

				Sandbank.InsertMany<PlayerData>( "players", new() { data, data, data, data } );
				Sandbank.Insert<PlayerData>( "players", data );
				var results = Sandbank.Select<PlayerData>( "players", x => x.Health > 90 );

				if ( results.Count > 0 )
				{
					Sandbank.SelectOneWithID<PlayerData>( "players", results[0].ID );
					Sandbank.DeleteWithID<PlayerData>( "players", results[0].ID );
				}

				Sandbank.Delete<PlayerData>( "players", x => x.Health <= 20 );
				Sandbank.Any<PlayerData>( "players", x => x.Name == "Tim" );
			} ));
		}

		GameTask.WaitAll( tasks.ToArray() );
	}
}
