using Sandbox;
using System;
using System.Linq;
using static TestClasses;

namespace SandbankDatabase;

[TestClass]
public partial class BackupsTest
{
	[TestCleanup]
	public void Cleanup()
	{
		Config.BACKUP_FREQUENCY = BackupFrequency.Daily;
		Config.BACKUPS_TO_KEEP = 10;
		Config.OBFUSCATE_FILES = false;

		Sandbank.DeleteAllData();
		Sandbank.Shutdown().GetAwaiter().GetResult();
		Sandbank.DeleteAllBackups();
	}

	[TestMethod]
	public void TestBackupsGetCreated()
	{
		Sandbank.Insert( "test", new ReadmeExample()
		{
			UID = "",
			Health = 100,
			Name = "TestPlayer",
			Level = Game.Random.Next( 10 ),
			LastPlayTime = DateTime.UtcNow,
			Items = new() { "gun", "frog", "banana" }
		} );

		Backups.CheckBackupStatus();

		Assert.AreEqual( 1, FileController.ListBackupFolders().Count );
	}

	[TestMethod]
	public void TestDeleteAllBackups()
	{
		FileController.Initialise();

		FileController.CreateBackupCollectionFolder( "invalid", new Collection
		{
			CollectionName = "test1"
		} );

		FileController.CreateBackupCollectionFolder( "2024-11-23 12pm", new Collection
		{
			CollectionName = "test2"
		} );

		Assert.AreEqual( 2, FileController.ListBackupFolders().Count );

		Sandbank.DeleteAllBackups();

		Assert.AreEqual( 0, FileController.ListBackupFolders().Count );
	}

	[TestMethod]
	public void TestInvalidBackupFoldersGetIgnored()
	{
		Sandbank.Insert( "test", new ReadmeExample()
		{
			UID = "",
			Health = 100,
			Name = "TestPlayer",
			Level = Game.Random.Next( 10 ),
			LastPlayTime = DateTime.UtcNow,
			Items = new() { "gun", "frog", "banana" }
		} );

		FileController.CreateBackupCollectionFolder( "invalid-name", new Collection
		{
			CollectionName = "test"
		} );

		Assert.AreEqual( 1, FileController.ListBackupFolders().Count );

		Backups.CheckBackupStatus();

		// We know it's been treated as invalid because it will try to create another backup.
		Assert.AreEqual( 2, FileController.ListBackupFolders().Count );
	}

	[TestMethod]
	public void TestOnlyOneBackupGetsMade()
	{
		Sandbank.Insert( "test", new ReadmeExample()
		{
			UID = "",
			Health = 100,
			Name = "TestPlayer",
			Level = Game.Random.Next( 10 ),
			LastPlayTime = DateTime.UtcNow,
			Items = new() { "gun", "frog", "banana" }
		} );

		Backups.CheckBackupStatus();
		Backups.CheckBackupStatus();
		Backups.CheckBackupStatus();
		Backups.CheckBackupStatus();
		Backups.CheckBackupStatus();
		
		Assert.AreEqual( 1, FileController.ListBackupFolders().Count );
	}

	[TestMethod]
	public void TestBackupFolderHasCorrectName()
	{
		Sandbank.Insert( "test", new ReadmeExample()
		{
			UID = "",
			Health = 100,
			Name = "TestPlayer",
			Level = Game.Random.Next( 10 ),
			LastPlayTime = DateTime.UtcNow,
			Items = new() { "gun", "frog", "banana" }
		} );

		Backups.CheckBackupStatus();

		var nowString = DateTime.Now.Date.AddHours( DateTime.Now.Hour ).ToString( Backups.BACKUP_DATE_FORMAT );
		var nowStringPlusOneHour = DateTime.Now.Date.AddHours( DateTime.Now.Hour + 1 ).ToString( Backups.BACKUP_DATE_FORMAT );
		var firstFolder = FileController.ListBackupFolders().First();

		Assert.IsTrue( firstFolder == nowString || firstFolder == nowStringPlusOneHour );
	}

	[TestMethod]
	public void TestBackupContainsCollectionDefinition()
	{
		Sandbank.InitialiseAsync().GetAwaiter().GetResult();

		Sandbank.Insert( "test", new ReadmeExample()
		{
			UID = "12345",
			Health = 100,
			Name = "TestPlayer",
			Level = Game.Random.Next( 10 ),
			LastPlayTime = DateTime.UtcNow,
			Items = new() { "gun", "frog", "banana" }
		} );

		Backups.CheckBackupStatus();

		var folderName = FileController.ListBackupFolders().First();
		var files = FileController.ListFiles( $"sandbank_backups/{folderName}/test" );

		// Should be collection definition and one document.
		Assert.AreEqual( 2, files.Count );

		Assert.AreEqual( "12345", files[0] );
		Assert.AreEqual( "definition.txt", files[1] );
	}

	[TestMethod]
	public void TestBackupSavesDocumentsCorrectly()
	{
		Sandbank.InitialiseAsync().GetAwaiter().GetResult();

		Sandbank.Insert( "test", new ReadmeExample()
		{
			UID = "12345",
			Health = 100,
			Name = "TestPlayer",
			Level = Game.Random.Next( 10 ),
			LastPlayTime = DateTime.UtcNow,
			Items = new() { "gun", "frog", "banana" }
		} );

		Backups.CheckBackupStatus();
		var folderName = FileController.ListBackupFolders().First();

		var fileContents = FileController.ReadFile( $"sandbank_backups/{folderName}/test/12345" );

		Assert.IsTrue( fileContents.Contains( "TestPlayer" ) );
	}

	[TestMethod]
	public void TestBackupWorksWithObfuscation()
	{
		Config.OBFUSCATE_FILES = true;
		Sandbank.InitialiseAsync().GetAwaiter().GetResult();

		Sandbank.Insert( "test", new ReadmeExample()
		{
			UID = "12345",
			Health = 100,
			Name = "TestPlayer",
			Level = Game.Random.Next( 10 ),
			LastPlayTime = DateTime.UtcNow,
			Items = new() { "gun", "frog", "banana" }
		} );

		Backups.CheckBackupStatus();
		var folderName = FileController.ListBackupFolders().First();

		var fileContents = FileController.ReadFile( $"sandbank_backups/{folderName}/test/12345" );

		Assert.IsTrue( fileContents.Contains( "OBFS|" ) );
	}

	[TestMethod]
	public void TestBackupDeletesOldestBackup()
	{
		Config.BACKUPS_TO_KEEP = 3;
		Sandbank.InitialiseAsync().GetAwaiter().GetResult();

		var collection = new Collection
		{
			CollectionName = "test"
		};

		FileController.CreateBackupCollectionFolder( "2024-11-10 12pm", collection );
		FileController.CreateBackupCollectionFolder( "2024-11-11 12pm", collection );
		FileController.CreateBackupCollectionFolder( "2024-11-12 12pm", collection );

		Sandbank.Insert( "test", new ReadmeExample()
		{
			UID = "12345",
			Health = 100,
			Name = "TestPlayer",
			Level = Game.Random.Next( 10 ),
			LastPlayTime = DateTime.UtcNow,
			Items = new() { "gun", "frog", "banana" }
		} );

		Backups.CheckBackupStatus();
		
		var backupFolders = FileController.ListBackupFolders();

		Assert.AreEqual( 3, backupFolders.Count );
		Assert.IsFalse( backupFolders.Contains( "2024-11-10 12pm" ) );
		Assert.IsTrue( backupFolders.Contains( "2024-11-11 12pm" ) );
		Assert.IsTrue( backupFolders.Contains( "2024-11-12 12pm" ) );
	}

	[TestMethod]
	public void TestBackupDoesntMakeBackupIfNoData()
	{
		Sandbank.InitialiseAsync().GetAwaiter().GetResult();

		Backups.CheckBackupStatus();

		var backupFolders = FileController.ListBackupFolders();

		Assert.AreEqual( 0, backupFolders.Count );
	}

	[TestMethod]
	public void TestBackupNotCreatedIfBackupFrequencySetToNever()
	{
		Config.BACKUP_FREQUENCY = BackupFrequency.Never;

		Sandbank.Insert( "test", new ReadmeExample()
		{
			UID = "",
			Health = 100,
			Name = "TestPlayer",
			Level = Game.Random.Next( 10 ),
			LastPlayTime = DateTime.UtcNow,
			Items = new() { "gun", "frog", "banana" }
		} );

		Backups.CheckBackupStatus();

		Assert.AreEqual( 0, FileController.ListBackupFolders().Count );
	}
}
