using Sandbox;
using System;
using System.Collections.Generic;

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
			TestEnableIndentJSON,
			TestConcurrencySafety,
			TestDisableUnsafeMode
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
		var insertedData = Sandbank.Insert<PlayerData>( "players", TestData.TestData1 );

		if ( insertedData.ID.Length <= 10 )
			Fail( insertedData.ID );

		var data = Sandbank.SelectOne<PlayerData>("players", x => x.ID == insertedData.ID );

		if ( data.Name != TestData.TestData1.Name )
			Fail( data.Name );
	}

	private static void TestInsertManyAndSelect()
	{
		var players = new List<PlayerData> { TestData.TestData1, TestData.TestData2 };
		var insertedData = Sandbank.InsertMany<PlayerData>( "players", players);

		if ( insertedData[0].ID.Length <= 10 )
			Fail( insertedData[0].ID );
		if ( insertedData[1].ID.Length <= 10 )
			Fail( insertedData[1].ID );

		var data = Sandbank.Select<PlayerData>( "players", x => true );

		if ( data[0].Name != TestData.TestData1.Name && data[0].Name != TestData.TestData2.Name )
			Fail( data[0].Name );
		if ( data[1].Name != TestData.TestData1.Name && data[1].Name != TestData.TestData2.Name )
			Fail( data[1].Name );
	}

	private static void TestSelectOneWithID()
	{
		var insertedData = Sandbank.Insert<PlayerData>( "players", TestData.TestData1 );

		if ( insertedData.ID.Length <= 10 )
			Fail( insertedData.ID );

		var data = Sandbank.SelectOneWithID<PlayerData>( "players", insertedData.ID );

		if ( data.Name != TestData.TestData1.Name )
			Fail( data.Name );
	}

	private static void TestDelete()
	{
		var insertedData = Sandbank.Insert<PlayerData>( "players", TestData.TestData1 );

		Sandbank.Delete<PlayerData>( "players", x => x.ID == insertedData.ID );

		var data = Sandbank.SelectOneWithID<PlayerData>( "players", insertedData.ID );

		if ( data != null )
			Fail( data.Name );
	}

	private static void TestDeleteWithID()
	{
		var insertedData = Sandbank.Insert<PlayerData>( "players", TestData.TestData1 );

		Sandbank.DeleteWithID<PlayerData>( "players", insertedData.ID );

		var data = Sandbank.SelectOneWithID<PlayerData>( "players", insertedData.ID );

		if ( data != null )
			Fail( data.Name );
	}

	private static void TestAny()
	{
		var insertedData = Sandbank.Insert<PlayerData>( "players", TestData.TestData1 );

		if ( !Sandbank.Any<PlayerData>( "players", x => x.Level == insertedData.Level ) )
			Fail("");
	}

	private static void TestAnyWithID()
	{
		var insertedData = Sandbank.Insert<PlayerData>( "players", TestData.TestData1 );

		if ( !Sandbank.AnyWithID<PlayerData>( "players", insertedData.ID ) )
			Fail( "" );
	}

	private static void TestEnableUnsafeMode()
	{
		Sandbank.EnableUnsafeMode();

		if ( NSSandbank.Config.UNSAFE_MODE != true )
			Fail( NSSandbank.Config.UNSAFE_MODE.ToString() );
	}

	private static void TestDisableUnsafeMode()
	{
		Sandbank.DisableUnsafeMode();

		if ( NSSandbank.Config.UNSAFE_MODE != false )
			Fail( NSSandbank.Config.UNSAFE_MODE.ToString() );
	}

	private static void TestEnableIndentJSON()
	{
		Sandbank.EnableIndentJSON();

		if ( NSSandbank.Config.INDENT_JSON != true )
			Fail( NSSandbank.Config.INDENT_JSON.ToString() );
	}

	private static void TestDisableIndentJSON()
	{
		Sandbank.DisableIndentJSON();

		if ( NSSandbank.Config.INDENT_JSON != false )
			Fail( NSSandbank.Config.INDENT_JSON.ToString() );
	}

	private static void TestConcurrencySafety()
	{
		for (int i = 0; i < 100; i++ )
		{
			GameTask.RunInThreadAsync( async () =>
			{
				Sandbank.EnableIndentJSON();
				Sandbank.DisableIndentJSON();

				var data = new PlayerData()
				{
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
			} );
		}
	}
}
