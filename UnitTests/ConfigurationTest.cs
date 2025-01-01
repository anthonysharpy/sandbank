namespace SandbankDatabase;

[TestClass]
public partial class ConfigurationTest
{
	[TestCleanup]
	public void Cleanup()
	{
		Sandbank.Shutdown().GetAwaiter().GetResult();
		FileController.DeleteFile( "config.ini" );
	}

	[TestMethod]
	public void TestConfigFilesGetsCreatedAutomatically()
	{
		FileController.Initialise();
		Assert.IsFalse( FileController.FileExists( "config.ini", "/" ) );
		InitialisationController.Initialise();
		Assert.IsTrue( FileController.FileExists( "config.ini", "/" ) );
	}

	[TestMethod]
	public void TestConfigFileUsedIfAlreadyExists()
	{
		FileController.Initialise();
		var defaultFile = ConfigController.GetDefaultConfigFileContents();
		defaultFile = defaultFile.Replace( "BACKUPS_TO_KEEP=10", "BACKUPS_TO_KEEP=123" );
		FileController.WriteFile( "config.ini", defaultFile );

		InitialisationController.Initialise();

		Assert.AreEqual( 123, ConfigController.BACKUPS_TO_KEEP );
	}

	[TestMethod]
	public void TestEnumsAreParsedCorrectly()
	{
		FileController.Initialise();
		var defaultFile = ConfigController.GetDefaultConfigFileContents();
		defaultFile = defaultFile.Replace( "ON_ENDPOINT_ERROR_BEHAVIOUR=LogWarning", "ON_ENDPOINT_ERROR_BEHAVIOUR=DoNothing" );
		defaultFile = defaultFile.Replace( "BACKUP_FREQUENCY=Hourly", "BACKUP_FREQUENCY=Daily" );
		FileController.WriteFile( "config.ini", defaultFile );

		InitialisationController.Initialise();

		Assert.AreEqual( BackupFrequency.Daily, ConfigController.BACKUP_FREQUENCY );
		Assert.AreEqual( OnEndpointErrorBehaviour.DoNothing, ConfigController.ON_ENDPOINT_ERROR_BEHAVIOUR );
	}

	[TestMethod]
	public void TestThrowsErrorIfKeyMissing()
	{
		FileController.Initialise();
		var defaultFile = ConfigController.GetDefaultConfigFileContents();
		defaultFile = defaultFile.Replace( "BACKUPS_TO_KEEP=10", "BACKUPS_TO_KEE=10" );
		FileController.WriteFile( "config.ini", defaultFile );

		// This will throw an exception and the database won't be initialised.
		InitialisationController.Initialise();

		Assert.AreEqual( InitialisationController.CurrentDatabaseState, DatabaseState.Uninitialised );
	}
}
