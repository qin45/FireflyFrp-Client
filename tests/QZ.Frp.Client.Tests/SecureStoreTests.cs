using QZ.Frp.Client.Services;

namespace QZ.Frp.Client.Tests;

public sealed class SecureStoreTests
{
    [Fact]
    public void RoundTripsProtectedData()
    {
        var path = Path.Combine(Path.GetTempPath(), $"qz-secure-{Guid.NewGuid():N}.dat");
        try
        {
            SecureStore.Save(path, "{\"token\":\"secret\"}", "test");
            Assert.Equal("{\"token\":\"secret\"}", SecureStore.Load(path, "test"));
            Assert.Null(SecureStore.Load(path, "different-purpose"));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void ReturnsNullForCorruptData()
    {
        var path = Path.Combine(Path.GetTempPath(), $"qz-secure-{Guid.NewGuid():N}.dat");
        try
        {
            File.WriteAllBytes(path, [1, 2, 3, 4]);
            Assert.Null(SecureStore.Load(path, "test"));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
