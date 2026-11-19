using Api.Data;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/imports")]
public sealed class ImportsController(LatestCsvImportReport latestImportReport) : ControllerBase
{
    [HttpGet("latest")]
    public ActionResult<CsvImportReport> GetLatest() =>
        latestImportReport.Report is { } report ? Ok(report) : NotFound();
}