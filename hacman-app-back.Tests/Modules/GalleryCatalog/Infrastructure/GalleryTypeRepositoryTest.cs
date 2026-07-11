using System.Linq;
using System.Threading.Tasks;
using hacman_app_back.Modules.GalleryCatalog.Infrastructure;
using hacman_app_back.Tests.repository;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;

namespace hacman_app_back.Tests.Modules.GalleryCatalog.Infrastructure;

[TestClass]
[TestCategory("Integration")]
public class GalleryTypeRepositoryTest : RepositoryTestBase
{
    private const string TestSchema = "public";
    private const string EnumName = "gallery_type_enum";

    private static async Task<bool> EnumExistsAsync(string connString, string schema, string enumType)
    {
        await using var conn = new NpgsqlConnection(connString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT EXISTS(
                                SELECT 1
                                FROM pg_type t
                                JOIN pg_namespace n ON t.typnamespace = n.oid
                                WHERE t.typname = @name AND n.nspname = @schema
                            );";
        cmd.Parameters.AddWithValue("name", enumType);
        cmd.Parameters.AddWithValue("schema", schema);
        var result = await cmd.ExecuteScalarAsync();
        return result is true;
    }

    [TestMethod]
    public async Task GetAllAsync_ReturnsValues_WhenEnumExists_InTestSchema()
    {
        var cs = GetConnectionString();
        if (string.IsNullOrWhiteSpace(cs))
            Assert.Fail("Connection string not found. Provide TEST_HACMAN_CONNECTIONSTRING or ConnectionStrings:Test.");

        var enumExists = await EnumExistsAsync(cs, TestSchema, EnumName);
        if (!enumExists)
            Assert.Fail($"Precondition failed: enum '{EnumName}' not found in schema '{TestSchema}'.");

        var repo = new GalleryTypeRepository(cs + $";Search Path={TestSchema}",
            NullLogger<GalleryTypeRepository>.Instance);
        var values = (await repo.GetAllAsync()).ToList();

        Assert.IsNotNull(values);
        Assert.IsNotEmpty(values, "Expected at least one value from gallery_type_enum.");
    }

    [TestMethod]
    public async Task GetAllAsync_Throws_WhenConnectionStringIsInvalid()
    {
        var repo = new GalleryTypeRepository("Host=invalid;Database=invalid;Username=invalid;Password=invalid",
            NullLogger<GalleryTypeRepository>.Instance);

        try
        {
            await repo.GetAllAsync();
            Assert.Fail("Expected NpgsqlException was not thrown.");
        }
        catch (NpgsqlException)
        {
            // expected
        }
    }
}