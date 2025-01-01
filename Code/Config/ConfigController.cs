using System;
using System.Linq;

namespace SandbankDatabase;

internal class ConfigController
{
	public static bool STARTUP_SHUTDOWN_MESSAGES = true;
	public static bool WARNINGS_AS_EXCEPTIONS = false;
	public static bool CLIENTS_CAN_USE = false;
	public static bool INDENT_JSON = true;
	public static float PERSIST_EVERY_N_SECONDS = 10f;
	public static int PARTIAL_WRITES_PER_SECOND = 1;
	public static bool ENABLE_LOGGING = false;
	public static string DATABASE_NAME = "sandbank";
	public static float TICK_DELTA = 0.1f;
	public static int CLASS_INSTANCE_POOL_SIZE = 2000;
	public static bool MERGE_JSON = true;
	public static bool OBFUSCATE_FILES = false;
	public static string SBSERVER_USER_ID = "";
	public static string SBSERVER_PUBLIC_KEY = "";
	public static OnEndpointErrorBehaviour ON_ENDPOINT_ERROR_BEHAVIOUR = OnEndpointErrorBehaviour.LogWarning;
	public static BackupFrequency BACKUP_FREQUENCY = BackupFrequency.Hourly;
	public static int BACKUPS_TO_KEEP = 10;

	private const string DEFAULT_CONFIG_FILE =
@"# Whether to show the startup and shutdown messages in the console when the database stops and starts.
STARTUP_SHUTDOWN_MESSAGES=true
	
# If this is true then all warnings are thrown as exceptions. I probably wouldn't recommend this but you can enable it
# if you want. This is used in the unit tests to make life easier.
WARNINGS_AS_EXCEPTIONS=false

# Set this to true if you want clients to be able to use the database too. You probably don't want this - turning this on
# doesn't magically sync data between host and clients. But there might be some situations where you want to store data on
# the client for some reason.
CLIENTS_CAN_USE=false

# This controls whether the written JSON files are indented or not. Indentation makes them more human-readable, but
# probably makes saving to disk a little bit slower.
INDENT_JSON=true

# The database will try to make sure that all stale data is written to disk at most every this many seconds. In the event
# of a crash, all stale data is lost, so lower numbers are ""safer"". But lower numbers can lead to decreased performance
# under heavy loads due to increased disk writing.
PERSIST_EVERY_N_SECONDS=10

# We will only try to perform a partial write this many times per second. A partial write doesn't write everything, so
# changing this will not really change your performance. But it does increase performance by ensuring that are we not spamming
# writes every tick. You probably don't want to change this.
PARTIAL_WRITES_PER_SECOND=1

# Enables logging for helping to diagnose issues. This is mostly for development purposes.
ENABLE_LOGGING=false

# This is the name of the folder where your files are kept (e.g. ""sandbank/my_collection""). There's no reason to change it,
# but you're more than welcome to. If you're renaming an existing database, make sure to copy your files across to the new folder.
DATABASE_NAME=sandbank

# How often the database ticks in seconds. I don't recommend changing this as you are not necessarily making things any faster.
TICK_DELTA=0.1

# The number of instances of each class used by your database that will be cached in RAM for faster fetching. Increasing this
# will improve performance if you are selecting lots of records. The optimal value for this is roughly twice your peak per-second
# fetch rate. For example, if at your peak you are fetching 1,000 records per second, for optimal performance, the recommended
# value for this would be 2,000. You will probably see little-to-no performance gain by increasing this further.
#
# Increasing this will increase memory usage. The memory increase is not affected by the number of records in a collection,
# but it is affected by the complexity of your data class. As a very rough rule, a 100,000 pool size takes up around 200mb
# RAM for each collection. A very rough formula for estimating the total memory usage is:
#
# 1mb * number of collections * CLASS_INSTANCE_POOL_SIZE / 500
CLASS_INSTANCE_POOL_SIZE=2000

# This should always true unless you know what you are doing.
#
# If this is true and you rename a field in your data class, the renamed data will remain in the file. For example, if you
# renamed ""Name"" to ""PlayerName"", both properties would still be there. This is because any existing JSON is ""merged""
# with any updates.
#
# If this is false and you rename a field, the renamed data is destroyed. The new document simply overwrites the file and no
# merge is done. So if you renamed ""Name"" to ""PlayerName"", ""Name"" is destroyed.
#
# This is here to protect you, so the only time this should be set to false is if you're ready to remove the renamed data.
MERGE_JSON=true

# Enabling this option will obfuscate files stored on the local filesystem, making them (almost) impossible to edit. This is
# useful if you want to store data on the client that you don't want them to be able to change easily.
#
# Note that this is not secure. If someone really wanted to, they could reverse-engineer the data and change it to whatever
# they want. However most people will not have the skills or inclination to do this.
#
# Note that this will cause saving and loading files to become a bit slower and more CPU intensive.
#
# The database will work whether this is enabled or not, regardless of whether some or all of the files are obfuscated. Files
# are only obfuscated/unobfuscated when they are saved, so changing this will have no impact on files until those files
# are re-saved.
OBFUSCATE_FILES=false

# Your Sandbank Server user ID (if any). Most people can ignore this.
SBSERVER_USER_ID=

# Your Sandbank Server public key (if any). Most people can ignore this.
SBSERVER_PUBLIC_KEY=

# When an endpoint fails, it will return null. By default it will also log a warning. You can change that here. Most people can
# ignore this. Valid options are DoNothing and LogWarning.
ON_ENDPOINT_ERROR_BEHAVIOUR=LogWarning

# Controls how often the database should be backed up. Valid options are Never, Hourly, Daily and Weekly.
BACKUP_FREQUENCY=Hourly

# How many backups should be kept. If the number of backups is greater than this, the oldest backup is deleted.
#
# Make sure you have enough storage to cover your needs. For example, if your database is 10MB big, and you want 100 backups,
# then you need at least 1GB of free disk storage.
BACKUPS_TO_KEEP=10";

	public static string GetDefaultConfigFileContents()
	{
		if ( !TestHelpers.IsUnitTests )
			throw new SandbankException( "this can only be used during tests" );

		return DEFAULT_CONFIG_FILE;
	}

	public static void CreateConfigFileIfNone()
	{
		if ( FileController.FileExists( "sandbank_config.ini", "/" ) )
			return;

		FileController.WriteFile( "sandbank_config.ini", DEFAULT_CONFIG_FILE );
	}

	private static T InterpretConfigOption<T>( string[] fileLines, string key ) where T : notnull
	{
		var line = fileLines.Where( x => x.StartsWith( key ) ).LastOrDefault();

		if ( line == null )
			throw new SandbankException( $"sandbank_config.ini is corrupt - it is missing the \"{key}\" option" );

		var parts = line.Split( '=' );
		var value = "";

		if ( parts.Length == 2 )
			value = parts[1];

		return typeof( T ) switch
		{
			Type t when t == typeof( string ) => (T)(object)value,
			Type t when t == typeof( int ) => (T)(object)int.Parse( value ),
			Type t when t == typeof( float ) => (T)(object)float.Parse( value ),
			Type t when t == typeof( bool ) => (T)(object)bool.Parse( value ),
			Type t when t == typeof( OnEndpointErrorBehaviour ) => (T)Enum.Parse( typeof( OnEndpointErrorBehaviour ), value ),
			Type t when t == typeof( BackupFrequency ) => (T)Enum.Parse( typeof( BackupFrequency ), value ),
			_ => throw new SandbankException( "unsupported type" )
		};
	}

	public static void LoadConfigFile()
	{
		var lines = FileController.ReadFile( "sandbank_config.ini" ).Replace("\r", "").Split( '\n' );

		STARTUP_SHUTDOWN_MESSAGES = InterpretConfigOption<bool>( lines, "STARTUP_SHUTDOWN_MESSAGES" );
		WARNINGS_AS_EXCEPTIONS = InterpretConfigOption<bool>( lines, "WARNINGS_AS_EXCEPTIONS" );
		CLIENTS_CAN_USE = InterpretConfigOption<bool>( lines, "CLIENTS_CAN_USE" );
		INDENT_JSON = InterpretConfigOption<bool>( lines, "INDENT_JSON" );
		ENABLE_LOGGING = InterpretConfigOption<bool>( lines, "ENABLE_LOGGING" );
		MERGE_JSON = InterpretConfigOption<bool>( lines, "MERGE_JSON" );
		OBFUSCATE_FILES = InterpretConfigOption<bool>( lines, "OBFUSCATE_FILES" );
		PERSIST_EVERY_N_SECONDS = InterpretConfigOption<float>( lines, "PERSIST_EVERY_N_SECONDS" );
		TICK_DELTA = InterpretConfigOption<float>( lines, "TICK_DELTA" );
		PARTIAL_WRITES_PER_SECOND = InterpretConfigOption<int>( lines, "PARTIAL_WRITES_PER_SECOND" );
		CLASS_INSTANCE_POOL_SIZE = InterpretConfigOption<int>( lines, "CLASS_INSTANCE_POOL_SIZE" );
		BACKUPS_TO_KEEP = InterpretConfigOption<int>( lines, "BACKUPS_TO_KEEP" );
		DATABASE_NAME = InterpretConfigOption<string>( lines, "DATABASE_NAME" );
		SBSERVER_USER_ID = InterpretConfigOption<string>( lines, "SBSERVER_USER_ID" );
		SBSERVER_PUBLIC_KEY = InterpretConfigOption<string>( lines, "SBSERVER_PUBLIC_KEY" );
		ON_ENDPOINT_ERROR_BEHAVIOUR = InterpretConfigOption<OnEndpointErrorBehaviour>( lines, "ON_ENDPOINT_ERROR_BEHAVIOUR" );
		BACKUP_FREQUENCY = InterpretConfigOption<BackupFrequency>( lines, "BACKUP_FREQUENCY" );
	}
}
