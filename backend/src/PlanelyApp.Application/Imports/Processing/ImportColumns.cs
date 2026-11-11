namespace PlanelyApp.Application.Imports.Processing;

/// <summary>The columns an import understands, by a stable key. The headers of the file are matched to these.</summary>
internal static class ImportColumns
{
    public const string Name = "name";
    public const string Duration = "duration";
    public const string Price = "price";
    public const string Description = "description";
    public const string Category = "category";
    public const string Email = "email";
    public const string Phone = "phone";
    public const string Marketing = "marketing";
    public const string Language = "language";

    /// <summary>What is reported when a customers file has neither column.</summary>
    public const string EmailOrPhone = "email|phone";
}