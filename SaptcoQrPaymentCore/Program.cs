using SaptcoQrPaymentCore.Models;
using Microsoft.Extensions.Options;
using SaptcoQrPaymentCore.Data;
using Microsoft.EntityFrameworkCore;
using SaptcoQrPaymentCore.Data;

var builder = WebApplication.CreateBuilder(args);

// -------------------- Services --------------------

// ✅ Add SQL Server connection
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();
builder.Services.Configure<PaymentSettings>(
    builder.Configuration.GetSection("PaymentSettings"));
builder.Services.AddSession();

// -------------------- Build App --------------------
var app = builder.Build();

// -------------------- PathBase --------------------
// التطبيق فعليًا منشور داخل فولدر فرعي اسمه /SaptcoQrPayment
app.UsePathBase("/SaptcoQrPayment");

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

// ✅ Middleware لإصلاح الريدايركت الخطأ من HyperPay فقط
app.Use(async (context, next) =>
{
    var originalPath = context.Request.Path.Value ?? "";
    var originalBase = context.Request.PathBase.Value ?? "";

    // لو الطلب بدأ بـ /QrPayment/Result أو Success أو Fail
    if (originalPath.StartsWith("/QrPayment/Result", StringComparison.OrdinalIgnoreCase)
        || originalPath.StartsWith("/QrPayment/Success", StringComparison.OrdinalIgnoreCase)
        || originalPath.StartsWith("/QrPayment/Fail", StringComparison.OrdinalIgnoreCase))
    {
        // إذا الـ PathBase لا يحتوي SaptcoQrPayment بالفعل
        if (!originalBase.Contains("SaptcoQrPayment", StringComparison.OrdinalIgnoreCase))
        {
            var newUrl = "/SaptcoQrPayment" + originalPath + context.Request.QueryString;
            Console.WriteLine($"🔁 Redirecting path {originalBase + originalPath} → {newUrl}");
            context.Response.Redirect(newUrl, permanent: false);
            return;
        }
    }

    await next();
});

// -------------------- Routing --------------------
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=QrPayment}/{action=Index}/{id?}");

// -------------------- Run --------------------
app.Run();
