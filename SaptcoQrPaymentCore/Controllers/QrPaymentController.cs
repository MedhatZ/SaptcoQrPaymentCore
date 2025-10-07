using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using SaptcoQrPaymentCore.Models;
using QRCoder;
using System.Drawing;
using System.IO;
using System.Drawing.Imaging;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SaptcoQrPaymentCore.Data;

namespace SaptcoQrPaymentCore.Controllers
{
    public class QrPaymentController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<QrPaymentController> _logger;
        private readonly PaymentSettings _paymentSettings;
        private readonly AppDbContext _context;

        public QrPaymentController(
            IHttpClientFactory httpClientFactory,
            ILogger<QrPaymentController> logger,
            IOptions<PaymentSettings> paymentOptions, AppDbContext context)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _paymentSettings = paymentOptions.Value;
            _context = context;
        }


        // Step 1: Show mobile entry
        public IActionResult Index() => View();

        // Step 2: Check mobile
        [HttpPost]
        public async Task<IActionResult> CheckMobile(string phone)
        {
            if (string.IsNullOrEmpty(phone))
            {
                ViewBag.Error = "Please enter a valid mobile number.";
                return View("Index");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Phone == phone);

            if (user != null)
            {
                // existing user → go to payment
                HttpContext.Session.SetString("Phone", user.Phone);
                return RedirectToAction("Purchase");
            }

            // new user → go to register
            ViewBag.Phone = phone;
            return View("Register");
        }

        // Step 3: Register new user
        [HttpPost]
        public async Task<IActionResult> Register(string phone, string name, string email)
        {
            if (string.IsNullOrEmpty(phone) || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(email))
            {
                ViewBag.Error = "All fields are required.";
                ViewBag.Phone = phone;
                return View();
            }

            var user = new User { Phone = phone, Name = name, Email = email };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            HttpContext.Session.SetString("Phone", user.Phone);
            return RedirectToAction("Purchase");
        }

        [HttpGet]
        public IActionResult Purchase()
        {
            ViewBag.Phone = HttpContext.Session.GetString("Phone");
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> SavePhone(string phone)
        {
            if (string.IsNullOrEmpty(phone))
            {
                ViewBag.Error = "Phone number is required.";
                return View("Purchase");
            }

            // Save to DB
            var user = new User { Phone = phone };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Save in session
            HttpContext.Session.SetString("Phone", phone);

            return RedirectToAction("Purchase");
        }

        [HttpPost]
        public async Task<IActionResult> ExecutePurchase()
        {
            var handler = new HttpClientHandler
            {
                UseCookies = true,
                CookieContainer = new CookieContainer(),
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            using (var client = new HttpClient(handler))
            {
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                client.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");

                // Step 1: Login
                var loginUrl = "https://fcpuat.saptco.com.sa/login?username=ext_website&password=Saptco@123";
                var loginResponse = await client.PostAsync(loginUrl, null);
                var loginBody = await loginResponse.Content.ReadAsStringAsync();

                if (!loginResponse.IsSuccessStatusCode)
                {
                    ViewBag.Error = $"Login failed ({loginResponse.StatusCode}). Response: {loginBody}";
                    return View("Index");
                }

                // Step 2: Initiate payment (3DS check)
                var payload = new
                {
                    url = _paymentSettings.SuccessUrl,
                    failUrl = _paymentSettings.FailUrl,
                    @event = new { fareId = "1093879357650108419" },
                    actionType = "QR_PURCHASE",
                    paymentParams = new { qrFormat = "V8" }
                };

                var json = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var paymentUrl = "https://fcpuat.saptco.com.sa/api/v1/payments/3ds/check";
                var paymentResponse = await client.PostAsync(paymentUrl, content);
                var paymentResult = await paymentResponse.Content.ReadAsStringAsync();

                if (!paymentResponse.IsSuccessStatusCode)
                {
                    ViewBag.Error = $"Payment check failed ({paymentResponse.StatusCode}). Response: {paymentResult}";
                    return View("Index");
                }

                string scriptUrl = "";
                try
                {
                    var parsed = JObject.Parse(paymentResult);
                    scriptUrl = parsed["url"]?.ToString() ?? "";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to parse payment check response");
                    ViewBag.Error = "Failed to parse JSON response.";
                    return View("Index");
                }

                ViewBag.ScriptUrl = scriptUrl;
                return View("PaymentForm");
            }
        }

        [HttpGet]
        public async Task<IActionResult> Result(string id = null, string resourcePath = null, string ordernumber = null)
        {
            ViewBag.OrderNumber = ordernumber ?? id;
            ViewBag.CheckoutId = id;
            ViewBag.ResourcePath = resourcePath;

            _logger.LogInformation("Result called with id={id}, resourcePath={resourcePath}, ordernumber={ordernumber}", id, resourcePath, ordernumber);

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("User-Agent", "SaptcoQrPaymentCore/1.0");

            try
            {
                // Case 1: HyperPay verification
                if (!string.IsNullOrEmpty(resourcePath))
                {
                    var hyperPayHost = "https://test.oppwa.com";
                    var verifyUrl = $"{hyperPayHost}{WebUtility.UrlDecode(resourcePath)}";

                    _logger.LogInformation("Calling HyperPay verify URL: {verifyUrl}", verifyUrl);
                    var verifyResponse = await client.GetAsync(verifyUrl);
                    var payload = await verifyResponse.Content.ReadAsStringAsync();

                    _logger.LogInformation("HyperPay verify response status: {status}. Body: {body}", verifyResponse.StatusCode, payload);

                    if (!verifyResponse.IsSuccessStatusCode)
                    {
                        ViewBag.Status = "error";
                        ViewBag.Message = $"Verification failed: {verifyResponse.StatusCode}";
                        ViewBag.RawResponse = payload;
                        return View("Result");
                    }

                    var json = JObject.Parse(payload);
                    var resultCode = json.SelectToken("result.code")?.ToString() ?? "";
                    var merchantTransactionId = json.SelectToken("merchantTransactionId")?.ToString() ?? "";
                    var paymentStatus = json.SelectToken("payment.status")?.ToString() ?? json.SelectToken("status")?.ToString();

                    ViewBag.RawResponse = json.ToString();
                    ViewBag.Status = paymentStatus ?? resultCode ?? "unknown";
                    ViewBag.Message = $"Verified via HyperPay. Status={ViewBag.Status} (resultCode={resultCode})";
                    ViewBag.ResponseJson = json;

                    // Proceed to confirm if successful
                    if (resultCode == "000.100.110" || resultCode == "000.100.111" || resultCode == "000.100.112")
                    {

                        // 🧱 Ensure login session is active before confirming
                        _logger.LogInformation("Re-authenticating before confirm...");
                        var loginUrl = "https://fcpuat.saptco.com.sa/login?username=ext_website&password=Saptco@123";
                        var loginResponse = await client.PostAsync(loginUrl, null);
                        if (!loginResponse.IsSuccessStatusCode)
                        {
                            _logger.LogWarning("Login failed before confirm: {status}", loginResponse.StatusCode);
                        }
                        else
                        {
                            _logger.LogInformation("Login re-validated successfully before confirm.");
                        }

                        // ⏳ Wait 3 seconds before confirm (to allow QR generation on backend)
                        await Task.Delay(3000);

                        _logger.LogInformation("Payment approved. Proceeding to confirm with SAPTCO using merchantTransactionId={merchantTransactionId}", merchantTransactionId);

                        var sapBase = "https://fcpuat.saptco.com.sa";
                        var confirmUrl = $"{sapBase}/api/v1/payments/3ds/confirm";

                        var confirmBody = new { ordernumber = merchantTransactionId };
                        var confirmContent = new StringContent(JObject.FromObject(confirmBody).ToString(), Encoding.UTF8, "application/json");

                        var confirmResponse = await client.PostAsync(confirmUrl, confirmContent);
                        var confirmJson = await confirmResponse.Content.ReadAsStringAsync();

                        _logger.LogInformation("SAPTCO confirm response: {body}", confirmJson);

                        if (confirmResponse.IsSuccessStatusCode)
                        {
                            var confirmObj = JObject.Parse(confirmJson);
                            var qrValue = confirmObj["event"]?["parameters"]?["qr"]?.ToString();

                            if (!string.IsNullOrEmpty(qrValue))
                            {
                                using (var qrGenerator = new QRCodeGenerator())
                                using (var qrData = qrGenerator.CreateQrCode(qrValue, QRCodeGenerator.ECCLevel.Q))
                                using (var qrCode = new QRCode(qrData))
                                using (var qrBitmap = qrCode.GetGraphic(20))
                                using (var ms = new MemoryStream())
                                {
                                    qrBitmap.Save(ms, ImageFormat.Png);
                                    var base64 = Convert.ToBase64String(ms.ToArray());
                                    ViewBag.QrImage = $"data:image/png;base64,{base64}";
                                }

                                ViewBag.Message += " → SAPTCO Confirmed & QR generated successfully ✅";
                            }
                            else
                            {
                                ViewBag.Message += " → SAPTCO confirm succeeded but QR not found.";
                            }
                        }
                        else
                        {
                            ViewBag.Message += $" → SAPTCO confirm failed ({confirmResponse.StatusCode}).";
                        }
                    }

                    return View("Result");
                }

                // Case 2: Direct confirm by merchantTransactionId
                if (!string.IsNullOrEmpty(ordernumber))
                {
                    var confirmUrl = "https://fcpuat.saptco.com.sa/api/v1/payments/3ds/confirm";
                    var bodyObj = new { ordernumber = ordernumber };
                    var content = new StringContent(JObject.FromObject(bodyObj).ToString(), Encoding.UTF8, "application/json");

                    var confirmResponse = await client.PostAsync(confirmUrl, content);
                    var confirmBody = await confirmResponse.Content.ReadAsStringAsync();

                    _logger.LogInformation("SAPTCO confirm response: {body}", confirmBody);

                    if (!confirmResponse.IsSuccessStatusCode)
                    {
                        ViewBag.Status = "error";
                        ViewBag.Message = $"Confirm endpoint returned {confirmResponse.StatusCode}";
                        ViewBag.RawResponse = confirmBody;
                        return View("Result");
                    }

                    var json = JObject.Parse(confirmBody);
                    var successFlag = json["success"]?.ToObject<bool?>() ?? false;
                    var qrValue = json["event"]?["parameters"]?["qr"]?.ToString();

                    ViewBag.Status = successFlag == true ? "paid" : "notpaid";
                    ViewBag.Message = $"SAPTCO confirm returned success={successFlag}";
                    ViewBag.ResponseJson = json;

                    if (!string.IsNullOrEmpty(qrValue))
                    {
                        using (var qrGenerator = new QRCodeGenerator())
                        using (var qrData = qrGenerator.CreateQrCode(qrValue, QRCodeGenerator.ECCLevel.Q))
                        using (var qrCode = new QRCode(qrData))
                        using (var qrBitmap = qrCode.GetGraphic(20))
                        using (var ms = new MemoryStream())
                        {
                            qrBitmap.Save(ms, ImageFormat.Png);
                            var base64 = Convert.ToBase64String(ms.ToArray());
                            ViewBag.QrImage = $"data:image/png;base64,{base64}";
                        }
                    }

                    return View("Result");
                }

                // Case 3: Missing params
                ViewBag.Status = "unknown";
                ViewBag.Message = "No resourcePath or ordernumber provided to verify the payment.";
                return View("Result");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during Result verification");
                ViewBag.Status = "error";
                ViewBag.Message = ex.Message;
                return View("Result");
            }
        }

        public IActionResult Success(string result = "success", string ordernumber = "")
        {
            ViewBag.Result = result;
            ViewBag.OrderNumber = ordernumber;
            return View();
        }

        public IActionResult Fail(string result = "fail", string ordernumber = "")
        {
            ViewBag.Result = result;
            ViewBag.OrderNumber = ordernumber;
            return View();
        }
    }
}
