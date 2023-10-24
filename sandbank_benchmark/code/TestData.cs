using System;

namespace SandbankBenchmark;

internal class TestData
{
	public static PlayerData TestData1 = new PlayerData()
	{
		Health = 100,
		Name = "TestPlayer1",
		Level = 10,
		LastPlayTime = DateTime.Now,
		Items = new() { "gun", "frog", "banana" }
	};

	public static PlayerData TestData2 = new PlayerData()
	{
		Health = 90,
		Name = "TestPlayer2",
		Level = 15,
		LastPlayTime = DateTime.Now,
		Items = new() { "apple", "box" }
	};
}

