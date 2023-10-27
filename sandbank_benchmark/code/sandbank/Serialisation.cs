using System;
using System.Text.Json;

namespace NSSandbank;

internal class Serialisation
{
	private static JsonSerializerOptions _jsonOptions = new()
	{
		ReadCommentHandling = JsonCommentHandling.Skip,
		WriteIndented = Config.INDENT_JSON
	};

	/// <summary>
	/// Re-build the JSON options object (we need to do this after
	/// programatically changing any of the config options that control
	/// JSON serialisation).
	/// </summary>
	public static void UpdateJSONOptions()
	{
		_jsonOptions = new()
		{
			ReadCommentHandling = JsonCommentHandling.Skip,
			WriteIndented = Config.INDENT_JSON
		};
	}

	public static string SerialiseClass<T>( T theClass )
	{
		return JsonSerializer.Serialize( theClass, _jsonOptions );
	}

	public static T DeserialiseClass<T>( string data )
	{
		return JsonSerializer.Deserialize<T>( data, _jsonOptions );
	}

	public static string SerialiseClass( object theClass, Type classType )
	{
		return JsonSerializer.Serialize( theClass, classType, _jsonOptions );
	}

	public static object DeserialiseClass( string data, Type type )
	{
		return JsonSerializer.Deserialize( data, type, _jsonOptions );
	}

	public static T CloneObject<T>( T theObject )
	{
		return Serialisation.DeserialiseClass<T>( Serialisation.SerialiseClass<T>( theObject ) );
	}

	public static object CloneObject( object theObject, Type objectType )
	{
		var json = Serialisation.SerialiseClass( theObject, objectType );
		return Serialisation.DeserialiseClass( json, objectType );
	}
}
