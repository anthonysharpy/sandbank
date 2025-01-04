Code samples for Sandbank.

Something you think could be added here? Please make an issue.

## Load an existing player record, or create a new record if one doesn't exist

A player has joined. Try and load their data, or create a new record for them if they have no data.

```
// Example player class.
// Note how this is also a component. This is the recommended way to do it.
// If you store your data in a component you can sync it over the network as
// well as save it.
public class MyPlayer : Component
{
	[AutoSaved("players")] public string UID { get; set; }
	[Sync, AutoSaved("players")] public string Name { get; set; }
	[Sync, AutoSaved("players")] public float Health { get; set; }
}

public Player CreatePlayer()
{
	// This assumes you have a player prefab which has a player component.
	// You don't have to do this, this is just an example.
	var playerObject = PlayerPrefab.Clone();
	return playerObject.Components.Get<MyPlayer>();
}

// Called when a player joins.
public void OnPlayerJoined( Connection connection )
{
	// Create the actual player object in the scene.
	var player = CreatePlayer();

	// Try and fetch their data (saved against their Steam ID).
	var playerData = Sandbank.SelectOneWithID<MyPlayer>( "players", connection.SteamId.ToString() );

	if ( playerData == null )
	{
		// They have no data. Set-up a fresh copy.
		// Because these are [AutoSaved], the data will be saved automatically once we have
		// populated the UID. 
		player.Data.UID = connection.SteamId.ToString();
		player.Data.Name = connection.DisplayName;
		player.Data.Health = 100;
	}
	else
	{
		// They have previously saved data. Load it.
		Sandbank.CopySavedData<MyPlayer>( playerData, player );
	}
}
```

If you wanted to use `[Saved]` instead of `[AutoSaved]`, the code is the same, except you would have to manually call `Sandbank.Insert<>()`.

## Search for a record

```
var playerWith100Health = Sandbank.SelectOne<MyPlayer>("players", x => x.Health == 100);
```

## Delete a record

```
Sandbank.DeleteWithID<MyPlayer>("players", myPlayer.UID);
```