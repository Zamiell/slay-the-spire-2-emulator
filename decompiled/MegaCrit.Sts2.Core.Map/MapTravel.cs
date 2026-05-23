using System.Collections.Generic;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Runs;

namespace MegaCrit.Sts2.Core.Map;

public static class MapTravel
{
	public static IEnumerable<MapPoint> GetTravelablePointsFrom(IRunState runState, MapPoint currentPoint)
	{
		if (Hook.ShouldAllowFreeTravel(runState))
		{
			return runState.Map.GetPointsInRow(currentPoint.coord.row + 1);
		}
		return currentPoint.Children;
	}
}
