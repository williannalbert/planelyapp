using PlanelyApp.Domain.Imports;
using PlanelyApp.Domain.SeedWork;
using Shouldly;

namespace PlanelyApp.Domain.Tests.Imports;

public class DataImportTests
{
    private static readonly Guid CompanyId = Guid.CreateVersion7();
    private static readonly Guid UserId = Guid.CreateVersion7();
    private static readonly DateTime Now = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);

    private static DataImport Request() =>
        DataImport.Request(CompanyId, ImportType.Customers, "  clientes.xlsx ", " imports/abc.xlsx ", UserId, Now);

    private static DataImport Started(int totalRows)
    {
        var import = Request();
        import.Start(totalRows);

        return import;
    }

    // ---- Request ----

    [Fact]
    public void Request_should_create_a_pending_import()
    {
        var import = Request();

        import.CompanyId.ShouldBe(CompanyId);
        import.Type.ShouldBe(ImportType.Customers);
        import.FileName.ShouldBe("clientes.xlsx");
        import.StoragePath.ShouldBe("imports/abc.xlsx");
        import.RequestedBy.ShouldBe(UserId);
        import.CreatedAtUtc.ShouldBe(Now);
        import.Status.ShouldBe(ImportStatus.Pending);
        import.TotalRows.ShouldBe(0);
        import.SuccessRows.ShouldBe(0);
        import.ErrorRows.ShouldBe(0);
        import.Errors.ShouldBeEmpty();
        import.CompletedAtUtc.ShouldBeNull();
    }

    [Fact]
    public void Request_should_reject_invalid_arguments()
    {
        Should.Throw<DomainException>(() => DataImport.Request(Guid.Empty, ImportType.Services, "a.xlsx", "p", UserId, Now))
            .Code.ShouldBe("DataImport.CompanyRequired");
        Should.Throw<DomainException>(() => DataImport.Request(CompanyId, (ImportType)99, "a.xlsx", "p", UserId, Now))
            .Code.ShouldBe("DataImport.TypeInvalid");
        Should.Throw<DomainException>(() => DataImport.Request(CompanyId, ImportType.Services, " ", "p", UserId, Now))
            .Code.ShouldBe("DataImport.FileNameInvalid");
        Should.Throw<DomainException>(() => DataImport.Request(CompanyId, ImportType.Services, new string('a', 256), "p", UserId, Now))
            .Code.ShouldBe("DataImport.FileNameInvalid");
        Should.Throw<DomainException>(() => DataImport.Request(CompanyId, ImportType.Services, "a.xlsx", "", UserId, Now))
            .Code.ShouldBe("DataImport.StoragePathInvalid");
        Should.Throw<DomainException>(() => DataImport.Request(CompanyId, ImportType.Services, "a.xlsx", "p", Guid.Empty, Now))
            .Code.ShouldBe("DataImport.RequestedByRequired");
        Should.Throw<DomainException>(() => DataImport.Request(
                CompanyId, ImportType.Services, "a.xlsx", "p", UserId, new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Unspecified)))
            .Code.ShouldBe("DataImport.TimeMustBeUtc");
    }

    // ---- Start ----

    [Fact]
    public void Start_should_move_to_processing_with_the_row_count()
    {
        var import = Started(10);

        import.Status.ShouldBe(ImportStatus.Processing);
        import.TotalRows.ShouldBe(10);
    }

    [Fact]
    public void Start_with_a_negative_count_should_leave_the_import_untouched()
    {
        var import = Request();

        Should.Throw<DomainException>(() => import.Start(-1)).Code.ShouldBe("DataImport.TotalRowsInvalid");

        import.Status.ShouldBe(ImportStatus.Pending);
    }

    [Fact]
    public void Start_twice_should_be_rejected()
    {
        var import = Started(10);

        Should.Throw<DomainException>(() => import.Start(10)).Code.ShouldBe("DataImport.InvalidStatusTransition");
    }

    // ---- Recording rows ----

    [Fact]
    public void Rows_should_be_counted_as_successes_and_errors()
    {
        var import = Started(3);

        import.RecordSuccess();
        import.RecordSuccess();
        import.RecordError(4, "Invalid e-mail");

        import.SuccessRows.ShouldBe(2);
        import.ErrorRows.ShouldBe(1);
        import.ProcessedRows.ShouldBe(3);
        import.Errors.ShouldBe([ImportError.Create(4, "Invalid e-mail")]);
    }

    [Fact]
    public void Rows_cannot_be_recorded_before_the_import_starts()
    {
        var import = Request();

        Should.Throw<DomainException>(() => import.RecordSuccess()).Code.ShouldBe("DataImport.NotProcessing");
        Should.Throw<DomainException>(() => import.RecordError(2, "x")).Code.ShouldBe("DataImport.NotProcessing");
    }

    [Fact]
    public void More_rows_than_the_total_cannot_be_recorded()
    {
        var import = Started(1);
        import.RecordSuccess();

        Should.Throw<DomainException>(() => import.RecordSuccess()).Code.ShouldBe("DataImport.TooManyRowsProcessed");
        Should.Throw<DomainException>(() => import.RecordError(3, "x")).Code.ShouldBe("DataImport.TooManyRowsProcessed");
    }

    [Fact]
    public void An_invalid_error_should_not_be_counted()
    {
        var import = Started(5);

        Should.Throw<DomainException>(() => import.RecordError(0, "x")).Code.ShouldBe("ImportError.RowInvalid");
        Should.Throw<DomainException>(() => import.RecordError(2, " ")).Code.ShouldBe("ImportError.ReasonRequired");

        import.ErrorRows.ShouldBe(0);
        import.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void A_long_reason_should_be_cut_and_not_rejected()
    {
        var import = Started(1);

        import.RecordError(2, new string('x', 600));

        import.Errors[0].Reason.Length.ShouldBe(ImportError.ReasonMaxLength);
    }

    [Fact]
    public void Only_the_first_errors_should_be_kept_in_detail_but_all_should_be_counted()
    {
        var import = Started(DataImport.MaxStoredErrors + 5);

        for (var row = 1; row <= DataImport.MaxStoredErrors + 1; row++)
        {
            import.RecordError(row, "Bad row");
        }

        import.ErrorRows.ShouldBe(DataImport.MaxStoredErrors + 1);
        import.Errors.Count.ShouldBe(DataImport.MaxStoredErrors);
        import.ErrorsAreTruncated.ShouldBeTrue();
    }

    // ---- Complete ----

    [Fact]
    public void Complete_without_errors_should_end_as_completed()
    {
        var import = Started(2);
        import.RecordSuccess();
        import.RecordSuccess();

        import.Complete(Now.AddMinutes(1));

        import.Status.ShouldBe(ImportStatus.Completed);
        import.CompletedAtUtc.ShouldBe(Now.AddMinutes(1));
    }

    [Fact]
    public void Complete_with_errors_should_end_as_completed_with_errors()
    {
        var import = Started(2);
        import.RecordSuccess();
        import.RecordError(3, "Bad row");

        import.Complete(Now.AddMinutes(1));

        import.Status.ShouldBe(ImportStatus.CompletedWithErrors);
    }

    [Fact]
    public void An_import_of_zero_rows_should_complete()
    {
        var import = Started(0);

        import.Complete(Now.AddMinutes(1));

        import.Status.ShouldBe(ImportStatus.Completed);
    }

    [Fact]
    public void Complete_with_unprocessed_rows_should_be_rejected()
    {
        var import = Started(3);
        import.RecordSuccess();

        Should.Throw<DomainException>(() => import.Complete(Now.AddMinutes(1)))
            .Code.ShouldBe("DataImport.NotAllRowsProcessed");

        import.Status.ShouldBe(ImportStatus.Processing);
    }

    [Fact]
    public void Complete_before_starting_should_be_rejected()
    {
        Should.Throw<DomainException>(() => Request().Complete(Now.AddMinutes(1)))
            .Code.ShouldBe("DataImport.InvalidStatusTransition");
    }

    [Fact]
    public void A_finished_import_should_not_accept_more_rows()
    {
        var import = Started(1);
        import.RecordSuccess();
        import.Complete(Now.AddMinutes(1));

        Should.Throw<DomainException>(() => import.RecordSuccess()).Code.ShouldBe("DataImport.NotProcessing");
    }

    // ---- Fail ----

    [Fact]
    public void Fail_should_end_the_import_with_the_reason_from_pending_or_processing()
    {
        var pending = Request();
        pending.Fail("Unreadable file", Now.AddMinutes(1));

        var processing = Started(5);
        processing.RecordSuccess();
        processing.Fail("Job crashed", Now.AddMinutes(2));

        pending.Status.ShouldBe(ImportStatus.Failed);
        pending.FailureReason.ShouldBe("Unreadable file");
        pending.CompletedAtUtc.ShouldBe(Now.AddMinutes(1));
        processing.Status.ShouldBe(ImportStatus.Failed);
        processing.SuccessRows.ShouldBe(1);
    }

    [Fact]
    public void Fail_should_require_a_reason_and_cut_a_long_one()
    {
        var import = Request();

        Should.Throw<DomainException>(() => import.Fail(" ", Now)).Code.ShouldBe("DataImport.FailureReasonRequired");
        import.Status.ShouldBe(ImportStatus.Pending);

        import.Fail(new string('x', 700), Now);
        import.FailureReason!.Length.ShouldBe(500);
    }

    [Fact]
    public void A_finished_import_cannot_fail_or_be_completed_again()
    {
        var import = Started(0);
        import.Complete(Now.AddMinutes(1));

        Should.Throw<DomainException>(() => import.Fail("late", Now.AddMinutes(2)))
            .Code.ShouldBe("DataImport.InvalidStatusTransition");
        Should.Throw<DomainException>(() => import.Complete(Now.AddMinutes(2)))
            .Code.ShouldBe("DataImport.InvalidStatusTransition");
    }

    [Fact]
    public void CanMoveTo_should_follow_the_transition_table()
    {
        var import = Request();

        import.CanMoveTo(ImportStatus.Processing).ShouldBeTrue();
        import.CanMoveTo(ImportStatus.Failed).ShouldBeTrue();
        import.CanMoveTo(ImportStatus.Completed).ShouldBeFalse();
        import.CanMoveTo(ImportStatus.CompletedWithErrors).ShouldBeFalse();
    }
}