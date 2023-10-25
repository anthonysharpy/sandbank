using Sandbox;

namespace SandbankBenchmark;

public partial class SandbankBenchmarkGame : GameManager
{
	public override void ClientJoined( IClient cl )
	{
		Tests.Run();
		Benchmark.Run();
	}
}
