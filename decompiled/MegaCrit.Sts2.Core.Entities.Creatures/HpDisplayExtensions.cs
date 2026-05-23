namespace MegaCrit.Sts2.Core.Entities.Creatures;

public static class HpDisplayExtensions
{
	public static bool IsInfinite(this HpDisplay display)
	{
		if ((uint)(display - 1) <= 1u)
		{
			return true;
		}
		return false;
	}

	public static bool ShowsNumbers(this HpDisplay display)
	{
		if ((uint)display <= 1u)
		{
			return true;
		}
		return false;
	}
}
