using IhiksTaxi.Application;
using IhiksTaxi.Domain;

namespace IhiksTaxi.Application.Tests;

public class TripStatusRulesTests
{
	[Test]
	public void Next_moves_along_the_lifecycle()
	{
		Assert.AreEqual(TripStatusNames.SearchingDriver, TripStatusRules.Next(TripStatusNames.Created));
		Assert.AreEqual(TripStatusNames.DriverAssigned, TripStatusRules.Next(TripStatusNames.SearchingDriver));
		Assert.IsNull(TripStatusRules.Next(TripStatusNames.Completed));
	}

	[TestCase(TripStatusNames.Created, true)]
	[TestCase(TripStatusNames.DriverAssigned, true)]
	[TestCase(TripStatusNames.InProgress, false)]
	[TestCase(TripStatusNames.Completed, false)]
	public void CanCancel_depends_on_the_current_state(string status, bool expected)
	{
		Assert.AreEqual(expected, TripStatusRules.CanCancel(status));
	}
}
