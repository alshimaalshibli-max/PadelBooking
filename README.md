# PadelBooking

منصة عربية متجاوبة لحجز ملاعب البادل، تتكون من واجهة عميل ولوحة إدارة داخل تطبيق React واحد، مع Backend مبني على ASP.NET Core 8 وEntity Framework Core وSQLite.

## الوظائف الرئيسية

- عرض الأوقات المتاحة دون كشف أسماء الملاعب للعميل.
- تخصيص ملعب متاح عشوائيًا ومنع تعارض الحجوزات، مع تثبيت السعر عبر عرض سعر مشفّر قصير الصلاحية.
- حجز ساعة أو عدة ساعات وعدة مواعيد أو أيام ضمن عملية ذرية واحدة.
- الدفع عند الوصول أو إنشاء جلسة دفع عبر Thawani Sandbox.
- إدارة الملاعب والأسعار وساعات العمل.
- إدارة العروض حسب عدد الساعات.
- إغلاقات عامة أو لعدة ملاعب عبر نطاق تواريخ وأيام أسبوع محددة.
- فلترة الحجوزات مع Pagination وتحديث حالات الحجز والدفع.
- حماية لوحة الإدارة باستخدام JWT.

## التقنيات

- .NET 8 / ASP.NET Core Web API
- Entity Framework Core 8
- SQLite
- xUnit وWebApplicationFactory لاختبارات التكامل
- React 19
- Vite 8
- CSS متجاوب وRTL دون مكتبات تصميم خارجية

## المتطلبات

- .NET SDK 8
- Node.js حديث متوافق مع Vite 8
- npm

## إعداد Backend

أسرار الإدارة وThawani غير محفوظة في كود التطبيق. اضبطها في جلسة PowerShell قبل التشغيل.

### بيانات دخول Demo للتقييم المحلي

- اسم المستخدم: `admin`
- كلمة المرور: `PadelDemo-2026!`

هذه بيانات تجريبية مقترحة للتشغيل المحلي فقط، وليست إعدادات افتراضية داخل التطبيق. غيّرها عند أي نشر حقيقي.

```powershell
$env:AdminAuth__Username="admin"
$env:AdminAuth__Password="PadelDemo-2026!"
$env:AdminAuth__JwtKey="Local-Demo-Jwt-Key-Change-For-Any-Deployment-2026"
$env:BookingQuotes__EncryptionKey="Local-Demo-Quote-Key-Change-For-Any-Deployment-2026"

# Required only for electronic payment testing.
$env:Thawani__SecretKey="<sandbox-secret-key>"
$env:Thawani__PublishableKey="<sandbox-publishable-key>"

dotnet run --project backend\PadelBooking.API\PadelBooking.API.csproj
```

يعمل API افتراضيًا على `http://localhost:5018` حسب ملف التشغيل، وتوجد Swagger في `/swagger` عند استخدام بيئة Development.

عند غياب إعدادات الإدارة تظل مسارات العميل العامة تعمل، بينما ترفض لوحة الإدارة تسجيل الدخول بأمان.

## إعداد Frontend

```powershell
Set-Location frontend
Copy-Item .env.example .env.local
npm install
npm run dev
```

المتغير الوحيد الذي يصل إلى كود المتصفح هو:

```text
VITE_API_BASE_URL=http://localhost:5018/api
```

لا تضع كلمة مرور الإدارة أو مفاتيح Thawani في أي متغير يبدأ بـ`VITE_`، لأن هذه المتغيرات تصبح مرئية داخل حزمة المتصفح.

## بناء المشروع

```powershell
dotnet build backend\PadelBooking.API\PadelBooking.API.csproj
dotnet build backend\PadelBooking.API.Tests\PadelBooking.API.Tests.csproj

Set-Location frontend
npm install
npm run build
```

## تشغيل الاختبارات

```powershell
dotnet test backend\PadelBooking.API.Tests\PadelBooking.API.Tests.csproj
```

تستخدم الاختبارات SQLite داخل الذاكرة من خلال `Data Source=:memory:`. لا تتصل الاختبارات بملف `padelbooking.db` ولا تعدّل بياناته.

تشمل الاختبارات:

- تسجيل دخول الإدارة ورفض البيانات الخاطئة.
- حماية المسارات الإدارية.
- الحجز الصحيح والتعارض والعروض.
- التوزيع العشوائي وثبات السعر النهائي باستخدام عرض السعر المشفّر.
- الأوقات الماضية والإغلاقات.
- ذرية الحجز الجماعي.
- الإغلاقات الجماعية ونطاق التواريخ.
- الإلغاء والدفع والإكمال.
- الفلاتر وPagination.
- صيغة إنشاء جلسة Thawani والتحقق من المبلغ والعملة والمرجع.
- إلغاء الحجز غير المدفوع عند تعذر إنشاء جلسة Thawani.

## إعداد Thawani Sandbox

يستخدم Backend القيم التالية، ويمكن ضبطها عبر متغيرات البيئة ذات الصيغة نفسها باستخدام فاصل `__`:

- `Thawani:SecretKey`
- `Thawani:PublishableKey`
- `Thawani:ApiBaseUrl`
- `Thawani:CheckoutBaseUrl`
- `Thawani:SuccessUrl`
- `Thawani:CancelUrl`

يجب أن تشير روابط النجاح والإلغاء إلى:

- `/payment/success`
- `/payment/cancel`

يحوّل Backend الريال العُماني إلى البيسة (`1 OMR = 1000`) ويتحقق من أن مبلغ الجلسة وعملتها ومرجعها تطابق الحجوزات قبل تحديث حالة الدفع. إذا تعذر إنشاء جلسة الدفع، تُلغى الحجوزات غير المدفوعة حتى لا تبقى المواعيد محجوزة دون دفع.

## ملاحظات الأمان

- لا توجد بيانات دخول أو مفاتيح دفع حقيقية داخل الكود.
- قيم Demo الواردة في README مخصصة للتقييم المحلي ويجب استبدالها في أي نشر فعلي.
- رمز الإدارة يُحفظ في `sessionStorage` ويُزال عند تسجيل الخروج.
- مسارات الإدارة فقط تتطلب دور `Admin`.
- إنشاء الحجز وعرض الأوقات والدفع متاحة للعميل دون حساب.
- يتحقق Backend من جلسة Thawani قبل تحديث حالة الدفع إلى `Paid`.
