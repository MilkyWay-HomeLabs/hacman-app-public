using Bunit;
using hacman_app_front.Components.Pages;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace hacman_app_front.Tests.Components;

public class ErrorTest : BunitContext
{
    [Fact]
    public void ShowsRequestId_WhenHttpContextTraceIdentifierIsPresent()
    {
        var httpContext = new DefaultHttpContext { TraceIdentifier = "trace-123" };

        var cut = Render<Error>(ps => ps.AddCascadingValue(httpContext));

        Assert.Contains("trace-123", cut.Markup);
        Assert.Contains("Request ID", cut.Markup);
    }

    [Fact]
    public void HidesRequestId_WhenNoHttpContextAvailable()
    {
        var cut = Render<Error>();

        Assert.DoesNotContain("Request ID", cut.Markup);
    }
}
