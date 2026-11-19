using Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

var connectionString = builder.Configuration.GetConnectionString("ConsignmentDatabase")
    ?? throw new InvalidOperationException("Connection string 'ConsignmentDatabase' is not configured.");
builder.Services.AddDbContext<ConsignmentDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<CsvImportService>();
builder.Services.AddSingleton<LatestCsvImportReport>();
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

var csvPath = Path.Combine(app.Environment.ContentRootPath, "consignments.csv");
if (!File.Exists(csvPath))
{
    csvPath = Path.Combine(app.Environment.ContentRootPath, "..", "..", "consignments.csv");
}

csvPath = Path.GetFullPath(csvPath);
await using (var scope = app.Services.CreateAsyncScope())
{
    var importer = scope.ServiceProvider.GetRequiredService<CsvImportService>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("CsvImport");
    CsvImportReport report;
    try
    {
        report = await importer.ImportAsync(csvPath);
    }
    catch (CsvImportFileException exception)
    {
        logger.LogCritical(
            exception,
            "Startup CSV import failed for {CsvPath}; application startup cannot continue: {FailureReason}",
            csvPath,
            exception.Message);
        throw;
    }

    scope.ServiceProvider.GetRequiredService<LatestCsvImportReport>().Set(report);
    logger.LogInformation(
        "CSV import complete: {Processed} processed, {Imported} imported, {Skipped} skipped, {Rejected} rejected",
        report.TotalRowsProcessed,
        report.Imported,
        report.Skipped,
        report.Rejected);

    foreach (var issue in report.Issues)
    {
        logger.LogWarning(
            "CSV import row {Row} (tenant {TenantCode}, consignment {ConsignmentNumber}): {Reason}",
            issue.Row,
            issue.TenantCode,
            issue.ConsignmentNumber,
            issue.Reason);
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
