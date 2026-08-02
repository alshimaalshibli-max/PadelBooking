# PadelBooking

منصة عربية متجاوبة لحجز ملاعب البادل، تتكون من واجهة عميل ولوحة إدارة داخل تطبيق React واحد، مع Backend مبني على ASP.NET Core 8 وEntity Framework Core وSQLite.

## الوظائف الرئيسية

- عرض الأوقات المتاحة دون كشف أسماء الملاعب للعميل.
- تخصيص ملعب متاح عشوائيًا ومنع تعارض الحجوزات.
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

أسرار الإدارة وThawani غير محفوظة في المستودع. اضبطها في جلسة PowerShell قبل التشغيل:

```powershell
$env:AdminAuth__Username="<admin-username>"
$env:AdminAuth__Password="<strong-admin-password>"
$env:AdminAuth__JwtKey="<random-secret-key-at-least-32-bytes>"

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
- الأوقات الماضية والإغلاقات.
- ذرية الحجز الجماعي.
- الإغلاقات الجماعية ونطاق التواريخ.
- الإلغاء والدفع والإكمال.
- الفلاتر وPagination.

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

## ملاحظات الأمان

- لا توجد بيانات دخول أو مفاتيح دفع حقيقية داخل الكود.
- رمز الإدارة يُحفظ في `sessionStorage` ويُزال عند تسجيل الخروج.
- مسارات الإدارة فقط تتطلب دور `Admin`.
- إنشاء الحجز وعرض الأوقات والدفع متاحة للعميل دون حساب.
- يتحقق Backend من جلسة Thawani قبل تحديث حالة الدفع إلى `Paid`.
