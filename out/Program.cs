using BaseForge.API.Extensions;
using Orders.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddBaseForge(options =>
{
    options.UsePostgreSQL<OrdersDbContext>(
        builder.Configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default tanımlı değil."));
    options.EnableCQRS(typeof(Program).Assembly);
    options.EnableAuditLog();
});

var app = builder.Build();

app.UseBaseForge();
app.MapControllers();
app.Run();
