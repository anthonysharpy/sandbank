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
}
