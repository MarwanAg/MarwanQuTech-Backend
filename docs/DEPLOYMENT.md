# النشر: SmarterASP (API) + Vercel (الواجهة) + Hostinger (الدومين)

```
marwanqutech.com      →  Vercel      (Next.js: الموقع + لوحة التحكم)
api.marwanqutech.com  →  SmarterASP  (ASP.NET Core API + SQL Server + الصور)
```

الدومين الواحد ضروري: كوكي الجلسة (`SameSite=Strict`) يعمل بين `marwanqutech.com` و `api.marwanqutech.com`
لأنهما «نفس الموقع»، ولا يعمل بين `xxx.vercel.app` و `xxx.smarterasp.net`.

---

## الخطوة 2 — الـ API على SmarterASP

### 2.1 الحساب والموقع
1. سجّل في التجربة المجانية (60 يوماً، بدون بطاقة). التجربة **لا تشمل SMTP**.
2. من لوحة التحكم: **Websites → Add Site**. اختر أحدث إصدار .NET Core متاح (يجب أن يدعم .NET 10).
3. سيعطيك رابطاً مؤقتاً مثل `xxxx-001-site1.xtempurl.com` — نستخدمه للتجربة حتى نربط الدومين.

### 2.2 قاعدة البيانات
1. **Databases → MSSQL → Create Database**، واختر كلمة مرور قوية.
2. انسخ **Connection String** كما تعرضه اللوحة (فيها `Data Source=SQLxxxx.site4now.net` …). أضف في آخرها
   `;TrustServerCertificate=True` إن لم تكن موجودة.

### 2.3 ملف الأسرار (مرة واحدة)
1. انسخ `WebAPI/appsettings.Secrets.example.json` إلى ملف جديد باسم `appsettings.Secrets.json` **على جهازك، خارج المستودع**.
2. املأ القيم:
   - `ConnectionStrings:Database` ← من الخطوة 2.2
   - `Jwt:SigningKey` و `NextJs:RevalidateSecret` ← قيمتان عشوائيتان مختلفتان. ولّدهما في PowerShell:
     ```powershell
     -join ((48..57)+(65..90)+(97..122) | Get-Random -Count 64 | % {[char]$_})
     ```
   - `AdminSeed` ← بريدك وكلمة مرور (12+ حرفاً، أحرف وأرقام). تُستخدم في أول تشغيل فقط.
   - `Smtp` ← اتركه فارغاً الآن (الخطوة 5).
3. احتفظ بنسخة من `RevalidateSecret` — ستضعها في Vercel أيضاً.

> الملف مستثنى من Git ومن Publish، فلا يُرفع إلى GitHub ولا يُستبدل عند كل نشر.

### 2.4 النشر من Visual Studio
1. من لوحة SmarterASP افتح الموقع ← **Deployment / Publish** ← فعّل **WebDeploy** وحمّل ملف
   **Publish Profile** (`.publishsettings`).
2. في Visual Studio: كليك يمين على **WebAPI** ← **Publish** ← **Import Profile** ← اختر الملف.
3. اضغط **Publish**.

### 2.5 رفع ملف الأسرار وتشغيل الموقع
1. من **File Manager** في لوحة SmarterASP، ارفع `appsettings.Secrets.json` إلى نفس مجلد `MarwanQuTech.WebApi.dll`.
2. امنح المجلد صلاحية **الكتابة** (Permissions → Write) حتى يُحفظ `wwwroot/uploads`.
3. أعد تشغيل الموقع (Restart / Recycle Application Pool).
4. افتح `https://<الرابط-المؤقت>/health` ← يجب أن يظهر `Healthy`.
   أول تشغيل يطبّق الـ Migrations وينشئ حساب الأدمن.

**إن ظهر خطأ 500.30 أو 502:** فعّل السجلات مؤقتاً في `web.config` على السيرفر
(`stdoutLogEnabled="true"`)، أعد التشغيل، وأرسل محتوى ملف السجل من مجلد `logs`.

---

## الخطوة 3 — الواجهة على Vercel
1. سجّل في Vercel بحساب GitHub ← **Add New → Project** ← اختر `MarwanQuTech-Frontend` ← Framework: **Next.js**.
2. **Environment Variables** (Production):
   | المتغير | القيمة |
   |---|---|
   | `NEXT_PUBLIC_API_URL` | `https://api.marwanqutech.com` |
   | `API_INTERNAL_URL` | `https://api.marwanqutech.com` |
   | `NEXT_PUBLIC_SITE_URL` | `https://marwanqutech.com` |
   | `REVALIDATE_SECRET` | نفس `NextJs:RevalidateSecret` من ملف الأسرار |
3. **Deploy**. بعدها كل `git push` على `main` يُنشر تلقائياً.

> المتغيرات التي تبدأ بـ `NEXT_PUBLIC_` تُدمج وقت البناء: بعد تغييرها اعمل **Redeploy**.

---

## الخطوة 4 — ربط الدومين (Hostinger)
1. **Vercel** ← Project → Settings → Domains ← أضف `marwanqutech.com` و `www.marwanqutech.com`.
   سيعرض Vercel السجلات المطلوبة (عادةً `A` للجذر و `CNAME` لـ `www`).
2. **SmarterASP** ← Site → Domains / Bindings ← أضف `api.marwanqutech.com`. سيعرض العنوان المطلوب
   (`A` أو `CNAME`).
3. **Hostinger** ← Domains → DNS / Nameservers ← أضف السجلات كما عرضها Vercel و SmarterASP حرفياً.
4. انتظر انتشار DNS (من دقائق إلى ساعات).
5. فعّل **SSL** المجاني لـ `api.marwanqutech.com` من لوحة SmarterASP (Vercel يفعّله تلقائياً).
6. تحقق: `https://api.marwanqutech.com/health` ← `Healthy`، ثم `https://marwanqutech.com/admin` ← تسجيل الدخول.

---

## الخطوة 5 — البريد (SMTP)
في `appsettings.Secrets.json` على السيرفر:
- **Gmail:** فعّل التحقق بخطوتين ← أنشئ **App Password** ← `Host: smtp.gmail.com`، `Port: 587`،
  `Username`: بريدك، `Password`: كلمة مرور التطبيق، `FromAddress`: نفس بريدك.
- أو بريد SmarterASP بعد الاشتراك المدفوع.

الإرسال يتم في الخلفية؛ الزائر لا ينتظر خادم البريد. الأخطاء تُسجَّل في السجلات.

---

## ما يعمل بشكل مختلف على الاستضافة المشتركة
- **لا يوجد Redis**: الكاش وعدّاد المشاهدات في الذاكرة. المشاهدات تُحفظ في قاعدة البيانات كل دقيقة
  وعند إيقاف التطبيق.
- **التطبيق ينام** بعد فترة بلا زيارات؛ أول طلب بعدها أبطأ ببضع ثوانٍ. صفحات الموقع نفسها مخزّنة في Vercel
  فلا يتأثر الزوار غالباً.
- **Swagger مغلق في الإنتاج.** لتفعيله مؤقتاً: `"WebApi": { "EnableSwagger": true }` في ملف الأسرار.

## نسخ احتياطي
- قاعدة البيانات: **Databases → Backup** في لوحة SmarterASP.
- الصور: نزّل مجلد `wwwroot/uploads` من File Manager.
