using Sandbox;
using System;

namespace SandbankDatabase;

static class Logging
{
	public static void Log(string message)
	{
		if ( !Config.ENABLE_LOGGING )
			return;

		Sandbox.Internal.GlobalGameNamespace.Log.Info( $"Sandbank: {message}" );
	}

	public static void Warn( string message )
	{
		if ( Config.WARNINGS_AS_EXCEPTIONS )
			throw new Exception( $"Sandbank: {message}" );

		Sandbox.Internal.GlobalGameNamespace.Log.Warning( $"Sandbank: {message}" );
	}

	public static void Error( string message )
	{
		if (Config.WARNINGS_AS_EXCEPTIONS )
			throw new Exception( $"Sandbank: {message}" );

		Sandbox.Internal.GlobalGameNamespace.Log.Error( $"Sandbank: {message}" );
	}
	 
	public static void PrintStatus()
	{
		Sandbox.Internal.GlobalGameNamespace.Log.Info( $"Sandbank: documents awaiting write: {Cache.GetDocumentsAwaitingWriteCount()}" );
	}

	public static string ExtractExceptionString(Exception e)
	{
		return $"{e.Message}\n\n{e.StackTrace}\n{e.InnerException}";
	}
}
