# Sandbank

Sandbank is the fast, easy-to-use no-SQL database for s&amp;box. Installation is click-and-drag, with zero setup required.

## Installation

Simply download the repository and drag the `sandbank` folder into your codebase. `sandbank_benchmark` contains tests - you don't want this.

## Usage

The database uses the document model. This means that data is saved as JSON files. It also means that there is no need to create SQL queries or do joins or anything like that. Each document belongs to a "collection", which contain many documents. Most databases will have multiple collections. Most of the time, each collection will represent a separate data type. For example, you might have a "players" collection for player data, and a "bases" collection for player bases, etc.

Data files are saved in s&box's data folder. For example:

`C:\Program Files (x86)\Steam\steamapps\common\sbox\data\my_organisation\my_project\sandbank`.

Some things to bear in mind:
- Every document you insert _**must**_ have a public string property called "ID" with a _**non-null default value**_. This is the _**unique**_ primary ID of the document and is also used as the document's file name. You can set this to be whatever you want. For example, you might want it to be the player's Steam ID. Alternatively, you can leave it empty, and the database will automatically populate the ID for you as a random GUID.
- Your data must be in a class. A struct won't work.
- Any data you want saved must be a public property. Hide any properties you don't want saved with the `[JsonIgnore]` attribute.
- Almost all types are supported - even custom ones.

When your server is switched-off, you can easily edit your data just by editing the files.

An example:

```
class PlayerData
{
	public string ID { get; set; } = ""; // Note the use of "" - a non-null value is required.
	public float Health { get; set; }
	public string Name { get; set; }
	public int Level { get; set; }
	public DateTime LastPlayTime { get; set; }
	public List<string> Items { get; set; } = new();
}
```

```
private PlayerData _myPlayerData = new();

public void SaveData()
{
	Log.Info($"My ID is empty: {_myPlayerData.ID}");

	_myPlayerData.Health = 100;
	_myPlayerData.Name = "Bob";

	// Insert the player. Their ID is populated from within the function because the
	// class is passed by reference.
	Sandbank.Insert<PlayerData>("players", _myPlayerData);

	Log.Info($"My ID is now populated: {_myPlayerData.ID}");

	var playerWith100Health = Sandbank.Select<PlayerData>("players", x => x.Health == 100);

	Log.Info($"The player with 100 health was: {playerWith100Health.Name}"); // "Bob".

	Sandbank.DeleteWithID<PlayerData>("players", playerWith100Health.ID);
}
```

## Performance

### CPU

Sandbank is designed to be thread-safe so that you can squeeze more out of it. In fact, since it resides in memory, for most use-cases it is probably faster than a conventional database, unless your alternative is an efficiently-indexed table with hundreds of thousands of records. Here are some benchmarks using the above PlayerData class on a Ryzen 5 5500 with 12 logical processors:

| Operation                                                                                  | Total Time    | Speed                             | Notes                  |
|--------------------------------------------------------------------------------------------|---------------|-----------------------------------|------------------------|
| 100,800 inserts (one thread)                                                               | 0.6896 seconds  | 146,000 documents inserted/second | In reality this is probably faster than your hard-drive could keep up with.                   |
| 100,800 inserts (24 threads)                                                               | 0.1662 seconds  | 606,000 documents inserted/second |                    |
| Search 100,800 documents [x => x.Health >= 90] (once on one thread)                        | 0.0598 seconds  | 1,686,000 documents searched/second |                     |
| Search 100,800 documents [x => x.Health >= 90] (24 times on 24 threads)                    | 0.5473 seconds  | 4,420,000 documents searched/second | ~10,080 records being returned here. |
| Search 100,800 documents [x => x.Health == 100] (24 times on 24 threads)                    | 0.1125 seconds  | 21,504,000 documents searched/second | ~1,008 records being returned here, hence much faster due to less memory copying. |
| Search 100,800 documents [x => x.Health >= 90] (once on one thread, unsafe references)     | 0.0148 seconds | 6,811,000 documents searched/second |                    |
| Search 100,800 documents [x => x.Health >= 90] (24 times on 24 threads, unsafe references) | 0.0587 seconds  | 41,213,000 documents searched/second |                    |
| Search 100,800 documents by ID (100,000 times on one thread)                               | 0.3013 seconds  | 33,455,028,000 documents searched/second |                    |
| Search 100,800 documents by ID (100,000 times on 24 threads)                               | 1.9953 seconds  | 121,244,926,000 documents searched/second |                    |

The above figures represent the time it took to write/read the data to/from the cache only (not to disk). As you can see, searching by ID is basically instant, inserts are very quick, and regular searches are relatively quick.

The speed of searching the database will depend heavily on:
- The size of your collection and documents
- The complexity of your query
- The number of documents returned (unless you're using unsafe references mode, in which case it won't matter that much, since there is no memory copying)

(Very) generally speaking, in a no-SQL database, you should be avoiding doing queries against the database as much as possible. Instead, lookup documents by their ID (which is instant). Bunch related data into the same document to increase ease-of-access. Don't strictly normalise your data.

### Memory

The database stores all data in memory in a cache. 100,000 of the above PlayerData classes only take up around 30mb memory. Unless you're handling millions of documents, or your documents are very big, you don't really need to worry about memory.

### Disk

The disk space used is less than the amount of memory used. Changes to the cache are written slowly to the disk over time in a background thread. Under extreme loads (thousands of documents being inserted per second) this may throttle your hard-drive a little, but it shouldn't impact performance too much.

## Consistency and Safety

Data is written to disk slowly over time. The frequency at which this is done is configurable in `Config.cs`, although I'd recommend leaving that alone unless you understand what you're doing. By default, the database aims to write a change to disk in under 10 seconds. Any data that is not written to disk is lost on a crash or server restart, but you can call `ForceWriteCache()` before an anticipated server shutdown to force-write all data to disk. However, if you're fine with potentially losing the last few seconds of changes, then you don't have to.

### Unsafe References Mode

The `Select` query has an unsafe counterpart `SelectUnsafeReferences`. The primary difference between the two is that the second one is about 9x faster. However, there is a crucial difference that you _**MUST**_ be aware of if you use `SelectUnsafeReferences`, or _**YOU RISK CORRUPTING YOUR DATA**_. `Select` copies the data from the cache into new objects and then gives those new objects to you. That means that any changes you make to those objects don't affect anything else - you're free to do what you want with them. The downside to this is that there is an overhead invovled in creating all those new objects. `SelectUnsafeReferences` on the other hand will give you a reference to the data that is stored in the cache. This is faster because it means no new copy has to be made. However, because it's giving you a reference, this means that _**ANY CHANGES YOU MAKE TO THESE RETURNED OBJECTS WILL BE REFLECTED IN THE CACHE, AND THEREFORE MAY CHANGE THE VALUES IN THE DATABASE UNEXEPECTEDLY!!!**_ You should therefore not modify these returned objects in any way, only read them. You are guaranteed that the cache will not change the object after you have requested it (because all inserts are new objects). To summarise, if you don't fully understand what I've just said, then stick with `Select`, as it is still quite fast. 


