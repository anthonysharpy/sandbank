# About Sandbank Server

### What is Sandbank Server?

Sandbank server is an online service that lets you call endpoints to save and load data as well as run serverside logic.

# Setup

Setup on the service is currently done manually.

First of all, contact me on Discord (anthonysharpy) to discuss what server-side logic you need 🤓

Following that it will probably take me a few weeks to implement the logic you want.

After that you will receive your public key, private key and user ID which will allow you to access the service. You will also receive a list of all your endpoints as well as what data they expect and return.

# Usage

First of all, download the Sandbank library for s&box if you haven't already:

https://sbox.game/anthonysharpy/sandbank

Second, open up `Config.cs` in the Sandbank library code and put your credentials in there (`SBSERVER_USER_ID` and `SBSERVER_PUBLIC_KEY`). Or, if you are not shipping your public key with your game, you need to set these values some other way (e.g. loading your credentials from file and then setting `SBSERVER_USER_ID` and `SBSERVER_PUBLIC_KEY` manually).

Now, let's imagine you have an endpoint that takes a Steam ID and returns that player's information. 

First, you need to make classes to represent your request and response data (these will get serialised to JSON, so they need to contain public properties with `{ get; set; }`). The classes also need to contain the JSON fields you were given for each endpoint (don't worry, it's not case-sensitive).

For example:

```
public class PlayerInformationRequest
{
	public string SteamID { get; set; }
}

public class PlayerInformationResponse
{
	public string Name { get; set; }
	public int Health { get; set; }
	public float Money { get; set; }
}
```

For the request object (but not the response), you can also use an anonymous type instead of a class:

```
var response = await CallEndpoint<PlayerInformationResponse>( "test", new { 
	SteamID = "12345"
} );
```

After that, it's as simple as using the `CallEndpoint` method.

```
var response = await SBServer.CallEndpoint<PlayerInformationResponse>( "getPlayerInformation", new PlayerInformationRequest
{
    SteamID = "abc123"
} );

if ( response == null )
{
    Log.Info("Request failed");
    return;
}

Log.Info( "Their health is " + response.Health );
```

This calls the relevant endpoint on the Sandbank Server API and fetches the response (if any).

As you can see, if the request fails for some reason, the response will be null. By default, a warning will also be logged to the console explaining what went wrong, but you can configure this behaviour in `Config.cs`.

If your endpoint doesn't require or return any data, you'd simply do something like this:

```
await SBServer.CallEndpoint( "myEndpoint" );
```

### Public and private keys

Endpoints are accessed using your public key. Therefore, give your public key to anyone that you want to be able to access your endpoints.

Private keys are for doing developer-related stuff (like checking your usage etc). Don't share it with anyone you do not trust.

If you want your key to be changed for whatever reason then get in touch.

# FAQ

### How much does it cost?

Unless your requirements are complicated, it is currently free.

### Are there usage limits?

Yes. For free, you will get:

- 100MB storage
- 60 core compute seconds per hour

I have no idea if this is not enough or too much, so for free users this might be increased or decreased. If this happens though you will get advance warning (if you provide a contact email on sign-up).

Usage limits are per-key, so if you have multiple access keys then you are getting extra.

### What kinds of serverside logic can it run?

Basically anything you want. All endpoints are plain POST requests though, so things like websockets are currently not supported. But I will add anything there is enough demand for.

### Is data safe?

It's pretty safe:

- Each user gets their own database
- Endpoints can only be accessed using your key
- All data is sent over HTTPS

### Will other people be able to access my endpoints?

If you give other people your public key or include it in your game code, yes. If you don't, then no.

### Can I have multiple access keys, e.g. one for each game I make?

Sure.

### If I design my game using this service, how do I know you won't just turn it off randomly and break my game? How do I know it's going to be reliable?

It's a good point. My answer to that:

- Software development is my real-life job, so I don't want to create a bad reputation for myself by annoying everyone
- This looks great on my CV, so I have no reason to stop doing it
- It's fun for me
- This requires very little maintenance, and costs me very little to host, so there's no real reason to turn it off

If one day it gets to the point where I have lots of people using the service, and it becomes very expensive for me, but no one wants to pay for it, then yes, I would probably turn it off. But I would probably give you six months' notice or something like that.

# Terms of Service

- Absolutely nothing illegal please!
- Don't use the service to store highly sensitive personal information (e.g. addresses, payment information, things about sexual identity or political views, IP addresses etc) without discussing it first.
- We own the copyright to all code created by us (even if it was created for you). Usually though we are happy to  give you a copy of your code and you can use it however you want.