# Sandbank

Sandbank is the easy-to-use no-SQL database for s&amp;box. Installation is click-and-drag, with zero setup required.

## Installation

Simply download the repository and drag the `sandbank` folder into your codebase. `sandbank_benchmark` contains tests - you don't need this.

## Usage

The database uses the document model. This means that data is saved to disk as JSON files and that there is no need to create SQL queries or do joins or anything like that. Each document belongs to a collection, and most databases will have multiple collections. This isn't strictly enforced, but 98% of the time you will want a separate collection for each class type. 

Data files are saved in s&box's data folder - e.g. `C:\Program Files (x86)\Steam\steamapps\common\sbox\data\my_organisation\my_project\sandbank`.

Some things to bear in mind:
- Due partially to some quirks of s&box, every document you insert _**must**_ have a public property called "ID" with a _**non-null default value**_. This is the unique primary ID of the document and is also used as the document's file name. You can set this to be whatever you want - for example, you might want it to be the player's Steam ID. Alternatively, you can leave it empty, and the database will automatically populate the ID for you as a random GUID.
- Your data must be in a class. A struct won't work.
- Any data you want saved must be a public property.
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

Sandbank is designed to be threadsafe so you can squeeze more out of it. In fact, since there is no network overhead, it is probably faster than a conventional database, unless you're talking about an efficiently-indexed table with hundreds of thousands of records. Here are some benchmarks using the above PlayerData class on a Ryzen 5 5500 with 12 logical processors:

| Operation                                                                         | Total Time    | Speed   |
|-----------------------------------------------------------------------------------|---------------|------------------|
| 100,800 inserts (one thread)                                                      | 0.70 seconds  | 144,000 documents inserted/second |
| 100,800 inserts (24 threads)                                                      | 0.18 seconds  | 560,000 documents inserted/second |
| Search 100,800 documents [x => x.Health >= 90] (once on one thread)               | 0.07 seconds  | 1,439,999 documents searched/second |
| Search 100,800 documents [x => x.Health >= 90] (24 times on 24 threads)           | 0.54 seconds  | 4,480,000 documents searched/second |
| Search 100,800 documents by ID (100,000 times on one thread)                      | 0.26 seconds  | 38,769,230,769 documents searched/second |
| Search 100,800 documents by ID (100,000 times on 24 threads)                      | 1.94 seconds  | 124,701,030,921 documents searched/second |

The above figures represent the time it took to write/read the data to/from the cache only (not to disk). As you can see, searching by ID is basically instant, inserts are crazy-quick, and regular searches are relatively quick. The speed of regular searches will depend heavily on the size of your collection and the complexity of your query however.

### Memory

The database stores all data in memory in a cache. You may think that'd be bad for memory consumption but actually even 100,000 of those PlayerData classes only take up about 20mb memory. Unless you're handling millions of documents, or your documents are very big, you don't really need to worry.

### Disk

The disk space used is less than the amount of memory used. Changes to the cache are written slowly to the disk over time in a background thread. Under extreme loads (i.e. hundreds/thousands of documents being written to disk per second) this may throttle your hard-drive a little, but it shouldn't impact performance too much. Using a HDD though is not recommended.

## Consistency and Safety

Data is written to disk slowly over time. The frequency at which this is done is configurable in `Config.cs`, although I'd recommend leaving that alone unless you understand what you're doing. Any data that is not written to disk is lost on a crash or server restart, but you can call `ForceWriteCache()` before an anticipated server shutdown to force-write all data to disk. However, if you're fine with potentially losing the last few seconds of changes, then you don't have to.

Transactions are not currently supported but if this is something that you would find useful then please make an issue or let me know :-)

With all the concurrency support this got quite complicated so please raise an issue if you encounter any bugs. Contributions are welcome too.
