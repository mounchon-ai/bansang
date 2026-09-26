using Bansang.Domain.Inventory;

namespace Bansang.Domain.Tests;

public class FactorAdvisorTests
{
    [Fact]
    public void Suggests_new_factor_after_three_shortages_in_a_row()
    {
        // ขายไป 5,100 ตัว บันทึกตัด 30 kg (สูตร 170 ตัว/kg) แต่นับแล้วขาดรวม 0.909 kg
        // → ของออกจริง 30.909 kg → ≈ 165 ตัว/kg
        var s = FactorAdvisor.Suggest([-0.3m, -0.309m, -0.3m], 30m, 5100m, 1m / 170m);
        Assert.NotNull(s);
        Assert.Equal(165m, Math.Round(s.SuggestedUnitsPerBase));
        Assert.Equal(170m, s.CurrentUnitsPerBase);
    }

    [Fact]
    public void No_suggestion_when_directions_are_mixed()
    {
        Assert.Null(FactorAdvisor.Suggest([-0.3m, 0.2m, -0.3m], 30m, 5100m, 1m / 170m));
    }

    [Fact]
    public void Zero_variances_are_ignored_and_need_enough_history()
    {
        Assert.Null(FactorAdvisor.Suggest([-0.3m, 0, -0.3m], 30m, 5100m, 1m / 170m));
        Assert.NotNull(FactorAdvisor.Suggest([0.1m, -0.3m, 0, -0.3m, -0.2m], 30m, 5100m, 1m / 170m));
    }

    [Fact]
    public void No_suggestion_without_approximate_sales()
    {
        Assert.Null(FactorAdvisor.Suggest([-0.3m, -0.3m, -0.3m], 0, 0, 1m / 170m));
    }
}
