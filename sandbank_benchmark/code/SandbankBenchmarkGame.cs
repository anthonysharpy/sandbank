using Sandbox;

namespace SandbankBenchmark;

public partial class SandbankBenchmarkGame : GameManager
{
	public override void ClientJoined( IClient cl )
	{
		Sandbank.Insert<SandbankBenchmark.PlayerData>( "players", TestData.TestData1 );
		Tests.Run();
		Benchmark.Run();
	}
}
