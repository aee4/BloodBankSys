namespace BloodLink.Web.Tests;

public class FoundationTests
{
    [Fact]
    public void WebAssemblyMarker_IsAvailable()
    {
        Assert.Equal("BloodLink.Web", typeof(Program).Assembly.GetName().Name);
    }
}
