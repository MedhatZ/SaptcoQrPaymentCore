using SaptcoQrPaymentCore.Models;
using Microsoft.Extensions.Options;
using SaptcoQrPaymentCore.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// -------------------- Services --------------------
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();
builder.Services.Configure<PaymentSettings>(
    builder.Configuration.GetSection("PaymentSettings"));
builder.Services.AddSession();

// -------------------- Build App --------------------
var app = builder.Build();

// -------------------- PathBase Handling --------------------
// ✅ Detect environment and adjust path automatically
if (app.Environment.IsDevelopment())
{
    // Local (VS) → app runs inside /SaptcoQrPayment/
    app.UsePathBase("/SaptcoQrPayment");
    Console.WriteLine("🧩 Running in Development: PathBase = /SaptcoQrPayment");
}
else
{
    // Production (IIS) → hosted at root (no prefix)
    Console.WriteLine("🚀 Running in Production: PathBase = /");
}

// -------------------- Error Handling --------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// -------------------- Middlewares --------------------
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseSession();
app.UseRouting();
app.UseAuthorization();

// ✅ Middleware to fix redirect only for HyperPay callback
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";

    if ((path.StartsWith("/QrPayment/Result", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/QrPayment/Success", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/QrPayment/Fail", StringComparison.OrdinalIgnoreCase))
        && !context.Request.PathBase.HasValue
        && app.Environment.IsDevelopment())
    {
        // Only apply this redirect locally
        var newUrl = "/SaptcoQrPayment" + path + context.Request.QueryString;
        Console.WriteLine($"🔁 Redirecting locally → {newUrl}");
        context.Response.Redirect(newUrl, permanent: false);
        return;
    }

    await next();
});

// -------------------- Routing --------------------
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=QrPayment}/{action=Index}/{id?}");

// -------------------- Run --------------------
app.Run();
