using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SaptcoQrPaymentCore.Controllers
{
    public class QrPaymentController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<QrPaymentController> _logger;

        public QrPaymentController(IHttpClientFactory httpClientFactory, ILogger<QrPaymentController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Purchase()
        {
            // 🧱 Handler فيه دعم للكوكيز (عشان نحافظ على الـ Session)
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

                // 1️⃣ محاولة تسجيل الدخول
                var loginUrl = "https://fcpuat.saptco.com.sa/login?username=ext_website&password=Saptco@123";
                var loginResponse = await client.PostAsync(loginUrl, null);
                var loginBody = await loginResponse.Content.ReadAsStringAsync();

                if (!loginResponse.IsSuccessStatusCode)
                {
                    ViewBag.Error = $"Login failed ({loginResponse.StatusCode}). Response: {loginBody}";
                    return View("Index");
                }

                // 2️⃣ الطلب التالي بعد نجاح اللوجن
                var payload = new
                {
                    url = "https://localhost:7083/QrPayment/Success",
                    failUrl = "https://localhost:7083/QrPayment/Fail",
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

                // 3️⃣ تحليل النتيجة
                string scriptUrl = "";
                try
                {
                    var parsed = JObject.Parse(paymentResult);
                    scriptUrl = parsed["url"]?.ToString() ?? "";

                }
                catch
                {
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
            // تفاصيل للعرض
            ViewBag.OrderNumber = ordernumber ?? id;
            ViewBag.CheckoutId = id;
            ViewBag.ResourcePath = resourcePath;

            // Log incoming params
            _logger.LogInformation("Result called with id={id}, resourcePath={resourcePath}, ordernumber={ordernumber}", id, resourcePath, ordernumber);

            // HTTP client
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("User-Agent", "SaptcoQrPaymentCore/1.0");

            try
            {
                // 1) حالة HyperPay / OPPWA: resourcePath موجود -> نستخدمه للتحقق
                if (!string.IsNullOrEmpty(resourcePath))
                {
                    // استعمل host الTest أو الLive حسب الحاجة
                    // ملاحظة: resourcePath عادة يبدأ بـ /v1/...
                    var hyperPayHost = "https://test.oppwa.com"; // غيّره للـ production عند الحاجة
                    var verifyUrl = $"{hyperPayHost}{WebUtility.UrlDecode(resourcePath)}";

                    _logger.LogInformation("Calling HyperPay verify URL: {verifyUrl}", verifyUrl);

                    // لو لازم تكون معاك entityId أو Authorization header، ضفهم هنا:
                    // client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "<token>");

                    var verifyResponse = await client.GetAsync(verifyUrl);
                    var payload = await verifyResponse.Content.ReadAsStringAsync();

                    _logger.LogInformation("HyperPay verify response status: {status}. Body: {body}", verifyResponse.StatusCode, payload);

                    if (!verifyResponse.IsSuccessStatusCode)
                    {
                        ViewBag.Status = "error";
                        ViewBag.Message = $"Verification failed: {verifyResponse.StatusCode}";
                        ViewBag.RawResponse = payload;
                        if (HttpContext.Request.Host.Host.Contains("localhost"))
                        {
                            // أثناء التطوير: اعرض كل التفاصيل
                            return View("Result");
                        }
                        else
                        {
                            // في السيرفر الحقيقي: اخفي التفاصيل
                            ViewBag.SafeMode = true;
                            return View("Result");
                        }

                    }

                    // parse JSON (مثال — شكل الرد قد يختلف حسب مزوّد الدفع)
                    try
                    {
                        var json = JObject.Parse(payload);

                        // أمثلة لحقل الحالة (عدّل حسب الرد الحقيقي)
                        // ممكن تلاقي fields زي: result.code, result.description, id, payment.status, amount, etc.
                        var resultCode = json.SelectToken("result.code")?.ToString() ?? json.SelectToken("resultCode")?.ToString();
                        var paymentStatus = json.SelectToken("payment.status")?.ToString() ?? json.SelectToken("status")?.ToString();
                        var respId = json.SelectToken("id")?.ToString() ?? json.SelectToken("checkoutId")?.ToString();

                        ViewBag.RawResponse = json.ToString();
                        ViewBag.Status = paymentStatus ?? resultCode ?? "unknown";
                        ViewBag.Message = $"Verified via HyperPay. Status={ViewBag.Status} (resultCode={resultCode})";
                        ViewBag.ResponseJson = json;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to parse HyperPay verify response");
                        ViewBag.Status = "error";
                        ViewBag.Message = "Failed to parse verification response.";
                        ViewBag.RawResponse = payload;
                    }

                    if (HttpContext.Request.Host.Host.Contains("localhost"))
                    {
                        // أثناء التطوير: اعرض كل التفاصيل
                        return View("Result");
                    }
                    else
                    {
                        // في السيرفر الحقيقي: اخفي التفاصيل
                        ViewBag.SafeMode = true;
                        return View("Result");
                    }

                }

                // 2) حالة SAPTCO confirm: لو معانا ordernumber أو نريد التأكيد عبر endpoint خاص
                if (!string.IsNullOrEmpty(ordernumber))
                {
                    var sapBase = "https://fcpuat.saptco.com.sa";
                    var confirmUrl = $"{sapBase}/api/v1/payments/3ds/confirm";

                    _logger.LogInformation("Calling SAPTCO confirm URL: {confirmUrl} with ordernumber={order}", confirmUrl, ordernumber);

                    var bodyObj = new { ordernumber = ordernumber };
                    var content = new StringContent(JObject.FromObject(bodyObj).ToString(), Encoding.UTF8, "application/json");

                    // إذا السيرفر يتطلب cookies/session (من login سابق) أو headers معينة، ضفها هنا.
                    var confirmResponse = await client.PostAsync(confirmUrl, content);
                    var confirmBody = await confirmResponse.Content.ReadAsStringAsync();

                    _logger.LogInformation("SAPTCO confirm response status: {status}. Body: {body}", confirmResponse.StatusCode, confirmBody);

                    if (!confirmResponse.IsSuccessStatusCode)
                    {
                        ViewBag.Status = "error";
                        ViewBag.Message = $"Confirm endpoint returned {confirmResponse.StatusCode}";
                        ViewBag.RawResponse = confirmBody;
                        if (HttpContext.Request.Host.Host.Contains("localhost"))
                        {
                            // أثناء التطوير: اعرض كل التفاصيل
                            return View("Result");
                        }
                        else
                        {
                            // في السيرفر الحقيقي: اخفي التفاصيل
                            ViewBag.SafeMode = true;
                            return View("Result");
                        }

                    }

                    try
                    {
                        var json = JObject.Parse(confirmBody);
                        // عدّل المسارات حسب بنية رد SAPTCO:
                        var successFlag = json["success"]?.ToObject<bool?>() ?? null;
                        var respOrder = json["ordernumber"]?.ToString() ?? ordernumber;
                        ViewBag.Status = successFlag == true ? "paid" : "notpaid";
                        ViewBag.Message = $"SAPTCO confirm returned success={successFlag}";
                        ViewBag.ResponseJson = json;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to parse SAPTCO confirm response");
                        ViewBag.Status = "error";
                        ViewBag.Message = "Failed to parse confirm response.";
                        ViewBag.RawResponse = confirmBody;
                    }

                    if (HttpContext.Request.Host.Host.Contains("localhost"))
                    {
                        // أثناء التطوير: اعرض كل التفاصيل
                        return View("Result");
                    }
                    else
                    {
                        // في السيرفر الحقيقي: اخفي التفاصيل
                        ViewBag.SafeMode = true;
                        return View("Result");
                    }

                }

                // 3) لا معطيات كافية -> اعرض رسالة انتظار أو خطأ
                ViewBag.Status = "unknown";
                ViewBag.Message = "No resourcePath or ordernumber provided to verify the payment.";
                if (HttpContext.Request.Host.Host.Contains("localhost"))
                {
                    // أثناء التطوير: اعرض كل التفاصيل
                    return View("Result");
                }
                else
                {
                    // في السيرفر الحقيقي: اخفي التفاصيل
                    ViewBag.SafeMode = true;
                    return View("Result");
                }

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
