using Sandbox;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SandbankDatabase;

public static class SBServer
{
	private static JsonSerializerOptions _jsonOptions = new()
	{
		PropertyNameCaseInsensitive = true
	};

	/// <summary>
	/// Calls an endpoint that returns data. The response is serialised as the given type T.
	/// <br/><br/>
	/// dataObject is an optional data class for the request that will be serialised into JSON.
	/// </summary>
	public static async Task<T> CallEndpoint<T>( string endpointName, object dataObject = null ) where T : class
	{
		var json = dataObject != null ? JsonSerializer.Serialize( dataObject ) : null;
		var requestContent = BuildRequestContent( endpointName, json );
		var response = await SendRequest( requestContent );
		HandleResponseType( endpointName, response );

		return await ProcessDataResponse<T>( response, endpointName );
	}

	/// <summary>
	/// Calls an endpoint that does not return data.
	/// <br/><br/>
	/// dataObject is an optional data class for the request that will be serialised into JSON.
	/// </summary>
	public static async Task CallEndpoint( string endpointName, object dataObject = null )
	{
		var json = dataObject != null ? JsonSerializer.Serialize( dataObject ) : null;
		var requestContent = BuildRequestContent( endpointName, json );
		var response = await SendRequest( requestContent );
		HandleResponseType( endpointName, response );
	}

	private static async Task<HttpResponseMessage> SendRequest( StringContent requestContent )
	{
		return await Http.RequestAsync( "https://sandbankdatabase.com/endpoint", "POST", requestContent );
	}

	private static StringContent BuildRequestContent( string endpointName, string jsonData )
	{
		jsonData ??= "null";

		var message = "{" +
			"\"userID\":\"" + Config.SBSERVER_USER_ID + "\"," +
			"\"publicKey\":\"" + Config.SBSERVER_PUBLIC_KEY + "\"," +
			"\"endpoint\":\"" + endpointName + "\"," +
			"\"data\":" + jsonData + "" +
		"}";

		return new StringContent( message, Encoding.UTF8, "application/json" );
	}

	private async static Task<T> ProcessDataResponse<T>( HttpResponseMessage response, string endpointName ) where T : class
	{
		if ( !response.IsSuccessStatusCode )
			return null;
		
		var responseData = await response.Content.ReadAsStringAsync();

		try
		{
			return JsonSerializer.Deserialize<T>( responseData, _jsonOptions );
		}
		catch (	Exception e )
		{
			throw new SandbankException( $"Sandbank Server: failed deserialising JSON response from server for endpoint " +
				$"{endpointName} - either your response type is wrong or there is a bug on the server: {e.Message} " +
				$"... {e.InnerException}");
		}
	}

	private static void HandleResponseType( string endpointName, HttpResponseMessage response )
	{
		if ( Config.ON_ENDPOINT_ERROR_BEHAVIOUR == OnEndpointErrorBehaviour.DoNothing )
			return;

		if ( response.IsSuccessStatusCode )
			return;
		else if ( response.StatusCode == System.Net.HttpStatusCode.TooManyRequests )
			Logging.Warn( $"failed calling endpoint {endpointName} - you have reached your rate limit" );
		else if ( response.StatusCode == System.Net.HttpStatusCode.InternalServerError )
			Logging.Warn( $"failed calling endpoint {endpointName} - internal server error (this is a bug)" );
		else if ( response.StatusCode == System.Net.HttpStatusCode.Forbidden )
			Logging.Warn( $"failed calling endpoint {endpointName} - forbidden (are your credentials correct?)" );
		else if ( response.StatusCode == System.Net.HttpStatusCode.BadRequest )
			Logging.Warn( $"failed calling endpoint {endpointName} - bad request (is your endpoint/request correct?)" );
		else
			Logging.Warn( $"failed calling endpoint {endpointName} - there was an unknown error (response code {response.StatusCode})" );
	}
}
