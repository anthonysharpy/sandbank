using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace SandbankDatabase;

internal static class Backups
{
	public const string BACKUP_DATE_FORMAT = "yyyy-MM-dd htt";

	/// <summary>
	/// Only allow one backup at once.
	/// </summary>
	public static object BackupLock = new();

	/// <summary>
	/// Check if we should do a backup. If we should, do one. Also check if we have too many backups and
	/// need to delete one.
	/// </summary>
	public static void CheckBackupStatus()
	{
		lock ( BackupLock )
		{
			try
			{
				var backups = GetBackups();

				// Do backups maybe.
				if ( ShouldDoBackup( backups ) )
					DoBackup();

				var numberOfBackups = backups.Count + 1;

				// Delete a backup if we have too many.
				if ( numberOfBackups > Config.BACKUPS_TO_KEEP )
					FileController.DeleteBackup( backups.First().FolderName );
			}
			catch ( Exception e )
			{
				Logging.Warn( "checking backups failed: " + Logging.ExtractExceptionString( e ) );
			}
		}
	}

	/// <summary>
	/// Returns the existing backup records in order from oldest to newest.
	/// </summary>
	private static List<Backup> GetBackups()
	{
		var backupFolders = FileController.ListBackupFolders();

		var invalidNames = backupFolders
			.Where( x => DateTime.TryParseExact( x, BACKUP_DATE_FORMAT, CultureInfo.CurrentCulture, DateTimeStyles.None, out _ ) == false );

		if ( invalidNames.Any() )
			Logging.Warn( $"backup folder {invalidNames.First()} has an invalid name, ignoring..." );

		backupFolders = backupFolders
			.Where( x => DateTime.TryParseExact( x, BACKUP_DATE_FORMAT, CultureInfo.CurrentCulture, DateTimeStyles.None, out _ ) == true )
			.ToList();

		return backupFolders
			.Select( x => new Backup
			{
				BackupTime = DateTime.ParseExact( x, BACKUP_DATE_FORMAT, CultureInfo.CurrentCulture ),
				FolderName = x
			} )
			.OrderBy( x => x.BackupTime )
			.ToList();
	}

	private static bool ShouldDoBackup( List<Backup> backups )
	{
		var mostRecentBackupTime = backups.Count > 0 ? backups.Last().BackupTime : DateTime.MinValue;

		switch ( Config.BACKUP_FREQUENCY )
		{
			case BackupFrequency.Never:
				return false;
			case BackupFrequency.Hourly:
				return DateTime.UtcNow.Subtract( mostRecentBackupTime ).TotalHours >= 1;
			case BackupFrequency.Daily:
				return DateTime.UtcNow.Subtract( mostRecentBackupTime ).TotalDays >= 1;
			case BackupFrequency.Weekly:
				return DateTime.UtcNow.Subtract( mostRecentBackupTime ).TotalDays >= 7;
			default:
				throw new SandbankException( $"unknown backup frequency {Config.BACKUP_FREQUENCY}" );
		}
	}

	private static void DoBackup()
	{
		Logging.Info( "performing backup..." );

		var stopwatch = Stopwatch.StartNew();

		var backupFolderName = DateTime.UtcNow.Date.AddHours( DateTime.UtcNow.Hour ).ToString( BACKUP_DATE_FORMAT );
		var collections = Cache.GetAllCollections();

		foreach ( var collection in collections )
		{
			Logging.Info( $"backing up collection {collection.CollectionName}..." );

			FileController.CreateBackupCollectionFolder( backupFolderName, collection );
			FileController.SaveBackupCollectionDefinition( backupFolderName, collection );

			foreach ( var document in collection.CachedDocuments )
			{
				FileController.SaveBackupDocument( backupFolderName, collection, document.Value );
			}
		}

		Logging.Log( $"backup took {stopwatch.Elapsed.TotalSeconds} seconds" );

		Logging.Info( "backup complete!" );
	}
}

struct Backup
{
	public DateTime BackupTime { get; set; }
	public string FolderName;
}
