# 🐱📡 سند جامع تحویل پروژه Meowgnal (Master Handover Document)
**نسخه ۴ — مرداد ۱۴۰۵ (Aug 2026)**

این سند جایگزین تمام سندهای قبلی است و وضعیت واقعی + نقشه‌راه نهایی‌شده‌ی پروژه را منعکس می‌کند.

---

## ۰. دستورالعمل ویژه برای هوش مصنوعی در چت جدید

1. همیشه **کد کامل و قابل کامپایل** برای Visual Studio بده — هرگز اسنیپت ناقص یا `// TODO` نده. اگر فایلی باید عوض شود، کل فایل را بازنویسی کن.
2. برای **تغییرات کوچک** از «پروتکل جست‌وجو و جایگزینی» استفاده کن: مسیر دقیق فایل + یک عبارت یکتا برای Ctrl+F + نمایش کنارهم «متن قدیمی» و «متن جدید».
3. برای **تغییرات گسترده** کل فایل را بده.
4. تمام توضیحات فنی به **فارسی ساده و روان**.
5. دستورالعمل‌ها فقط برای **Visual Studio** (منوها / Solution Explorer / پنل Git Changes) — نه VS Code، نه ترمینال.
6. **قبل از هر تغییر بزرگ معماری، اول بحث و توافق، بعد کد**.
7. کدها را در **بلاک‌های معمولی** بده — بدون `<details>` یا جعبه‌ی جمع‌شونده (در اپ چت کاربر کار نمی‌کند).
8. **سبک پیام کامیت: ساده و بدون پیشوند فاز.** مثال: `add multi-watchlist panel with live prices`.
9. فایل‌های `MainWindow.xaml` و `MainWindow.xaml.cs` بسیار بزرگ‌اند (cs حدود ۱۹۰۰+ خط). **قبل از هر تغییری روی این دو فایل، از کاربر بخواه آخرین نسخه‌ی فعلی آن‌ها را پیست کند** تا روی نسخه‌ی واقعی کار شود.
10. **پالت رنگ TradingView Dark** را در همه‌ی UIهای جدید/بازطراحی‌شده رعایت کن (جدول بخش ۴).
11. **«آزمون فرزاد»** معیار سادگی است: اگر کاربر (غیر برنامه‌نویس و غیر تریدر حرفه‌ای) بدون توضیح نتواند صفحه‌ای را استفاده کند، باید بازطراحی شود.
12. اگر کاربر فایلی آپلود کرد و محتوا نرسید، بخواه متن را پیست کند یا کل فایل را بده.
13. **اصل هویت محصول:** Meowgnal صرفاً ابزار تحلیل نیست؛ ربات/اتوماسیونی است که با پارامترها سیگنال خرید/فروش می‌دهد و سرمایه را با SL/TP مدیریت می‌کند.
14. **اصل معماری:** «یک هسته‌ی اجرایی، چند درِ ورود» — قالب‌ها / جادوگر / ویرایشگر پیشرفته / دستیار چتی همگی به یک هسته وصل می‌شوند.

---

## ۱. نقشه‌راه پروژه (Project Roadmap & Milestones)

### چشم‌انداز کلی
اپ دسکتاپ ویندوز (C#/.NET WPF) برای رصد بازار کریپتو بر اساس استراتژی تکنیکال که کاربر بدون کدنویسی می‌سازد؛ سیگنال ورود/خروج + حدضرر + تارگت می‌دهد و **هرگز معامله‌ی واقعی نمی‌کند**. بلندمدت: فارکس/سهام/کالا + مدل تجاری اشتراکی. شعار: «هلو بپر تو گلو».

### ✅ فازهای کاملاً تکمیل‌شده (۱ تا ۲۹)

| # | فاز | وضعیت |
|---|---|---|
| 1 | برنامه‌ریزی معماری + تصمیمات فنی/تجاری/امنیتی | ✅ |
| 2 | اسکلت WPF + اتصال گیت‌هاب (Private) | ✅ |
| 3 | مدل داده‌ی استراتژی (StrategyDefinition، ConditionNode درختی) | ✅ |
| 4 | ذخیره‌سازی رمزنگاری‌شده DPAPI | ✅ |
| 5 | اتصال زنده به Binance + Hyperliquid (بدون API Key) | ✅ |
| 6 | موتور اندیکاتور (FacioQuo) | ✅ |
| 7 | موتور ارزیابی قوانین (RuleEngine با حالت‌های all/any/threshold) | ✅ |
| 8 | موتور بک‌تست (BacktestEngine) | ✅ |
| 9 | داشبورد اصلی (چارت + سیگنال + کارت‌ها) | ✅ |
| 10 | نمودار کندل‌استیک اولیه (LiveCharts2 — جایگزین شد) | ✅ |
| 11 | استراتژی‌ساز (رابط کلیکی بدون کد) | ✅ |
| 12 | صفحه‌ی بک‌تست کامل (Equity Curve + جدول معاملات) | ✅ |
| 13 | صفحه‌ی تنظیمات (منبع، API با هشدار Read-Only، اعلان‌ها) | ✅ |
| 14 | تایم‌فریم کلیک‌پذیر + فول‌اسکرین+Esc + اسکرین‌شات Save As | ✅ |
| 15 | پن/زوم چارت (LiveCharts2 — جایگزین شد) | ✅ |
| 16 | نوار OHLC بالای چارت با رنگ‌بندی جدا | ✅ |
| 17 | سوییچ چارت به WebView2 + TradingView Lightweight Charts | ✅ |
| 18 | انواع چارت (Candles/Line/Area/HeikinAshi/Bars) + منوی آیکونی | ✅ |
| 19 | عمق تاریخچه با صفحه‌بندی (۱۰۰۰ کندل چارت / ۲۰۰۰ بک‌تست / ۵۰۰ اسکن) | ✅ |
| 20 | منوی کامل تایم‌فریم با ستاره (حداکثر ۶) + گروه‌بندی + سکشن‌های جمع‌شونده + اسکرول هوشمند | ✅ |
| 21 | تب‌های چارت (هر جفت‌ارز در تب خودش + ＋ + نرمال‌سازی نماد) | ✅ |
| 22 | اطلاع‌رسانی واقعی پس‌زمینه (تایمر + Windows Toast + صدا + فاصله‌ی قابل‌تنظیم) | ✅ |
| 23 | واچ‌لیست کامل (چند لیست یونیک + قیمت زنده ۵ ثانیه + انتخاب منبع Binance/Hyperliquid با پیش‌نمایش زنده دوصرافی) | ✅ |
| 24 | بازطراحی پوسته‌ی MainWindow به پالت TradingView Dark | ✅ |
| 25 | هم‌رنگ‌سازی SettingsWindow/StrategyBuilderWindow/BacktestWindow با پالت TV | ✅ |
| 26 | حساب کاغذی: مدل‌های PaperAccount + PaperAccountStorageService + تب Paper در Settings | ✅ |
| 27 | ترید دستی: کنترل Margin + Leverage + SL/TP + Trailing Stop + دکمه‌ی ⏹ Close + PnL زنده در تب و Status Bar | ✅ |
| 28 | ترید خودکار از سیگنال‌ها (Entry→LONG خودکار، Exit→بستن) + خطوط Entry/SL/TP/LIQ و مارکر فلش روی چارت (پیام setPositions) | ✅ |
| 29 | ساعت کلیکی نوار پایین با لیست کامل مناطق زمانی مرتب‌شده بر اساس آفست (UTC/Local/Custom) | ✅ |

**آخرین کامیت:** `add clickable status-bar clock with full timezone picker`

### ⏳ فازهای نهایی‌شده و آماده‌ی اجرا

#### فاز ۳۰ — موتور رسم (Drawing Tools) «دست‌ها»
- ابزارها: خط افقی، خط روند، فیبوناچی ریتریسمنت (۰/۲۳.۶/۳۸.۲/۵۰/۶۱.۸/۷۸.۶/۱)
- ذخیره‌ی زمان‌محور (روی همه‌ی تایم‌فریم‌های همان نماد دیده می‌شود) در `drawings.dat` رمزنگاری‌شده
- تولبار WPF (منوی کشویی کنار دکمه‌ی نوع چارت) + Drawing Manager (مشاهده/حذف تکی/حذف همه)
- رندر در chart.html؛ پروتکل: C#→JS با `setDrawingMode` و `setDrawings` · JS→C# با `drawingCompleted`
- تشخیص خودکار حمایت/مقاومت «مهم»: بازه ۳۰۰ کندل، پیوت با ۵ کندل هر طرف، تلورانس ۰.۲۵٪ یا نصف ATR14، حداقل ۳ برخورد، حداکثر ۳ خط بالای قیمت + ۳ پایین؛ قابل تغییر توسط کاربر/چت
- هشدار سطح ۲: تیک «Alert on cross» برای هر رسم؛ بررسی در مانیتور پس‌زمینه و Toast/صدا فقط در لحظه‌ی عبور

#### فاز ۳۱ — استراتژی‌ساز سه‌لایه (توافق نهایی)
- تعریف جدید: استراتژی = برنامه‌ی معاملاتی کامل (ورود + خروج + برنامه‌ی ریسک)
- **لایه‌ی ۱ «فروشگاه قالب‌ها»** (درِ اصلی): کارت با نام + توضیح یک‌جمله‌ای ساده + نشان سبک/تایم‌فریم/ریسک + آمار بک‌تست + دکمه‌های Use و Customize (از روز اول)
- قالب‌های اولیه (۶ عدد با اندیکاتورهای فعلی): Fast Trend Rider (کراس EMA 9/21) · Golden Cross (SMA 50/200) · RSI Dip Buyer · MACD Momentum · Cautious Combo (کراس EMA + فیلتر RSI) · قالب نوسان با ATR
- **لایه‌ی ۲ «جادوگر ۳ سوالی»**: سبک (روند/برگشت/شکست) + سرعت (اسکالپ/سویینگ/بلندمدت) + احتیاط (کم‌ریسک/متعادل/تهاجمی) → ساخت خودکار
- **لایه‌ی ۳ «حالت پیشرفته»**: بازطراحی بیلدر فعلی به Sentence Builder پشت دکمه‌ی Advanced + رجیستری اندیکاتور (FacioQuo بیش از ۱۰۰ اندیکاتور دارد؛ افزایش تدریجی)
- زبان توضیح‌ها: انگلیسی ساده

#### فاز ۳۲ — دستیار چتی میوگنال 🐱
- آیکون گربه گوشه‌ی پایین چارت → چت‌باکس
- مغز: مدل زبانی ابری به‌عنوان «مترجم» به دستورات JSON (tool-call schema)؛ اپ هر دستور را قبل از اجرا اعتبارسنجی می‌کند
- ارائه‌دهنده‌های رایگان/سریع/دقیق: Google Gemini (پیش‌فرض) · Groq · OpenRouter · OpenAI · URL سفارشی؛ کلید API رمزنگاری‌شده در Settings؛ هزینه برای کاربر صفر
- حالت آفلاین «فرمان‌های سریع» بدون کلید (الگوهای ثابت) — رایگان و آنی
- **تایید اجباری برای هر عمل پولی** (باز/بستن پوزیشن، تغییر مارجین): کارت خلاصه + [بله]/[نه]؛ اعمال غیرپولی فوراً اجرا و گزارش می‌شوند
- لحن: دوستانه‌ی کوتاه با گاهی 🐾/میو — تست و تکرار
- ابزارهای v1: رسم خودکار S/R مهم با رنگ دلخواه کاربر · ساخت استراتژی M-از-N (حالت threshold در موتور موجود است) · تغییر تایم‌فریم/نوع چارت · توضیح ساده‌ی اندیکاتورها
- v2: حافظه‌ی سلیقه، مدیریت پوزیشن با چت، هشدار روی خطوط
- یادداشت تجاری: در دسته‌ی پ، «ابر خودمان + سهمیه‌ی دستیار» به‌عنوان اشتراک فروخته می‌شود

#### فاز ۳۳ — دسته‌ی پ (تجاری/امنیتی)
- لایسنس/قفل دمو (شناسه سخت‌افزاری + Cloudflare Workers/KV)
- درگاه پرداخت کریپتویی، ایمیل لایسنس، کد ردیم، سطح‌بندی Basic/Pro/Ultra
- ابر دستیار برای کاربران بدون کلید API

---

## ۲. مشخصات محیط توسعه و گیت (Environment & Source Control)

| مورد | مقدار |
|---|---|
| زبان/فریم‌ورک | C# / .NET 10.0 LTS |
| TargetFramework | `net10.0-windows10.0.19041.0` (برای API داخلی Toast ویندوز) |
| IDE | Visual Studio (نسخه کامل) — نه VS Code، نه ترمینال |
| مسیر ریشه | `C:\Users\fdoos\Desktop\Github\Meowgnal` |
| فایل پروژه | `C:\Users\fdoos\Desktop\Github\Meowgnal\Meowgnal\Meowgnal.csproj` |
| اکانت/ریپو | `meowrypto` / `github.com/meowrypto/Meowgnal` — **Private** |
| برنچ اصلی | `master` |
| commit/push | پنل داخلی Git Changes در Visual Studio |
| اکستنشن‌های VS | CodeMaid 2022، Roslynator 2022، XAML Styler 2022 |

### ساختار پوشه‌ای فعلی

```text
Meowgnal/
├── Meowgnal/
│   ├── ChartHost/
│   │   ├── chart.html                              (میزبان Lightweight Charts + setPositions)
│   │   └── lightweight-charts.standalone.production.js  (v4.2.3 لوکال)
│   ├── DataProviders/   IDataProvider.cs / BinanceDataProvider.cs / HyperliquidDataProvider.cs
│   ├── Engine/          IndicatorEngine.cs / RuleEngine.cs / BacktestEngine.cs
│   ├── Models/          Bar / ConditionNode / StrategyDefinition / BacktestResult /
│   │                    SignalDisplayItem / StrategyBuilderRows / AppSettings /
│   │                    WatchlistModels / PaperAccount (جدید)
│   ├── Services/        AppPaths / StrategyStorageService / SettingsStorageService /
│   │                    WatchlistStorageService / NotificationService /
│   │                    PaperAccountStorageService (جدید) / PaperTradingEngine (جدید)
│   ├── Views/           SignalTypeToBrushConverter / StrategyBuilderWindow / BacktestWindow /
│   │                    SettingsWindow (با تب Paper Trading)
│   ├── MainWindow.xaml(.cs)   (بسیار بزرگ — تب‌ها، منوها، واچ‌لیست، WebView2، Paper, ساعت کلیکی)
│   └── Meowgnal.csproj
```

**هر دو فایل ChartHost:** Build Action = **Content** و Copy to Output Directory = **Copy if newer**

### محتوای فعلی Meowgnal.csproj

<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWPF>true</UseWPF>
    <NoWarn>NU1701;IDE0028;CA1416</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="FacioQuo.Stock.Indicators" Version="3.0.0" />
    <PackageReference Include="LiveChartsCore" Version="2.0.5" />
    <PackageReference Include="LiveChartsCore.SkiaSharpView" Version="2.0.5" />
    <PackageReference Include="LiveChartsCore.SkiaSharpView.WPF" Version="2.0.5" />
    <PackageReference Include="Microsoft.Web.WebView2" Version="1.0.4129.50" />
  </ItemGroup>
</Project>   


### فایل‌های کانفیگ (همه رمزنگاری‌شده DPAPI — بدون کلید واقعی در سند)

- `%AppData%\Meowgnal\settings.dat`
- `%AppData%\Meowgnal\Strategies\*.mgstrat`
- `%AppData%\Meowgnal\watchlists.dat`
- `%AppData%\Meowgnal\paper_account.dat` (جدید)
- `%AppData%\Meowgnal\alert.wav` (ساخته‌شده خودکار در اولین پخش صدا)
- `%AppData%\Meowgnal\drawings.dat` (آینده — فاز ۳۰)

### پالت رسمی TradingView Dark (اجباری برای UIهای جدید)

  

|توکن|مقدار||توکن|مقدار|
|---|---|---|---|---|
|background|#131722||accent (آبی)|#2962FF|
|panel (BgPanel)|#1E222D||up (سبز)|#089981|
|border|#2A2E39||down (قرمز)|#F23645|
|hover|#363A45||فونت|Trebuchet MS|
|textPrimary|#D1D4DC||textSecondary|#B2B5BE|
|textMuted|#787B86||||


## ۳. خلاصه اجرایی و هدف اپلیکیشن

**هدف:** دستیار سیگنال‌دهی کریپتو برای کاربرانی که بدون کدنویسی استراتژی می‌سازند؛ بدون اجرای خودکار معامله.

**معماری:** `DataProviders` → `Engine` → `Models` → `Services` → `Views`/`MainWindow` (WPF).

**مخاطب:** سه سطح مبتدی/متوسط/حرفه‌ای — با سه لایه‌ی ورود (قالب‌ها، جادوگر، پیشرفته) + دستیار چتی.

|پکیج|نسخه|کاربرد فعلی|
|---|---|---|
|FacioQuo.Stock.Indicators|3.0.0|EMA/SMA/RSI/ATR/MACD (+ ۱۰۰ اندیکاتور دیگر برای آینده)|
|Microsoft.Web.WebView2|1.0.4129.50|میزبان TradingView Lightweight Charts|
|LiveChartsCore (+SkiaSharpView +WPF)|2.0.5|فقط Equity Curve بک‌تست|


## ۴. تاریخچه تغییرات، مسیرهای ردشده و باگ‌های حل‌شده

### تصمیمات ردشده (دیگر پیشنهاد نشوند)

- ❌ Skender.Stock.Indicators → ✅ FacioQuo.Stock.Indicators
- ❌ .NET 8/9 → ✅ .NET 10 LTS
- ❌ VS Code + ترمینال → ✅ Visual Studio (پروژه یک‌بار از صفر بازسازی شد)
- ❌ export استراتژی توسط کاربر → فقط بک‌آپ رمزنگاری‌شده
- ❌ لایسنس متن‌باز → سورس Private
- ❌ خروجی JSON برای MetaTrader → آینده‌ی دور
- ❌ کپی از مستندات Pine Script → رد (کپی‌رایت)؛ ✅ کتابخانه‌ی متن‌باز Lightweight Charts مجاز است (با نوتیس اجباری)
- ❌ پیش‌فرض Mode=threshold/MinScore=3 → ✅ Mode=all (باگ صفر معامله)
- ❌ پکیج CommunityToolkit.Notifications → ✅ API داخلی ویندوز با TargetFramework نسخه‌دار (بدون وابستگی اضافه)
- ❌ namespace `Microsoft.Web.WebView2.WPF` → ✅ `Microsoft.Web.WebView2.Wpf` (حساس به بزرگی/کوچکی حروف!)
- ❌ بلاک کد جمع‌شونده `<details>` → در اپ چت کاربر کار نمی‌کند؛ فقط بلاک معمولی
- ❌ GitHub Copilot Coding Agent برای جایگزینی جریان فعلی → کاربر ادامه‌ی همین جریان (AI + پیست دستی) را انتخاب کرد
- ❌ مدل زبانی لوکال برای دستیار → رد (سنگین/کم‌دقت)؛ انتخاب: ابر رایگان + فرمان‌های آفلاین

### باگ‌های مهم حل‌شده

|باگ|راه‌حل|
|---|---|
|ProtectedData با entropy خطا|آرگومان بدون نام پاس داده شود|
|CS0104 'Bar' ambiguous|`using Bar = Meowgnal.Models.Bar;` در Engine|
|SkiaSharp load نشد|حذف bin/obj + Rebuild|
|XAML داخل .cs پیست شد|جداسازی به فایل درست|
|بک‌تست ۰ معامله|تغییر پیش‌فرض به Mode=all|
|MC3074 WebView2 not found|namespace صحیح `...WebView2.Wpf`|
|CapturePreviewAsync ارور آرگومان|ترتیب `(CoreWebView2CapturePreviewImageFormat.Png, stream)`|
|XDG0008 (ارور ظاهری دیزاینر)|با Build موفق خودبه‌خود رفع می‌شود|
|InvalidOperationException در پیام JSON WebView2|اگر `RootElement.ValueKind == String` بود یک لایه unwrap کن|
|`new Thickness(10, 5)` نامعتبر|فرم چهارتایی `new Thickness(10, 5, 10, 5)`|
|منوی تایم‌فریم از پنجره بیرون می‌زد|`FitTimeframeMenuToWindow` با `TranslatePoint` نسبت به کف پنجره|
|EndsWith("s") هشدار|استفاده از `EndsWith('s')`|
|`new SKPathEffect()` بدون آرگومان|حذف PathEffect از SeparatorsPaint در BacktestWindow|
|تعریف دوباره‌ی `PaperAccountFile` در AppPaths|یک تعریف واحد|
|به‌هم‌ریختگی فایل‌ها با جایگزینی‌های متعدد|پروتکل «کل فایل» برای فایل‌های بزرگ|
|آپلود فایل فقط عنوان را می‌رساند|درخواست پیست متن یا ارسال کل فایل|
|هشدارهای پلتفرمی CA1416 LiveCharts/SkiaSharp|`<NoWarn>$(NoWarn);CA1416</NoWarn>` در csproj|

---

## ۵. وضعیت فعلی کدها (Current Codebase)

### Models (فایل‌های کلیدی)

#### Models/AppSettings.cs

namespace Meowgnal.Models;

public sealed class AppSettings
{
    public string DefaultDataSource { get; set; } = "binance";
    public string BinanceApiKey { get; set; } = "";
    public string BinanceApiSecret { get; set; } = "";
    public bool ToastNotificationsEnabled { get; set; } = true;
    public bool SoundNotificationsEnabled { get; set; } = true;
    public int SignalCheckIntervalSeconds { get; set; } = 60;
    public List<string> FavoriteTimeframes { get; set; } = new() { "15m", "1h", "4h", "1d", "1w", "1M" };

    // Paper Trading Settings
    public decimal PaperStartingBalance { get; set; } = 10000m;
    public bool PaperUseRiskBasedSizing { get; set; } = true;
    public decimal PaperRiskPercentPerTrade { get; set; } = 2m;
    public decimal PaperPositionSizePercent { get; set; } = 10m;
    public decimal PaperDefaultLeverage { get; set; } = 10m;
    public decimal PaperDefaultStopLossPercent { get; set; } = 2m;
    public decimal PaperDefaultTakeProfitPercent { get; set; } = 4m;
    public decimal PaperMaxDailyLossPercent { get; set; } = 5m;
    public int PaperMaxOpenPositions { get; set; } = 5;
    public decimal PaperTakerFeePercent { get; set; } = 0.04m;
    public bool PaperAutoTradeEnabled { get; set; } = true;

    // Status-bar clock
    public string ClockMode { get; set; } = "utc";
    public string ClockTimeZoneId { get; set; } = "";
}


#### Models/PaperAccount.cs (خلاصه ساختار)

namespace Meowgnal.Models;

public enum PositionSide { Long, Short }

public enum CloseReason
{
    TakeProfit, StopLoss, Liquidation, Manual, SignalExit, RiskRule, TrailingStop
}

public class PaperPosition
{
    public string PositionId { get; set; } = Guid.NewGuid().ToString("N");
    public string Symbol { get; set; } = "";
    public string DataSource { get; set; } = "binance";
    public PositionSide Side { get; set; } = PositionSide.Long;
    public decimal EntryPrice, Size, Leverage, Margin;
    public decimal StopLoss, TakeProfit, LiquidationPrice;
    public bool TrailingEnabled;
    public decimal TrailingDistancePercent, TrailingActivationPercent;
    public decimal TrailingCurrentStop, HighestPriceSinceEntry, LowestPriceSinceEntry;
    public DateTime OpenTime;
    public decimal EntryFee;
    public string? StrategyId;

    public decimal UnrealizedPnL(decimal currentPrice, decimal takerFeePercent) { /* ... */ }
    public decimal UnrealizedRoiPercent(decimal currentPrice, decimal takerFeePercent) { /* ... */ }
}

public class PaperTrade { /* نسخه‌ی بسته‌شده‌ی PaperPosition با ExitPrice/PnL/Reason */ }

public class PaperAccountFile
{
    public decimal StartingBalance, CurrentBalance;
    public List<PaperPosition> OpenPositions;
    public List<PaperTrade> TradeHistory;
    public DateTime DailyResetDate;
    public decimal DailyRealizedPnL;
    public bool IsSuspendedUntilTomorrow;
}


### Services (فایل‌های کلیدی)

#### Services/AppPaths.cs

using System;
using System.IO;

namespace Meowgnal.Services;

public static class AppPaths
{
    public static string AppDataFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Meowgnal");

    public static string StrategiesFolder => Path.Combine(AppDataFolder, "Strategies");
    public static string WatchlistsFile => Path.Combine(AppDataFolder, "watchlists.dat");
    public static string PaperAccountFile => Path.Combine(AppDataFolder, "paper_account.dat");
}

#### Services/PaperTradingEngine.cs (خلاصه رفتارهای فنی)

- **Liquidation:** Long = `entry×(1−1/lev+0.005)` · Short = `entry×(1+1/lev−0.005)`
- **بررسی استاپ‌ها هر ۵ ثانیه** با High/Low کندل ۱ دقیقه‌ای + قیمت آخر؛ ترتیب محافظه‌کارانه: Liquidation → SL → TP (برخورد همزمان = SL اول)
- **Trailing Stop** فقط در جهت سود جلو می‌رود؛ فعال‌شدن پس از X٪ سود
- **ریسک روزانه:** عبور از MaxDailyLoss → بستن همه + تعلیق تا فردای UTC
- **سقف پوزیشن همزمان:** PaperMaxOpenPositions
- **M-از-N:** RuleGroup با Mode=threshold و MinScore
- **Auto-trade:** Entry → LONG خودکار (اگر پوزیشن نداشته باشد) · Exit → بستن پوزیشن همان نماد · تگ StrategyId
- **ساعت:** `TimeZoneInfo.GetSystemTimeZones()` مرتب با BaseUtcOffset + ConvertTimeFromUtc

### Views

|   |   |   |
|---|---|---|
|فایل|مسئولیت|وضعیت|
|SignalTypeToBrushConverter.cs|رنگ‌های TV (#089981/#F23645) برای سیگنال‌ها|✅|
|StrategyBuilderWindow.xaml(.cs)|فرم ساخت استراتژی — تم TV|✅ (فاز ۳۱ تبدیل به لایه ۳)|
|BacktestWindow.xaml(.cs)|بک‌تست با limit=2000 — تم TV|✅|
|SettingsWindow.xaml(.cs)|تنظیمات شامل تب Paper Trading (Account Setup/Position Sizing/SL-TP/Risk Management/Danger Zone) + تب‌های Data sources/API/Notifications/License|✅|
|MainWindow.xaml(.cs)|تب‌ها، منوها، واچ‌لیست، WebView2، تب Paper، ساعت کلیکی منطقه‌ی زمانی — ~۱۹۰۰ خط|✅|

### ChartHost/chart.html

کد نهایی شامل هندل پیام‌های `setCandles`، `setChartType`، `setPositions` (رسم خطوط Entry/SL/TP/LIQ و مارکر فلش) + کراس‌هِیر OHLC برگشتی به C# است.

### MainWindow.xaml.cs (ساختار کلی)

فایل بزرگ شامل این بخش‌ها:

- Chart tabs (یک تب برای هر نماد)
- Watchlist با قیمت زنده ۵ ثانیه
- WebView2 + چارت
- Timeframe toolbar + منوی گروه‌بندی‌شده با ستاره
- Chart type dropdown با آیکون‌ها
- Background signal monitor
- **Paper trading** (manual + auto + live + risk rules)
- **Status-bar clock** (UTC/System/Custom با پاپ‌آپ)
- **SendPositionsToChartAsync** (انتقال پوزیشن‌ها به chart.html)

---

## ۶. کار در دست اقدام و گام بعدی (Immediate Action Item)

### آخرین کارهای تمام‌شده

- فاز ۲۹ (ساعت کلیکی با مناطق زمانی) — کامیت شده
- تمام بحث‌های طراحی برای فازهای بعدی نهایی شد:
    - سه‌لایه‌ی استراتژی (قالب‌ها/جادوگر/پیشرفته)
    - دستیار چتی (رایگان/سریع/دقیق + تایید پولی + لحن دوستانه)
    - موتور رسم با تشخیص S/R مهم

### گام بعدی: شروع کد فاز ۳۰ (موتور رسم)

**اولین کارها:**

1. دریافت آخرین نسخه‌ی `MainWindow.xaml` و `MainWindow.xaml.cs` از کاربر (قبل از هر تغییر روی این فایل‌ها)
2. ساخت مدل `Models/Drawing.cs` (کلاس‌های `Drawing`, `DrawingKind`, `DrawingsFile`)
3. ساخت `Services/DrawingStorageService.cs` (DPAPI → drawings.dat)
4. ساخت `Services/SupportResistanceDetector.cs` (الگوریتم تشخیص سطوح مهم)
5. افزودن تولبار رسم به MainWindow.xaml (منوی کشویی کنار دکمه‌ی نوع چارت)
6. افزودن هندل رسم به chart.html (کلیک اول/دوم + رندر)
7. پروتکل پیام: C#↔JS (`setDrawingMode`, `setDrawings`, `drawingCompleted`)

**کامیت پیشنهادی پس از تکمیل:**

add drawing tools with auto support-resistance detection


## ۷. جمع‌بندی قوانین کاری

1. کد کامل، نه اسنیپت
2. Ctrl+F برای تغییرات کوچک
3. کل فایل برای تغییرات گسترده
4. کامیت ساده بدون پیشوند فاز
5. فارسی ساده + Visual Studio + بدون ترمینال
6. پالت TV در همه‌ی UIهای جدید
7. توافق قبل از تغییر بزرگ
8. MainWindow را قبل از دست‌کاری از کاربر بخواه
9. آزمون فرزاد (سادگی)
10. بحث قدم‌به‌قدم برای موضوعات محصول

---

**پایان سند.**

💡 **نحوه‌ی استفاده از این سند:**

- **در چت جدید:** کل این سند را به‌عنوان اولین پیام پیست کن تا AI کاملاً در جریان پروژه قرار بگیرد
- **به PDF:** در Obsidian یا VS Code پیست کن → File → Export to PDF (فارسی کامل و درست رندر می‌شود)
