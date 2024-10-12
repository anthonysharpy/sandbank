using Sandbox;
using Sandbox.Internal;
using System;
using System.Linq;

namespace SandbankDatabase;

/// <summary>
/// Add this attribute to a property to allow it to be saved to file. When the property
/// is modified, the whole class will be saved to file. Nothing will happen if the UID is not set.
/// 
/// While this is the most convenient approach, as this will save the document every time data
/// is changed, this will generally perform worse than [Saved].
/// </summary>
[AttributeUsage( AttributeTargets.Property )]
[CodeGenerator(CodeGeneratorFlags.WrapPropertySet | CodeGeneratorFlags.Instance, "SandbankDatabase.SandbankAutoSavedEventHandler.AutoSave" )]
public class AutoSaved : Attribute
{
	public string CollectionName { get; set; }

	public AutoSaved( string collectionName )
	{
		CollectionName = collectionName;
	}
}

public static class SandbankAutoSavedEventHandler
{
	private static object _autoSaveLock = new();
	private static object _objectBeingAutoSaved = null;

	public static void WipeStaticFields()
	{
		_objectBeingAutoSaved = null;
	}

	public static void AutoSave<T>( WrappedPropertySet<T> p )
	{
		p.Setter( p.Value );

		// Don't auto-save while we are initialising. It is pointless.
		if ( !Sandbank.IsInitialised )
			return;

		string id = (string)GlobalGameNamespace.TypeLibrary.GetPropertyValue( p.Object, "UID" );

		// If the UID is not set then we can assume this document hasn't even been fully created yet.
		if ( string.IsNullOrEmpty( id ) )
			return;

		lock ( _autoSaveLock )
		{
			// When we save this in a moment, the cache will create a copy of it. This will basically send it right
			// back here. So to avoid an infinite loop, don't auto save if we're already auto saving an object.
			//
			// This means that only one object can be auto saved at a time, but in practice this isn't really a big
			// deal since a) 95% of users won't be saving things in multiple threads and b) auto save is meant for
			// people who don't care about performance. Also, creating a system for locking each object could just
			// end up adding more latency than it's worth.
			if ( _objectBeingAutoSaved != null )
				return;
			
			try
			{
				_objectBeingAutoSaved = p.Object;

				var collectionName = (string)GlobalGameNamespace.TypeLibrary.GetPropertyValue(
					p.Attributes.First( x => x.GetType().ToString() == "SandbankDatabase.AutoSaved" ),
					"CollectionName" );

				Sandbank.Insert( collectionName, p.Object, p.Object.GetType() );
			}
			finally
			{
				_objectBeingAutoSaved = null;
			}
		}
	}
}
