using ArenaGodEyes.Core.Domain.Product;

namespace ArenaGodEyes.Tests.Core.Domain;

public sealed class ProductMetadataTests
{
    [Fact]
    public void ProductMetadata_UsesExpectedProjectIdentity()
    {
        Assert.Equal("ArenaGodEyes", ProductMetadata.Name);
        Assert.Equal("0.1.0", ProductMetadata.Version);
        Assert.Equal("The eyes above your arena.", ProductMetadata.Tagline);
    }
}
