namespace PlanelyApp.Api.Resources;

/// <summary>The text of an error code in the language of the current request.</summary>
internal interface IErrorTexts
{
    /// <summary>Null when the code has no text. The code is the contract; the text is a courtesy.</summary>
    string? Find(string code, IReadOnlyList<string>? arguments = null);
}