using MailArchive.Application.Common;
using MailArchive.Application.Contracts.Imports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MailArchive.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/imports/parser")]
public class ImportParserStatusController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public ImportParserStatusController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet("status")]
    public IActionResult GetParserStatus()
    {
        var provider = _configuration["MailArchive:PstParserProvider"];

        if (string.IsNullOrWhiteSpace(provider))
            provider = "Mock";

        var normalizedProvider = provider.Trim();

        var response = new ImportParserStatusResponse(
            ActiveProvider: normalizedProvider,
            IsMock: string.Equals(normalizedProvider, "Mock", StringComparison.OrdinalIgnoreCase),
            IsXstReader: string.Equals(normalizedProvider, "XstReader", StringComparison.OrdinalIgnoreCase)
        );

        return Ok(ApiResponse<ImportParserStatusResponse>.Ok(response));
    }
}