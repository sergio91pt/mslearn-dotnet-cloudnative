using System.Text.Json.Serialization;
using DataEntities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Compliance.Redaction;
using Products.Data;
using Products.Endpoints;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<EShopDataContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("ProductsContext") ??
                      throw new InvalidOperationException("Connection string 'ProductsContext' not found.")));

builder.Services.AddControllers()
    .AddJsonOptions(options => { options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.Preserve; });

builder.Services.AddRedaction(configure =>
{
    configure.SetHmacRedactor(configureHmac =>
        {
            configureHmac.KeyId = 1;
            configureHmac.Key = "R0h5MncyVDdmV2tYWldvbld6SllnT2E0bEE4MTZBdzZ4cGFFT2lDUXNKV1paSHJpMm54RERQUFVxZURNR1NTUg==";
        },
        new DataClassificationSet(DataClassifications.EUIIDataClassification)
    );
    configure.SetRedactor<StarsRedactor>(new DataClassificationSet(DataClassifications.EUPDataClassification));
});

builder.Services.AddLogging(logging =>
{
    logging.EnableRedaction();
    logging.AddJsonConsole();
});

// Add services to the container.
var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapProductEndpoints();
app.MapOrderEndpoints();

app.UseStaticFiles();

app.CreateDbIfNotExists();

app.Run();