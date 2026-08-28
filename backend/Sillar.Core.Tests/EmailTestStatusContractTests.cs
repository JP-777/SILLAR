using Sillar.Core.Dtos;

namespace Sillar.Core.Tests;

public sealed class EmailTestStatusContractTests
{
    [Fact]
    public void Estado_smtp_no_expone_secretos()
    {
        var names = typeof(EmailTestStatusResponse)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain(
            names,
            name => name.Contains(
                "Password",
                StringComparison.OrdinalIgnoreCase));

        Assert.DoesNotContain(
            names,
            name => name.Contains(
                "Secret",
                StringComparison.OrdinalIgnoreCase));

        Assert.DoesNotContain(
            names,
            name => name.Contains(
                "Token",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Estado_smtp_representa_nunca_probado_sin_inventar_fecha()
    {
        var status = new EmailTestStatusResponse(
            NeverTested: true,
            LastTestedAt: null,
            LastSuccess: null);

        Assert.True(status.NeverTested);
        Assert.Null(status.LastTestedAt);
        Assert.Null(status.LastSuccess);
    }
}
