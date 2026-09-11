using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace Finort.Tests;

/// <summary> stub de IWebHostEnvironment apontando para o wwwroot real do projeto web. </summary>
public sealed class TestWebHostEnvironment : IWebHostEnvironment
{
    public TestWebHostEnvironment()
    {
        ContentRootPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "aspnet"));
        WebRootPath = Path.Combine(ContentRootPath, "wwwroot");
    }

    public string EnvironmentName { get; set; } = "Test";
    public string ApplicationName { get; set; } = "Finort.Tests";
    public string WebRootPath { get; set; }
    public IFileProvider WebRootFileProvider { get; set; } = null!;
    public string ContentRootPath { get; set; }
    public IFileProvider ContentRootFileProvider { get; set; } = null!;
}
