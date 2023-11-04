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

	/// <summary>
	/// Throws an exception and outputs to s&box's error output (just to be safe).
	/// </summary>
	public static void Throw( string errorMessage )
	{
		Sandbox.Internal.GlobalGameNamespace.Log.Error( errorMessage );
		throw new Exception( errorMessage );
	}

}
