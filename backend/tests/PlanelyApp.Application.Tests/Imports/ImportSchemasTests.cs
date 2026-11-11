using PlanelyApp.Application.Imports.Processing;
using PlanelyApp.Domain.Imports;
using Shouldly;

namespace PlanelyApp.Application.Tests.Imports;

public class ImportSchemasTests
{
    [Theory]
    [InlineData("Duração (min)", "duracaomin")]
    [InlineData("E-mail", "email")]
    [InlineData("  Nome ", "nome")]
    [InlineData("TELÉFONO", "telefono")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void A_header_should_be_normalized(string? header, string expected) =>
        ImportSchemas.Normalize(header).ShouldBe(expected);

    [Fact]
    public void Headers_in_english_portuguese_and_spanish_should_be_recognized()
    {
        var map = ImportSchemas.MapHeaders(
            ["Nome", "Duração", "Preço", "Descripción", "Categoría", "E-mail", "Teléfono", "Marketing", "Idioma"]);

        string[] keys =
        [
            ImportColumns.Name, ImportColumns.Duration, ImportColumns.Price, ImportColumns.Description,
            ImportColumns.Category, ImportColumns.Email, ImportColumns.Phone, ImportColumns.Marketing, ImportColumns.Language,
        ];

        foreach (var key in keys)
        {
            map.Has(key).ShouldBeTrue(key);
        }
    }

    [Fact]
    public void A_cell_should_come_trimmed_and_empty_when_the_column_or_the_cell_is_missing()
    {
        var map = ImportSchemas.MapHeaders(["Name", "Duration"]);

        map.Get(["  Haircut ", "30"], ImportColumns.Name).ShouldBe("Haircut");
        map.Get(["Haircut"], ImportColumns.Duration).ShouldBe(string.Empty);
        map.Get(["Haircut", "30"], ImportColumns.Price).ShouldBe(string.Empty);
    }

    [Fact]
    public void The_first_header_of_a_column_should_win()
    {
        var map = ImportSchemas.MapHeaders(["Name", "Nome"]);

        map.Get(["First", "Second"], ImportColumns.Name).ShouldBe("First");
    }

    [Fact]
    public void A_services_file_should_need_the_name_and_the_duration()
    {
        ImportSchemas.FirstMissingColumn(ImportType.Services, ImportSchemas.MapHeaders(["Duration"])).ShouldBe("name");
        ImportSchemas.FirstMissingColumn(ImportType.Services, ImportSchemas.MapHeaders(["Name"])).ShouldBe("duration");
        ImportSchemas.FirstMissingColumn(ImportType.Services, ImportSchemas.MapHeaders(["Name", "Duration"])).ShouldBeNull();
    }

    [Fact]
    public void A_customers_file_should_need_the_email_or_the_phone()
    {
        ImportSchemas.FirstMissingColumn(ImportType.Customers, ImportSchemas.MapHeaders(["Name"])).ShouldBe("email|phone");
        ImportSchemas.FirstMissingColumn(ImportType.Customers, ImportSchemas.MapHeaders(["Phone"])).ShouldBeNull();
        ImportSchemas.FirstMissingColumn(ImportType.Customers, ImportSchemas.MapHeaders(["E-mail"])).ShouldBeNull();
    }
}