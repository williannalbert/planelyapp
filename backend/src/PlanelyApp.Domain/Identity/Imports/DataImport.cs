using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Domain.Imports;

/// <summary>
/// The import of a spreadsheet of services or customers. The API creates it as pending, a background job
/// processes the file row by row, and the front reads the progress.
/// </summary>
public sealed class DataImport : AggregateRoot, ITenantEntity
{
    /// <summary>Errors beyond this are only counted: a huge bad file must not become a huge record.</summary>
    public const int MaxStoredErrors = 1000;

    private const int FileNameMaxLength = 255;
    private const int StoragePathMaxLength = 500;
    private const int FailureReasonMaxLength = 500;

    private static readonly Dictionary<ImportStatus, ImportStatus[]> AllowedTransitions = new()
    {
        [ImportStatus.Pending] = [ImportStatus.Processing, ImportStatus.Failed],
        [ImportStatus.Processing] = [ImportStatus.Completed, ImportStatus.CompletedWithErrors, ImportStatus.Failed],
        [ImportStatus.Completed] = [],
        [ImportStatus.CompletedWithErrors] = [],
        [ImportStatus.Failed] = [],
    };

    private readonly List<ImportError> _errors = [];

    private DataImport(
        Guid companyId,
        ImportType type,
        string fileName,
        string storagePath,
        Guid requestedBy,
        DateTime createdAtUtc)
    {
        CompanyId = companyId;
        Type = type;
        FileName = fileName;
        StoragePath = storagePath;
        RequestedBy = requestedBy;
        CreatedAtUtc = createdAtUtc;
        Status = ImportStatus.Pending;
    }

    public Guid CompanyId { get; }

    public ImportType Type { get; }

    /// <summary>The name of the file the user sent.</summary>
    public string FileName { get; }

    /// <summary>Where the API saved the file, for the job to read. Opaque to the domain.</summary>
    public string StoragePath { get; }

    public ImportStatus Status { get; private set; }

    public int TotalRows { get; private set; }

    public int SuccessRows { get; private set; }

    public int ErrorRows { get; private set; }

    /// <summary>The first errors, at most <see cref="MaxStoredErrors"/>. <see cref="ErrorRows"/> counts them all.</summary>
    public IReadOnlyList<ImportError> Errors => _errors.AsReadOnly();

    public bool ErrorsAreTruncated => ErrorRows > _errors.Count;

    /// <summary>Why the import failed as a whole. Only when the status is Failed.</summary>
    public string? FailureReason { get; private set; }

    public Guid RequestedBy { get; }

    public DateTime CreatedAtUtc { get; }

    public DateTime? CompletedAtUtc { get; private set; }

    public int ProcessedRows => SuccessRows + ErrorRows;

    public static DataImport Request(
        Guid companyId,
        ImportType type,
        string? fileName,
        string? storagePath,
        Guid requestedBy,
        DateTime nowUtc)
    {
        if (companyId == Guid.Empty)
        {
            throw new DomainException("DataImport.CompanyRequired");
        }

        if (!Enum.IsDefined(type))
        {
            throw new DomainException("DataImport.TypeInvalid");
        }

        if (requestedBy == Guid.Empty)
        {
            throw new DomainException("DataImport.RequestedByRequired");
        }

        Guard.Utc(nowUtc, "DataImport.TimeMustBeUtc");

        return new DataImport(
            companyId,
            type,
            Guard.RequiredText(fileName, FileNameMaxLength, "DataImport.FileNameInvalid"),
            Guard.RequiredText(storagePath, StoragePathMaxLength, "DataImport.StoragePathInvalid"),
            requestedBy,
            nowUtc);
    }

    public bool CanMoveTo(ImportStatus to) => AllowedTransitions[Status].Contains(to);

    /// <summary>The job opened the file and counted its rows.</summary>
    public void Start(int totalRows)
    {
        EnsureCanMoveTo(ImportStatus.Processing);

        if (totalRows < 0)
        {
            throw new DomainException("DataImport.TotalRowsInvalid");
        }

        TotalRows = totalRows;
        Status = ImportStatus.Processing;
    }

    public void RecordSuccess()
    {
        EnsureCanRecordRow();

        SuccessRows++;
    }

    /// <summary>Counts a row that failed. The detail is kept for the first <see cref="MaxStoredErrors"/> errors.</summary>
    public void RecordError(int row, string? reason)
    {
        EnsureCanRecordRow();

        // Built first: if the row or the reason is invalid, nothing has changed yet.
        var error = ImportError.Create(row, reason);

        ErrorRows++;

        if (_errors.Count < MaxStoredErrors)
        {
            _errors.Add(error);
        }
    }

    /// <summary>All rows were processed. It ends as completed, or completed with errors if any row failed.</summary>
    public void Complete(DateTime nowUtc)
    {
        Guard.Utc(nowUtc, "DataImport.TimeMustBeUtc");

        var to = ErrorRows == 0 ? ImportStatus.Completed : ImportStatus.CompletedWithErrors;
        EnsureCanMoveTo(to);

        if (ProcessedRows != TotalRows)
        {
            throw new DomainException("DataImport.NotAllRowsProcessed", ProcessedRows, TotalRows);
        }

        Status = to;
        CompletedAtUtc = nowUtc;
    }

    /// <summary>The import could not go on (unreadable file, the job crashed...). Rows already counted are kept.</summary>
    public void Fail(string? reason, DateTime nowUtc)
    {
        Guard.Utc(nowUtc, "DataImport.TimeMustBeUtc");
        EnsureCanMoveTo(ImportStatus.Failed);

        var validReason = Guard.RequiredTextTruncated(reason, FailureReasonMaxLength, "DataImport.FailureReasonRequired");

        Status = ImportStatus.Failed;
        FailureReason = validReason;
        CompletedAtUtc = nowUtc;
    }

    private void EnsureCanMoveTo(ImportStatus to)
    {
        if (!CanMoveTo(to))
        {
            throw new DomainException("DataImport.InvalidStatusTransition", Status, to);
        }
    }

    private void EnsureCanRecordRow()
    {
        if (Status != ImportStatus.Processing)
        {
            throw new DomainException("DataImport.NotProcessing", Status);
        }

        if (ProcessedRows >= TotalRows)
        {
            throw new DomainException("DataImport.TooManyRowsProcessed", TotalRows);
        }
    }
}