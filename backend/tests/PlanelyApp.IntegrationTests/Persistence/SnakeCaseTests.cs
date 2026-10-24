using PlanelyApp.Infrastructure.Persistence.Conventions;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Persistence;

public class SnakeCaseTests
{
    [Theory]
    [InlineData("Id", "id")]
    [InlineData("CompanyId", "company_id")]
    [InlineData("BranchOfferingOverride", "branch_offering_override")]
    [InlineData("Address_ZipCode", "address_zip_code")]
    [InlineData("BookingPolicy_MinimumNoticeHours", "booking_policy_minimum_notice_hours")]
    [InlineData("IX_Widget_CompanyId", "ix_widget_company_id")]
    [InlineData("URLPath", "url_path")]
    [InlineData("Line2Total", "line2_total")]
    [InlineData("already_snake_case", "already_snake_case")]
    public void A_name_should_become_snake_case(string name, string expected) =>
        SnakeCase.From(name).ShouldBe(expected);
}