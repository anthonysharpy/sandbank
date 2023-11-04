using System;

namespace NSSandbank;

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
		Sandbox.Internal.GlobalGameNamespace.Log.Warning( $"Sandbank: {message}" );
	}

	public static void Error( string message )
	{
		Sandbox.Internal.GlobalGameNamespace.Log.Error( $"Sandbank: {message}" );
	}
}
