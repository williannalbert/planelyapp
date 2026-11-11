namespace PlanelyApp.Application.Abstractions.Imports;

public sealed class ImportSheetException(string message, Exception? innerException = null)
    : Exception(message, innerException);