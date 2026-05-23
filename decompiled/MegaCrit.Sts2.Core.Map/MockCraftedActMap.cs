namespace MegaCrit.Sts2.Core.Map;

public sealed class MockCraftedActMap : ActMap
{
	public override MapPoint BossMapPoint { get; }

	public override MapPoint StartingMapPoint { get; }

	protected override MapPoint?[,] Grid { get; }

	public MockCraftedActMap(int width, int height, MapPoint startingPoint, MapPoint bossPoint)
	{
		Grid = new MapPoint[width, height];
		StartingMapPoint = startingPoint;
		BossMapPoint = bossPoint;
	}

	public void Put(int col, int row, MapPointType type = MapPointType.Monster)
	{
		MapPoint mapPoint = new MapPoint(col, row)
		{
			PointType = type
		};
		Grid[col, row] = mapPoint;
	}
}
