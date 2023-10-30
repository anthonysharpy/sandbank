# Sandbank

Sandbank is the fast, easy-to-use no-SQL database for s&amp;box. Installation is click-and-drag, with zero setup required.

## Installation

Simply download the repository and drag the `sandbank` folder into your codebase. `sandbank_benchmark` contains tests - you don't need this.

## Usage

The database uses the document model. This means that data is saved as JSON files and that there is no need to create SQL queries or do joins or anything like that. Each document belongs to a collection. Most databases will have multiple collections. This isn't strictly enforced, but 98% of the time you will want a separate collection for each data type. For example, you might have a "players" collection for player data, and a "highscores" collection for highscores, etc.

Data files are saved in s&box's data folder - e.g. `C:\Program Files (x86)\Steam\steamapps\common\sbox\data\my_organisation\my_project\sandbank`.

Some things to bear in mind:
- Every document you insert _**must**_ have a public property called "ID" with a _**non-null default value**_. This is the _**unique**_ primary ID of the document and is also used as the document's file name. You can set this to be whatever you want - for example, you might want it to be a player's Steam ID. Alternatively, you can leave it empty, and the database will automatically populate the ID for you as a random GUID.
- Your data must be in a class. A struct won't work.
- Any data you want saved must be a public property. Hide any properties you don't want saved with the `[JsonIgnore]` attribute.
- Most types are supported - even custom ones.

An example:

```
// In reality you would probably make this a BaseNetworkable or something, assuming
// you wanted to network it.
class PlayerData
{
	public string ID { get; set; } = ""; // Note the use of "" - a non-null value is required.
	public float Health { get; set; } = 100;
	public string Name { get; set; } = "Bob";
	public int Level { get; set; }
	public DateTime LastPlayTime { get; set; }
	public List<string> Items { get; set; } = new();
}

...

private PlayerData _myPlayerData = new();

public void SaveData()
{
	Log.Info($"My ID is empty: {_myPlayerData.ID}");

	// Insert the player. Their ID is populated from within the function because the
	// class is passed by reference.
	Sandbank.Insert<PlayerData>("players", _myPlayerData);

	Log.Info($"My ID is now populated: {_myPlayerData.ID}");

	var playerWith100Health = Sandbank.Select<PlayerData>("players", x => x.Health == 100);

	Log.Info($"The player with 100 health was: {playerWith100Health.Name}");

	Sandbank.DeleteWithID<PlayerData>("players", playerWith100Health.ID);
}
```

## Performance

### CPU

Sandbank is designed to be thread-safe so that you can squeeze more out of it. In fact, since there is no network overhead, it is probably faster than a conventional database, unless you're talking about an efficiently-indexed table with hundreds of thousands of records. Here are some benchmarks using the above PlayerData class on a Ryzen 5 5500 with 12 logical processors:

| Operation                                                                                  | Total Time    | Speed   |
|--------------------------------------------------------------------------------------------|---------------|------------------|
| 100,800 inserts (one thread)                                                               | 0.70 seconds  | 144,000 documents inserted/second |
| 100,800 inserts (24 threads)                                                               | 0.18 seconds  | 560,000 documents inserted/second |
| Search 100,800 documents [x => x.Health >= 90] (once on one thread)                        | 0.07 seconds  | 1,439,999 documents searched/second |
| Search 100,800 documents [x => x.Health >= 90] (24 times on 24 threads)                    | 0.54 seconds  | 4,480,000 documents searched/second |
| Search 100,800 documents [x => x.Health >= 90] (once on one thread, unsafe references)     | 0.015 seconds | 6,720,000 documents searched/second |
| Search 100,800 documents [x => x.Health >= 90] (24 times on 24 threads, unsafe references) | 0.06 seconds  | 40,320,000 documents searched/second |
| Search 100,800 documents by ID (100,000 times on one thread)                               | 0.26 seconds  | 38,769,230,769 documents searched/second |
| Search 100,800 documents by ID (100,000 times on 24 threads)                               | 1.94 seconds  | 124,701,030,921 documents searched/second |


The above figures represent the time it took to write/read the data to/from the cache only (not to disk). As you can see, searching by ID is basically instant, inserts are very quick, and regular searches are relatively quick.

The speed of regular searches will depend heavily on:
- The size of your collection/documents
- The complexity of your query
- The number of documents returned (unless you're using unsafe references mode, in which case it won't matter that much).

### Memory

The database stores all data in memory in a cache. You may think that'd be quite bad for memory consumption, but actually, even 100,000 of the above PlayerData classes only take up about 30mb memory (a gross oversimplification, but you get the point). Unless you're handling millions of documents, or your documents are very big, you don't really need to worry.

### Disk

The disk space used is less than the amount of memory used. Changes to the cache are written slowly to the disk over time in a background thread. Under extreme loads (i.e. thousands of documents being inserted per second) this may throttle your hard-drive a little, but it shouldn't impact performance too much. Using a HDD though is not recommended.

## Consistency and Safety

Data is written to disk slowly over time. The frequency at which this is done is configurable in `Config.cs`, although I'd recommend leaving that alone unless you understand what you're doing. Any data that is not written to disk is lost on a crash or server restart, but you can call `ForceWriteCache()` before an anticipated server shutdown to force-write all data to disk. However, if you're fine with potentially losing the last few seconds of changes, then you don't have to.

Transactions are not currently supported but if this is something that you would find useful then please make an issue or let me know :-)

With all the concurrency support this got quite complicated so please raise an issue if you encounter any bugs. Contributions are welcome too.

### Unsafe References Mode

The `Select` query has an unsafe counterpart `SelectUnsafeReferences`. The primary difference between the two is that the second one is about 9x faster. However, there is a crucial difference that you _**MUST**_ be aware of if you use `SelectUnsafeReferences`, or _**YOU RISK CORRUPTING YOUR DATA**_. `Select` copies the data from the cache into new objects and then gives those new objects to you. That means that any changes you make to these objects don't affect anything else - you're free to do what you want with them. The downside to this is that there is an overhead invovled in creating all those new objects. `SelectUnsafeReferences` on the other hand will give you the exact copy of the data that is stored in the cache. This is faster because it means no new copy has to be made. However, because it's giving you a class, this is a reference type. This means that _**ANY CHANGES YOU MAKE TO THIS RETURNED VALUE WILL BE REFLECTED IN THE CACHE, AND THEREFORE MAY CHANGE THE VALUES IN THE DATABASE UNEXEPECTEDLY!!!**_ You should therefore not modify the returned values in any way, only read them. You are guaranteed however that the cache will not change the object after you have requested it (because all inserts are new objects). To summarise, if you don't fully understand what I've just said, then stick with `Select`, as it is still quite fast. 


