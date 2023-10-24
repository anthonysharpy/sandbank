using System;
using System.Collections.Generic;

namespace SandbankBenchmark;

class PlayerData
{
	public string ID { get; set; } = "";
	public float Health { get; set; }
	public string Name { get; set; }
	public int Level { get; set; }
	public DateTime LastPlayTime { get; set; }
	public List<string> Items { get; set; } = new();
}
