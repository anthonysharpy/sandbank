using System;

namespace SandbankDatabase;

public class SandbankException : Exception
{
	public SandbankException( string message ) : base( message )
	{
	}
}
