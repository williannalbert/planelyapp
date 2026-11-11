using NSubstitute;
using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Application.Abstractions.Imports;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Imports;
using PlanelyApp.Application.Imports.Request;
using PlanelyApp.Application.Tests.Organization.Branches;
using PlanelyApp.Application.Tests.Scheduling;
using PlanelyApp.Domain.Imports;
using PlanelyApp.Domain.SeedWork;
using Shouldly;

namespace PlanelyApp.Application.Tests.Imports;

public class RequestImportTests
{
    private static readonly DateTime Now = new(2027, 11, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid CompanyId = Guid.CreateVersion7();
    private static readonly Guid UserId = Guid.CreateVersion7();
    private const string StoragePath = "company/file.xlsx";

    private readonly IDataImportRepository _imports = Substitute.For<IDataImportRepository>();
    private readonly IImportFileStore _files = Substitute.For<IImportFileStore>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public RequestImportTests()
    {
        _currentUser.Kind.Returns(ActorKind.Staff);
        _currentUser.Id.Returns(UserId);
        _files.SaveAsync(CompanyId, Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(StoragePath);
    }

    [Fact]
    public async Task The_file_should_be_stored_and_a_pending_import_requested_by_the_user()
    {
        var added = new List<DataImport>();
        _imports.When(imports => imports.Add(Arg.Any<DataImport>())).Do(call => added.Add(call.Arg<DataImport>()));
        using var content = Spreadsheet();

        var result = await Handler().HandleAsync(
            new RequestImportCommand(ImportType.Customers, "  clients.xlsx ", content), _ct);

        result.IsSuccess.ShouldBeTrue();
        var import = added.Single();
        result.Value.ShouldBe(import.Id);
        import.CompanyId.ShouldBe(CompanyId);
        import.Type.ShouldBe(ImportType.Customers);
        import.FileName.ShouldBe("clients.xlsx");
        import.StoragePath.ShouldBe(StoragePath);
        import.RequestedBy.ShouldBe(UserId);
        import.CreatedAtUtc.ShouldBe(Now);
        import.Status.ShouldBe(ImportStatus.Pending);
        await _files.Received(1).SaveAsync(CompanyId, content, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_customer_should_be_refused_before_anything_is_saved()
    {
        _currentUser.Kind.Returns(ActorKind.Customer);
        using var content = Spreadsheet();

        var result = await Handler().HandleAsync(
            new RequestImportCommand(ImportType.Services, "services.xlsx", content), _ct);

        result.Error.Type.ShouldBe(ErrorType.Forbidden);
        result.Error.Code.ShouldBe("DataImport.StaffOnly");
        _imports.DidNotReceive().Add(Arg.Any<DataImport>());
        await _files.DidNotReceiveWithAnyArgs().SaveAsync(default, default!, _ct);
    }

    [Fact]
    public async Task A_file_name_the_domain_refuses_should_reach_the_caller_and_no_import_should_be_added()
    {
        using var content = Spreadsheet();

        var exception = await Should.ThrowAsync<DomainException>(() => Handler().HandleAsync(
            new RequestImportCommand(ImportType.Services, new string('a', 252) + ".xlsx", content), _ct));

        exception.Code.ShouldBe("DataImport.FileNameInvalid");
        _imports.DidNotReceive().Add(Arg.Any<DataImport>());
    }

    [Fact]
    public async Task A_valid_request_should_pass_the_validator()
    {
        using var content = Spreadsheet();

        var problems = await TestValidation.RunAsync(
            new RequestImportValidator(), new RequestImportCommand(ImportType.Services, "services.xlsx", content));

        problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_extension_should_be_accepted_in_any_case()
    {
        using var content = Spreadsheet();

        var problems = await TestValidation.RunAsync(
            new RequestImportValidator(), new RequestImportCommand(ImportType.Services, "SERVICES.XLSX", content));

        problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_file_that_is_not_a_xlsx_should_be_reported()
    {
        using var content = Spreadsheet();

        var problems = await TestValidation.RunAsync(
            new RequestImportValidator(), new RequestImportCommand(ImportType.Services, "services.csv", content));

        problems.ShouldBe(["FileName:Validation.Invalid"], ignoreOrder: true);
    }

    [Fact]
    public async Task A_missing_file_name_should_be_reported_as_required()
    {
        using var content = Spreadsheet();

        var problems = await TestValidation.RunAsync(
            new RequestImportValidator(), new RequestImportCommand(ImportType.Services, null, content));

        problems.ShouldBe(["FileName:Validation.Required"], ignoreOrder: true);
    }

    [Fact]
    public async Task A_type_that_does_not_exist_should_be_reported()
    {
        using var content = Spreadsheet();

        var problems = await TestValidation.RunAsync(
            new RequestImportValidator(), new RequestImportCommand((ImportType)9, "services.xlsx", content));

        problems.ShouldBe(["Type:Validation.Invalid"], ignoreOrder: true);
    }

    [Fact]
    public async Task A_missing_content_should_be_reported_as_required()
    {
        var problems = await TestValidation.RunAsync(
            new RequestImportValidator(), new RequestImportCommand(ImportType.Services, "services.xlsx", null));

        problems.ShouldBe(["Content:Validation.Required"], ignoreOrder: true);
    }

    [Fact]
    public async Task An_empty_file_should_be_reported_as_required()
    {
        using var content = new MemoryStream();

        var problems = await TestValidation.RunAsync(
            new RequestImportValidator(), new RequestImportCommand(ImportType.Services, "services.xlsx", content));

        problems.ShouldBe(["Content:Validation.Required"], ignoreOrder: true);
    }

    [Fact]
    public async Task A_file_over_the_limit_should_be_reported_as_too_long()
    {
        using var content = new MemoryStream();
        content.SetLength(ImportRules.MaxFileBytes + 1);

        var problems = await TestValidation.RunAsync(
            new RequestImportValidator(), new RequestImportCommand(ImportType.Services, "services.xlsx", content));

        problems.ShouldBe(["Content:Validation.TooLong"], ignoreOrder: true);
    }

    [Fact]
    public async Task A_stream_that_cannot_seek_should_be_reported()
    {
        var content = Substitute.For<Stream>();
        content.CanRead.Returns(true);
        content.CanSeek.Returns(false);

        var problems = await TestValidation.RunAsync(
            new RequestImportValidator(), new RequestImportCommand(ImportType.Services, "services.xlsx", content));

        problems.ShouldBe(["Content:Validation.Invalid"], ignoreOrder: true);
    }

    [Fact]
    public async Task A_stream_that_cannot_be_read_should_be_reported()
    {
        var content = Substitute.For<Stream>();
        content.CanRead.Returns(false);
        content.CanSeek.Returns(true);
        content.Length.Returns(10L);

        var problems = await TestValidation.RunAsync(
            new RequestImportValidator(), new RequestImportCommand(ImportType.Services, "services.xlsx", content));

        problems.ShouldBe(["Content:Validation.Invalid"], ignoreOrder: true);
    }

    private static MemoryStream Spreadsheet() => new(new byte[] { 1, 2, 3 });

    private RequestImportHandler Handler() =>
        new(_imports, _files, _currentUser, BranchData.TenantOf(CompanyId), new FixedTimeProvider(Now));
}