Are you using Sandbank in your project? If so, let me know and I'll advertise your project here! 😃

# Sandbank

Sandbank is the fast, easy-to-use no-SQL database for s&box.

Sandbank is a local data store and does not save anything in the cloud. If you're looking for a service that lets you host data, call endpoints and do other server-related logic, we have a separate service called Sandbank Server that you can [read about here](SandbankServer.md).

# Installation

### Via the package manager

In the editor, go to `View -> Library Manager` and find Sandbank Database. You might have to restart s&box.

### Directly from source

Alternatively you can get the latest version of the source code from https://github.com/anthonysharpy/sandbank. This can be put directly in your source code, or wherever else you want to put it.

# Usage and features

### Basic introduction

The database uses the document model. This means that data is saved as JSON files. It also means that there is no need to create SQL queries or do joins or anything like that.

A "document" is just a class containing some data. For example, data for a specific player.

Each document belongs to a "collection". Every collection contains many documents. Most databases will have multiple collections. For example, you might have a "players" collection for player data, and a "houses" collection for players' houses, etc.

Data files are saved in s&box's data folder. For example:

`C:\Program Files (x86)\Steam\steamapps\common\sbox\data\my_organisation\my_project\sandbank`.

The basics you need to know:
- After the first time running, the database automatically creates a `sandbank_config.ini` file. Here you can configure the database and change some performance-related settings (the default settings will work very well for 95% of people).

- The data you want to save must be in a _**class**_. Structs are not supported. Structs are supported when used inside a class, though.

- You can't use different class types with the same collection. It's one class per collection.

- Any data you want saved must be a public property with the `[Saved]` or `[AutoSaved]` attribute. If you put this on something like a class or a `List` of classes, all public properties in those classes will get saved. If you don't want this, you can add the `[JsonIgnore]` attribute probably to the properties you don't want saved (I will probably add more control over this later).

- Every document _**must**_ have a _**public string property**_ called "UID" (unique ID). This is the _**unique**_ primary ID of the document and is also used as the document's file name. You can set this to be whatever you want. For example, you might want it to be a player's Steam ID. Alternatively, you can leave it empty, and the database will automatically populate the ID for you as a random GUID.

- When your server is ***switched-off***, you can easily edit your data just by editing the files.

### Saving your data

There are two ways to save data with Sandbank.

The first way is the convenient but potentially less performant way. You attach the `[AutoSaved]` attribute to each property you want to save. In the attribute you must specify the name of the collection you want the class to be saved in (e.g. `[AutoSaved("players")]`). Whenever that property is updated, the data is saved to file automatically. The reason this is slower is because if the property is updated often, there can be an excessive amount of inserts. ***Note that `[AutoSaved]` will not do anything if the UID is empty (manually saving the record will populate the UID automatically, or you can populate it yourself)***.

The second way is the less convenient but more performant way. You attach the `[Saved]` attribute to your property. You then have to manually insert the data into the database in order to actually save it. You can do this straight away, or if you want to maximise performance, you might save all your data every few seconds in a background loop.

In practice, unless you are making a game with lots of players or lots of things that need to be saved, `[AutoSaved]` will probably be fine for you.

Here is an example of tagging data using both `[Saved]` and `[AutoSaved]`:

```
// Note how this is also a component. This is the recommended way to do it.
// If you store your data in a component you can sync it over the network as
// well as save it.

class PlayerData : Component
{
	[Saved] public string UID { get; set; }
	[Saved, Sync] public float Health { get; set; }
	[AutoSaved("players"), Sync] public string Name { get; set; }
	[Saved, Sync] public int Level { get; set; }
	[Saved] public DateTime LastPlayTime { get; set; }
	[Saved, Sync] public List<string> Items { get; set; } = new();
}
```

### Inserting, querying and deleting
```
using SandbankDatabase;

private PlayerData _myPlayerData = new();

public void SaveData()
{
	Log.Info($"My ID is empty: {_myPlayerData.UID}");

	_myPlayerData.Health = 100;
	_myPlayerData.Name = "Bob";

	// Insert the player. Their ID is populated from within the function because the
	// class is passed by reference.
	Sandbank.Insert("players", _myPlayerData);

	Log.Info($"My ID is now populated: {_myPlayerData.UID}");

	var playerWith100Health = Sandbank.SelectOne<PlayerData>("players", x => x.Health == 100);

	Log.Info($"The player with 100 health is: {playerWith100Health.Name}"); // "Bob".

	Sandbank.DeleteWithID<PlayerData>("players", playerWith100Health.UID);
}
```

### Using the data

If you fetch data from the database and want to put it in a component in the scene or something like that, you can either copy each field yourself, or use the helper method `CopySavedData`, which will copy all `[Saved]` and `[AutoSaved]` public properties:

```
var player = GetOurPlayer(); // Get the player in the scene.
var ourPlayerData = Sandbank.SelectOneWithID<PlayerData>("players", "123");
Sandbank.CopySavedData<PlayerData>(ourPlayerData, player.Data);
```

### Slow queries

A well-designed query should return instantly.

However, if you're doing something hardcore, you should consider wrapping the call in its own async task or thread:

```
public bool Something()
{
	// Run as background task.
	DoSomething();
}

async void DoSomething()
{
	var houses = Sandbank.Select<House>( "houses", x => x.OwnerName == "Steve" );

	// Do something with this data.
}
```

```
// Run as background thread.
GameTask.RunInThreadAsync( () => {
	var houses = Sandbank.Select<House>("houses", x => x.OwnerName == "Steve");

	// Do something with this data.
});
```

### Renaming properties

If you rename the properties in your data class, the data is not lost. For example, if you renamed a property called `Name` to `PlayerName`, the `Name` property will still be saved on file, and `PlayerName` will be created alongside it.

This is good because it means you can't accidentally delete all your data. It's also bad because it makes it harder to remove old data from your database.

In order to get around this, first start the database and migrate your data:

```
playerData.PlayerName = playerData.Name;
Sandbank.Insert<PlayerData>("players", playerData);
```

Next, check the changes were applied and _**make a backup of your data**_. Make sure you don't have any other unused fields as they will get deleted next.

Lastly, set `MERGE_JSON` in `sandbank_config.ini` to false. Then, start up the database again and wait a few seconds for the changes to take effect. The renamed data should be gone now.

You must then set `MERGE_JSON` back to true, or it will spam warnings at you.

### Saving on the client or the server

Sandbank supports saving data on the client or server (or both). It just depends on where you call the code from. By default, saving on the client is not allowed, but this can be enabled in `sandbank_config.ini`.

### Backups

Sandbank supports backups and these are enabled by default. Backup settings can be configured in `sandbank_config.ini`.

### File obfuscation

Sandbank supports file obfuscation, which can be enabled in `sandbank_config.ini`. This makes the saved data files unreadable and uneditable. This is useful if you are saving data on the client and don't want them to be able to see or change it.

The obfuscation can be reverse-engineered, so it does not prevent data editing. But it makes it so that 99.9% of people will not bother.

You can disable or enable obfuscation at any time and it will still work. Note that once a file is obfuscated, it can only become unobfuscated after it has been saved again.

Obfuscation has a slight impact on performance.

# Performance

### CPU

Sandbank is designed to be thread-safe, letting you squeeze more out of it. 

Sandbank creates a copy of itself in program memory, so for most use-cases it is probably faster than a conventional database, unless you have hundreds of thousands of records, and you know how to index them efficiently.

Here are some benchmarks using the above PlayerData class on a Ryzen 5 5500 with 12 logical processors:

| Operation                                                                                  | Total Time    | Speed                             | Notes                  |
|--------------------------------------------------------------------------------------------|---------------|-----------------------------------|------------------------|
| 100,800 inserts (one task) | 0.475 seconds | 212,000 documents inserted/second | In reality this is probably faster than your disk could keep up with anyway. |
| 100,800 inserts (24 tasks) | 0.114 seconds | 884,000 documents inserted/second | |
| Search 100,800 documents [x => x.Health >= 90] (one task) | 0.044 seconds | 2,290,000 documents searched/second | ~10,080 records being returned here. |
| Search 2,419,200 documents [x => x.Health >= 90] (24 tasks) | 0.188 seconds | 12,868,000 documents searched/second | ~10,080 records being returned here per task. |
| Search 2,419,200 documents [x => x.Health == 100] (24 tasks) | 0.073 seconds | 33,140,000 documents searched/second | ~1,008 records being returned here per task, hence much faster due to less memory copying. This is probably the more realistic scenario. |
| Search 100,800 documents [x => x.Health >= 90] (one task, unsafe references) | 0.0182 seconds | 5,538,000 documents searched/second |  ~10,080 records being returned here. |
| Search 2,419,200 documents [x => x.Health >= 90] (24 tasks, unsafe references) | 0.071 seconds | 34,073,000 documents searched/second |  ~10,080 records being returned here per task. |
| Search 100,800 documents by ID 100,000 times (one task) | 0.124 seconds | 806,000 lookups/second | 1 document returned. |
| Search 100,800 documents by ID 2,400,000 times (24 tasks) | 0.55 seconds | 4,364,000 lookups/second | 1 document returned. |

The above figures represent the time it took to write/read the data to/from the cache only (not to disk). As you can see, searching by ID is basically instant, inserts are very quick, and regular searches are relatively quick. These benchmarks used an optimal pool size of around 200,000 (about 240mb worth of extra memory).

The speed of searching the database will depend heavily on:
- The size of your collection and documents
- The complexity of your query
- The number of documents returned

### Memory

The database stores all data in memory in a cache. 10,000 of the above PlayerData classes only take up around 12mb memory. Unless you're handling millions of documents, or your documents are very big, you don't really need to worry about memory.

### Disk

The disk space used is less than the amount of memory used. Changes to the cache are written slowly to the disk over time in the background. Under extreme loads (thousands of documents being inserted per second) this may throttle your hard-drive a little, but it shouldn't impact performance too much.

# Data consistency

Data is written to disk slowly over time. The frequency at which this is done, as well as a number of other things, is configurable in `sandbank_config.ini`. By default, the database aims to write any change to disk in under 10 seconds.

Sandbank attempts to shut itself down gracefully in the background when the game stops, saving all remaining data. However, it is still recommended to call `Shutdown()` before an anticipated server shutdown to ensure that the database is terminated properly. If the server crashes or if the server process is suddenly terminated, any data that is not written to disk by that point will probably be lost.

# Contributions

Contributions are more than welcome. Also, feel free to ask questions or raise issues on the GitHub page: https://github.com/anthonysharpy/sandbank. If you do want to contribute something, it's probably a good idea to raise an issue first.

Please note that the project is not entirely open-source and there are some very minor restrictions around what you can do (such as creating other versions of the software). Please read the licence or ask if you are unsure.

# Learn More

- [Database repair guide](RepairGuide.md)
- [Advanced optimisation guide](OptimisationGuide.md)
