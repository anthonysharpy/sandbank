using System;
using Sandbox;
using Sandbox.Internal;

namespace NSSandbank;

class Document
{
	/// <summary>
	/// This is also stored embedded in the Data object, but we keep it
	/// here as an easily-accessible copy for convenience.
	/// </summary>
	public string ID { get; private set; }
	public object Data { get; private set; }
	public bool Stale;

	public Document( object data, Type documentType, bool needsCloning )
	{
		object id = GlobalGameNamespace.TypeLibrary.GetPropertyValue( data, "ID" );

		if ( id is null || id is not string )
			Logging.Throw( "cannot handle a document that has no valid \"ID\" property - make sure your data class has a public property called ID, and that it is initialised to a non-null value, like this: \"public string ID {get; set;} = \"\";\"" );

		if ( ((string)id).Length > 0 )
		{
			ID = (string)id;
		}
		else
		{
			ID = Guid.NewGuid().ToString().Replace( "-", "" );

			// We DO want to modify the ID of the passed-in reference.
			GlobalGameNamespace.TypeLibrary.SetProperty( data, "ID", ID );
		}

		// We want to avoid modifying a passed-in reference, so we clone it.
		// But this is redundant in some cases, in which case we don't do it.
		if ( needsCloning )
			data = Serialisation.CloneObject( data, documentType );

		Data = data;
		Stale = true;
	}
}
