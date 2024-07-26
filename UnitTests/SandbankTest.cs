using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static TestClasses;

namespace SandbankDatabase;

[TestClass]
public partial class SandbankTest
{
	[TestCleanup]
	public void Cleanup()
	{
		if ( !Sandbank.IsInitialised )
			Sandbank.InitialiseAsync().GetAwaiter().GetResult();

		Sandbank.DeleteAllData();
		Sandbank.Shutdown();
	}

	[TestMethod]
	public void WarnsIfDatabaseNotInitialised()
	{
		var document = new TestClasses.NullUIDClass();

		var e = Assert.ThrowsException<SandbankException>( () => Sandbank.Insert( "nulluidtest", document ) );

		Assert.AreEqual( "Insert failed as the database is not yet initialised - check IsInitialised before making any requests", e.Message );
	}

	[TestMethod]
	public void SelectingUnitialisedCollectionReturnsEmptyList()
	{
		Sandbank.InitialiseAsync().GetAwaiter().GetResult();

		var results = Sandbank.Select<TestClasses.ReadmeExample>( "players", x => x.Health == 50 );

		Assert.AreEqual( 0, results.Count );
	}

	[TestMethod]
	public void NullUIDWorks()
	{
		Sandbank.InitialiseAsync().GetAwaiter().GetResult();

		var document = new TestClasses.NullUIDClass();

		Sandbank.Insert( "nulluidtest", document );

		Assert.AreEqual( 32, document.UID.Length );
	}

	[TestMethod]
	public void NoUIDThrowsException()
	{
		Sandbank.InitialiseAsync().GetAwaiter().GetResult();

		var document = new TestClasses.NoUIDClass();

		var e = Assert.ThrowsException<SandbankException>( () => Sandbank.Insert( "nouidtest", document ) );

		Assert.AreEqual( $"cannot handle a document without a \"UID\" property - make sure your data " +
			"class has a public property called UID, like this: \"[Saved] public string UID { get; set; }\"", e.Message );
	}

	[TestMethod]
	public void CantPutDifferentClassTypesInSameCollection()
	{
		Sandbank.InitialiseAsync().GetAwaiter().GetResult();

		var document1 = new TestClasses.ValidClass1();

		Sandbank.Insert( "test2", document1 );

		var document2 = new TestClasses.ValidClass2();

		var e = Assert.ThrowsException<SandbankException>( () => Sandbank.Insert( "test2", document2 ) );

		Assert.AreEqual( "there is no registered instance pool for the type TestClasses+ValidClass2 - " +
			"are you using the wrong class type for this collection?", e.Message );
	}

	[TestMethod]
	public void CopySavedData()
	{
		var document1 = new ClassWithNonSavedProperty();
		document1.Health = 50;
		document1.Name = "Steve";

		var document2 = new ClassWithNonSavedProperty();

		Assert.AreEqual( 0, document2.Health );
		Assert.AreEqual( null, document2.Name );

		Sandbank.CopySavedData<ClassWithNonSavedProperty>( document1, document2 );

		Assert.AreEqual( 50, document2.Health );
		Assert.AreEqual( null, document2.Name );
	}

	[TestMethod]
	public void CantGetDifferentClassTypesFromSameCollection()
	{
		Sandbank.InitialiseAsync().GetAwaiter().GetResult();

		var document1 = new TestClasses.ValidClass1();
		document1.Health = 50;

		Sandbank.Insert( "test3", document1 );

		var e = Assert.ThrowsException<InvalidCastException>(
			() => Sandbank.SelectOneWithID<TestClasses.ValidClass1Copy>( "test3", document1.UID ) );

		Assert.AreEqual( "Unable to cast object of type 'ValidClass1' to type 'ValidClass1Copy'.",
			e.Message );
	}

	[TestMethod]
	public void ReadmeExampleWorks()
	{
		Sandbank.InitialiseAsync().GetAwaiter().GetResult();

		var readmeExample = new TestClasses.ReadmeExample();

		Assert.AreEqual( null, readmeExample.UID );

		readmeExample.Health = 100;
		readmeExample.Name = "Bob";

		Sandbank.Insert( "players", readmeExample );

		Assert.AreEqual( 32, readmeExample.UID.Length );

		var playerWith100Health = Sandbank.SelectOne<TestClasses.ReadmeExample>( "players", 
			x => x.Health == 100 );

		Assert.AreEqual( "Bob", playerWith100Health.Name );

		Sandbank.DeleteWithID<TestClasses.ReadmeExample>( "players", playerWith100Health.UID );
	}

	[TestMethod]
	public void SelectUnsafeReferences_DocumentDoesntChangeAfterNewInsert()
	{
		Sandbank.InitialiseAsync().GetAwaiter().GetResult();

		var document1 = new TestClasses.ValidClass1();
		document1.Health = 70;

		Sandbank.Insert( "players", document1 );

		var unsafeDocument = Sandbank.SelectUnsafeReferences<TestClasses.ValidClass1>("players",
			x => x.Health == 70 ).First();

		var safeDocument = Sandbank.SelectOne<TestClasses.ValidClass1>( "players",
			x => x.Health == 70 );

		safeDocument.Health = 80;

		Sandbank.Insert( "players", safeDocument );

		Assert.AreEqual( unsafeDocument.Health, 70 );
	}

	[TestMethod]
	public void TestInsertAndSelectOne()
	{
		Sandbank.InitialiseAsync().GetAwaiter().GetResult();

		Sandbank.Insert<ReadmeExample>( "players", TestData.TestData1 );

		Assert.IsFalse( TestData.TestData1.UID.Length <= 10 );

		var data = Sandbank.SelectOne<ReadmeExample>( "players", x => x.UID == TestData.TestData1.UID );

		Assert.IsFalse( data.Name != TestData.TestData1.Name );
	}

	[TestMethod]
	public void TestInsertManyAndSelect()
	{
		Sandbank.InitialiseAsync().GetAwaiter().GetResult();

		var players = new List<ReadmeExample>{ TestData.TestData1, TestData.TestData2 };
		Sandbank.InsertMany<ReadmeExample>( "players", players );

		Assert.IsFalse( players[0].UID.Length <= 10 );
		Assert.IsFalse( players[1].UID.Length <= 10 );

		var data = Sandbank.Select<ReadmeExample>( "players", x => true );

		Assert.IsFalse( data[0].Name != TestData.TestData1.Name && data[0].Name != TestData.TestData2.Name );
		Assert.IsFalse( data[1].Name != TestData.TestData1.Name && data[1].Name != TestData.TestData2.Name );
	}

	[TestMethod]
	public void TestSelectOneWithID()
	{
		Sandbank.InitialiseAsync().GetAwaiter().GetResult();

		Sandbank.Insert<ReadmeExample>( "players", TestData.TestData1 );

		Assert.IsFalse( TestData.TestData1.UID.Length <= 10 );

		var data = Sandbank.SelectOneWithID<ReadmeExample>( "players", TestData.TestData1.UID );

		Assert.IsFalse( data?.Name != TestData.TestData1.Name );
	}

	[TestMethod]
	public void TestDelete()
	{
		Sandbank.InitialiseAsync().GetAwaiter().GetResult();

		Sandbank.Insert<ReadmeExample>( "players", TestData.TestData1 );

		Sandbank.Delete<ReadmeExample>( "players", x => x.UID == TestData.TestData1.UID );

		var data = Sandbank.SelectOneWithID<ReadmeExample>( "players", TestData.TestData1.UID );

		Assert.IsFalse( data != null );
	}

	/// <summary>
	/// Ensure that when we receive an object from the database, modifying it doesn't
	/// also modify the cache.
	/// </summary>
	[TestMethod]
	public void TestReferenceDoesntModifyCache()
	{
		Sandbank.InitialiseAsync().GetAwaiter().GetResult();

		Sandbank.Insert<ReadmeExample>( "players", TestData.TestData1 );
		var data = Sandbank.SelectOneWithID<ReadmeExample>( "players", TestData.TestData1.UID );
		data.Level = 999;
		data = Sandbank.SelectOneWithID<ReadmeExample>( "players", TestData.TestData1.UID );

		Assert.IsFalse( data.Level == 999 );
	}

	/// <summary>
	/// Ensure that when we receive an object from the database, modifying it doesn't
	/// also modify the cache.
	/// </summary>
	[TestMethod]
	public void TestSpammingShutdownAndInitialise_DoesntCorruptData()
	{
		const int documents = 100;

		Sandbank.InitialiseAsync().GetAwaiter().GetResult();

		Sandbank.Insert<ReadmeExample>( "players", new ReadmeExample()
		{
			UID = "",
			Health = 100,
			Name = "TestPlayer1",
			Level = 10,
			LastPlayTime = DateTime.UtcNow,
			Items = new() { "gun", "frog", "banana" }
		} );

		// Warm the pool up to avoid console spam about not being able to load objects from the pool.
		Task.Delay( 4000 ).GetAwaiter().GetResult();

		var data = new List<ReadmeExample>();

		for ( int i = 0; i < documents - 1; i++ )
		{
			data.Add( new ReadmeExample()
			{
				UID = "",
				Health = 100,
				Name = "TestPlayer1",
				Level = 10,
				LastPlayTime = DateTime.UtcNow,
				Items = new() { "gun", "frog", "banana" }
			} );
		}

		Sandbank.InsertMany<ReadmeExample>( "players", data );

		Assert.AreEqual( documents, Sandbank.Select<ReadmeExample>( "players", x => x.Health == 100 ).Count() );

		var tasks = new List<Task>();

		for ( int i = 0; i < 100; i++ )
		{
			tasks.Add( GameTask.RunInThreadAsync( async () => await Sandbank.InitialiseAsync() ) );
			tasks.Add( GameTask.RunInThreadAsync( () => Sandbank.Shutdown() ) );
		}

		Task.WaitAll( tasks.ToArray() );

		Sandbank.InitialiseAsync().GetAwaiter().GetResult();

		Assert.AreEqual( documents, Sandbank.Select<ReadmeExample>( "players", x => x.Health == 100 ).Count() );
	}

	[TestMethod]
	public void TestDeleteWithID()
	{
		Sandbank.InitialiseAsync().GetAwaiter().GetResult();

		Sandbank.Insert<ReadmeExample>( "players", TestData.TestData1 );

		Sandbank.DeleteWithID<ReadmeExample>( "players", TestData.TestData1.UID );

		var data = Sandbank.SelectOneWithID<ReadmeExample>( "players", TestData.TestData1.UID );

		Assert.IsFalse( data != null );
	}

	[TestMethod]
	public void TestAny()
	{
		Sandbank.InitialiseAsync().GetAwaiter().GetResult();

		Sandbank.Insert<ReadmeExample>( "players", TestData.TestData1 );

		Assert.IsFalse( !Sandbank.Any<ReadmeExample>( "players", x => x.Level == TestData.TestData1.Level ) );
	}

	[TestMethod]
	public void TestAnyWithID()
	{
		Sandbank.InitialiseAsync().GetAwaiter().GetResult();

		Sandbank.Insert<ReadmeExample>( "players", TestData.TestData1 );

		Assert.IsFalse( !Sandbank.AnyWithID<ReadmeExample>( "players", TestData.TestData1.UID ) );
	}


	[TestMethod]
	public void TestShutdown()
	{
		Sandbank.InitialiseAsync().GetAwaiter().GetResult();

		TestData.TestData1.UID = "";
		Sandbank.Insert<ReadmeExample>( "players", TestData.TestData1 );

		var collection = Cache.GetCollectionByName<ReadmeExample>( "players", false );

		Assert.AreEqual( 1, Cache.GetDocumentsAwaitingWriteCount() );

		Sandbank.Shutdown();

		Assert.AreEqual( 0, Cache.GetDocumentsAwaitingWriteCount() );
	}

	[TestMethod]
	public void TestConcurrencySafety()
	{
		Sandbank.InitialiseAsync().GetAwaiter().GetResult();

		List<Task> tasks = new();

		for ( int i = 0; i < 10; i++ )
		{
			tasks.Add( Task.Run( () =>
			{
				var data = new ReadmeExample()
				{
					UID = "",
					Health = Game.Random.Next( 101 ),
					Name = "TestPlayer1",
					Level = 10,
					LastPlayTime = DateTime.UtcNow,
					Items = new() { "gun", "frog", "banana" }
				};

				Sandbank.InsertMany<ReadmeExample>( "players", new ReadmeExample[] { data, data, data, data } );
				Sandbank.Insert<ReadmeExample>( "players", data );
				var results = Sandbank.Select<ReadmeExample>( "players", x => x.Health > 90 );

				if ( results.Count > 0 )
				{
					Sandbank.SelectOneWithID<ReadmeExample>( "players", results[0].UID );
					Sandbank.DeleteWithID<ReadmeExample>( "players", results[0].UID );
				}

				Sandbank.Delete<ReadmeExample>( "players", x => x.Health <= 20 );
				Sandbank.Any<ReadmeExample>( "players", x => x.Name == "Tim" );
			} ) );
		}

		Task.WaitAll( tasks.ToArray() );
	}

	[TestMethod]
	public void TestConcurrencySafety2()
	{
		Sandbank.InitialiseAsync().GetAwaiter().GetResult();

		List<Task> tasks = new();

		List<ReadmeExample> loadsOfData = new();

		for ( int i = 0; i < 10000; i++ )
		{
			loadsOfData.Add( new ReadmeExample()
			{
				UID = "",
				Health = 100,
				Name = "TestPlayer",
				Level = Game.Random.Next( 10 ),
				LastPlayTime = DateTime.UtcNow,
				Items = new() { "gun", "frog", "banana" }
			} );
		}

		for ( int i = 0; i < 10; i++ )
		{
			tasks.Add( Task.Run( () =>
			{
				for ( int j = 0; j < 10000; j++ )
				{
					var data = new ReadmeExample()
					{
						UID = "",
						Health = 100,
						Name = "TestPlayer",
						Level = 10,
						LastPlayTime = DateTime.UtcNow,
						Items = new() { "gun", "frog", "banana" }
					};

					Sandbank.Insert<ReadmeExample>( "players", data );
					Sandbank.Insert<ReadmeExample>( "players_two", data );
				}
			} ) );

			tasks.Add( Task.Run( () =>
			{
				Sandbank.InsertMany<ReadmeExample>( "players", loadsOfData );
				Sandbank.InsertMany<ReadmeExample>( "players_two", loadsOfData );
			} ) );

			tasks.Add( Task.Run( () =>
			{
				Sandbank.Select<ReadmeExample>( "players", x => x.Health == 100 );
				Sandbank.Select<ReadmeExample>( "players_two", x => x.Name == "TestPlayer" );
			} ) );

			tasks.Add( Task.Run( () =>
			{
				Sandbank.Delete<ReadmeExample>( "players", x => x.Level == 5 || x.Level == 2 );
				Sandbank.Delete<ReadmeExample>( "players_two", x => x.Level == 5 || x.Level == 2 );
			} ) );
		}

		Task.WaitAll( tasks.ToArray() );
	}

	[TestMethod]
	public void TestConcurrencySafety_SpamCollections()
	{
		Sandbank.InitialiseAsync().GetAwaiter().GetResult();

		List<Task> tasks = new();

		List<ReadmeExample> loadsOfData = new();

		for ( int i = 0; i < 100; i++ )
		{
			loadsOfData.Add( new ReadmeExample()
			{
				UID = "",
				Health = 100,
				Name = "TestPlayer",
				Level = Game.Random.Next( 10 ),
				LastPlayTime = DateTime.UtcNow,
				Items = new() { "gun", "frog", "banana" }
			} );
		}

		for ( int x = 0; x < 10; x++ )
		{
			for ( int i = 0; i < 100; i++ )
			{
				string collection = "collection" + i;

				tasks.Add( Task.Run( () =>
				{
					for ( int i = 0; i < 100; i++ )
					{
						Sandbank.Insert<ReadmeExample>( collection, loadsOfData[i] );
					}
				} ) );

				tasks.Add( Task.Run( () =>
				{
					Sandbank.Delete<ReadmeExample>( collection, x => x.Level == 5 || x.Level == 2 );
				} ) );
			}
		}

		Task.WaitAll( tasks.ToArray() );
	}
}
