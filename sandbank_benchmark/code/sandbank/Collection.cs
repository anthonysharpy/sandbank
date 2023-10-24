using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace NSSandbank;

class Collection
{
	/// <summary>
	/// Due to sandbox restrictions we have to save a string of the class type.
	/// We'll convert it back when we need it.
	/// </summary>
	public string DocumentClassTypeSerialized { get; set; }
	public string CollectionName { get; set; }
	public ConcurrentDictionary<string, Document> CachedDocuments = new();
	public Type DocumentClassType;
}
