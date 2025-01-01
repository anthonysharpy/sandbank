namespace SandbankDatabase;

[TestClass]
public partial class ConfigurationTest
{
	[TestInitialize]
	public void Initialise()
	{
		FileController.Initialise();

		// Do this on start and on finish so not to have any interference from (or to) other tests.
		FileController.DeleteFile( "sandbank_config.ini" );
	}

	[TestCleanup]
	public void Cleanup()
	{
		Sandbank.Shutdown().GetAwaiter().GetResult();
		FileController.DeleteFile( "sandbank_config.ini" );
	}

	[TestMethod]
	public void TestConfigFilesGetsCreatedAutomatically()
	{
		Assert.IsFalse( FileController.FileExists( "sandbank_config.ini", "/" ) );
		InitialisationController.Initialise();
		Assert.IsTrue( FileController.FileExists( "sandbank_config.ini", "/" ) );
	}

	[TestMethod]
	public void TestConfigFileUsedIfAlreadyExists()
	{
		var defaultFile = ConfigController.GetDefaultConfigFileContents();
		defaultFile = defaultFile.Replace( "BACKUPS_TO_KEEP=10", "BACKUPS_TO_KEEP=123" );
		FileController.WriteFile( "sandbank_config.ini", defaultFile );

		InitialisationController.Initialise();

		Assert.AreEqual( 123, ConfigController.BACKUPS_TO_KEEP );
	}

	[TestMethod]
	public void TestEnumsAreParsedCorrectly()
	{
		var defaultFile = ConfigController.GetDefaultConfigFileContents();
		defaultFile = defaultFile.Replace( "ON_ENDPOINT_ERROR_BEHAVIOUR=LogWarning", "ON_ENDPOINT_ERROR_BEHAVIOUR=DoNothing" );
		defaultFile = defaultFile.Replace( "BACKUP_FREQUENCY=Hourly", "BACKUP_FREQUENCY=Daily" );
		FileController.WriteFile( "sandbank_config.ini", defaultFile );

		InitialisationController.Initialise();

		Assert.AreEqual( BackupFrequency.Daily, ConfigController.BACKUP_FREQUENCY );
		Assert.AreEqual( OnEndpointErrorBehaviour.DoNothing, ConfigController.ON_ENDPOINT_ERROR_BEHAVIOUR );
	}

	[TestMethod]
	public void TestThrowsErrorIfKeyMissing()
	{
		var defaultFile = ConfigController.GetDefaultConfigFileContents();
		defaultFile = defaultFile.Replace( "BACKUPS_TO_KEEP=10", "BACKUPS_TO_KEE=10" );
		FileController.WriteFile( "sandbank_config.ini", defaultFile );

		// This will throw an exception and the database won't be initialised.
		InitialisationController.Initialise();

		Assert.AreEqual( InitialisationController.CurrentDatabaseState, DatabaseState.Uninitialised );
	}
}
