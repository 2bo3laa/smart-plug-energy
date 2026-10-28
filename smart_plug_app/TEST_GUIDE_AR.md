# دليل تجربة التطبيق بدون هاردوير

> **مهم:** استخدم الباك اند المعدّل `SmartEnergyApi-net10-fixed.zip` (مش القديم).
> القديم كان ناقصه تسجيل الـ services في `Program.cs` فكان هيفشل عند أول Login،
> وكان بيكرر التنبيهات كل 5 ثواني، وجدول الشرائح كان فاضي.

## 1) شغّل الباك اند
```
cd SmartEnergyApi
dotnet run
```
لازم يطلع: `Now listening on http://0.0.0.0:4000`

## 2) جهّز التطبيق (مرة واحدة)
جوه فولدر `smart_plug_app`:
- ويندوز: دوس دبل كليك على `setup.bat`  (أو شغّله من الـ terminal)
- ماك/لينكس: `./setup.sh`

ده بيعمل `flutter create` ويظبط إذن الإنترنت وHTTP لأندرويد لوحده.

## 3) شغّل التطبيق
```
flutter run
```
- **Android Emulator**: شغال على طول (العنوان الافتراضي `10.0.2.2:4000`).
- **موبايل حقيقي**: الموبايل والكمبيوتر على نفس الواي فاي، وبعد ما التطبيق يفتح:
  Settings ← Server Address ← `http://<IP الكمبيوتر>:4000`
  (وافتح بورت 4000 في الـ Firewall لو ويندوز منعه).

## 4) اعمل حساب
في شاشة اللوجين اسحب حبل اللمبة ← Sign Up. أول ما تسجّل بيتعمل لك تلقائيًا
3 أجهزة تجريبية + تاريخ 48 ساعة + تنبيهات + جداول، فالإحصائيات والـ Alerts مش هتبقى فاضية.

## 5) شغّل الجهاز الوهمي (بدل الـ ESP32)
في terminal تاني، من فولدر `smart_plug_app`:
```
python esp32_simulator.py --email حسابك@mail.com --password كلمة_السر --db "المسار\SmartEnergyApi\smart_energy.db"
```
هتشوف سطور `OK ... W` كل 5 ثواني. في التطبيق: القياسات في Home بتتغير لايف.
- اضغط زرار الـ Power في التطبيق ← السكريبت يطبع `relay command received` والقراءة تنزل لصفر.
- زوّد `--spike` عشان كل دقيقة تحصل قراءة عالية (2300W) تجرّب بيها تنبيه الـ Threshold (الجهاز 01 عنده حد 1500W).

## 6) قائمة تجربة سريعة
- [ ] Sign Up / Log In / Log Out / Forgot Password
- [ ] Home: القياسات بتتحدث، الـ toggle شغال، Pull-to-refresh
- [ ] Device Detail: تعديل الاسم والغرفة، حذف جهاز، تعديل Power Threshold
- [ ] Schedule: إضافة / تعديل / تفعيل / حذف بالسحب
- [ ] Alerts: الفلاتر، علامة مقروء، Mark all، حذف بالسحب
- [ ] Statistics: Day / Week / Month / Year
- [ ] Add Device: Scan ← Setup ← الجهاز يظهر في Devices
- [ ] Profile: تعديل الاسم، تغيير كلمة السر، Server Address

## 7) الجديد (قائمة تجربة إضافية)
- [ ] Add Device ← خانة **Rated Power (Watts)** (اختيارية)
- [ ] Device Detail ← صف **Rated Power** ← تعديل القيمة (فاضي = مسح)
- [ ] Statistics ← كارت **Month Forecast** (توقّع استهلاك وتكلفة آخر الشهر + الشريحة الحالية)
- [ ] Statistics ← **Tariff →** أو Settings ← **Electricity Tariff** (الشرائح السبع والشريحة اللي انت فيها)
- [ ] شغّل السكريبت بـ `--spike`: لازم يجيلك تنبيه واحد بس كل 10 دقايق للجهاز (مش كل 5 ثواني)
- [ ] لما القراءة تعدّي 120% من الـ Rated Power بيظهر تنبيه "Above rated power"
- [ ] لما التوقّع يدخّلك شريحة أغلى بيظهر تنبيه "Approaching a higher tariff tier" (مرة كل 24 ساعة)

## ملاحظات
- الجدولة (Schedules) بتتخزن وبتتعدّل من التطبيق، لكن مفيش في الباك اند مؤقّت بينفّذها تلقائيًا؛ ده مش من التطبيق.
- بعد ما تعمل الهاردوير الحقيقي: استخدم نفس الـ DeviceKey (من جدول Devices) في `esp32_firmware.ino` بدل السكريبت.
