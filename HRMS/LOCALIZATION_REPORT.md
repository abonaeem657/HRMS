# تقرير إصلاح الترجمة

تم العمل في المسار الفعلي لمساحة العمل:
`C:\Users\NITRO\OneDrive - zu.edu.jo\Desktop\HRMS\HRMS\HRMS`
والحل `..\HRMS.sln` يحتوي المشاريع الأربعة المطلوبة. المسار المذكور في الطلب `C:\Users\us\Desktop\HRMS` ليس مسار هذه النسخة.

## العمليات والبناء

- كانت حالة Git نظيفة عند البداية. لم تُمسح تغييرات المستخدم أو تُغير إعدادات Git.
- العملية HRMS ذات الرقم 11072 لم تكن موجودة عند الفحص؛ لم تكن هناك نسخة HRMS أو IIS Express شغالة. عمليات dotnet run الموجودة كانت تخص UniSphere، وتركت دون تدخل. لذلك لم يمكن التحقق من هوية العملية التاريخية أو إعادة إنتاج قفل DLL السابق. رسالة القفل المقدمة تشير إلى نسخة التطبيق التي كانت تحمل DLL؛ القفل غير موجود في هذه البيئة الآن.
- تعثر البناء الأول بـ NU1301 بسبب الوصول إلى NuGet. تمت استعادة الحزم والبناء بصلاحيات الشبكة والكتابة اللازمة دون تغيير إعدادات المشروع أو حذف bin/obj.
- نجح البناء النهائي للحل الكامل: صفر أخطاء وصفر تحذيرات.
- شُغلت نسخة HRMS واحدة في كل مرة، وأُوقفت نسخ الاختبار عبر Ctrl+C. التشغيل المقيد لم يستطع الوصول إلى مفاتيح Windows Data Protection وسجل الأحداث؛ نجح التشغيل بصلاحيات Windows اللازمة دون تعديل المصدر لإخفاء المشكلة.

## السبب المثبت للترجمة

إعداد `ResourcesPath = ""` موجود بالفعل قبل التعديل، وهو يتوافق مع النوع `HRMS.Resources.SharedResource` وموقع ملفات الموارد. أعاد `IStringLocalizer<SharedResource>` النص العربي الصحيح في النسخة الحالية. لم تظهر مشكلة `SignIn` و`SkipToContent` التاريخية مع المصدر المبني هنا، ولا يمكن الجزم بسببها في النسخة السابقة دون فحصها.

المشكلة القابلة لإعادة الإنتاج كانت في DataAnnotations: تحديد `ResourceType` يوجه تسميات الحقول إلى الخصائص الثابتة في SharedResource، التي كانت تعيد اسم المفتاح نفسه، وبعض الخصائص المطلوبة غير موجودة أصلًا. أظهر التشخيص `T[LabelEmail]=البريد الإلكتروني` لكن `ModelMetadata.Email.DisplayName=LabelEmail`. كذلك كانت أسماء حقول كثيرة مثل `LabelFirstName` لا تطابق `Label.FirstName` في الموارد.

تم توجيه Display وValidation إلى `AddDataAnnotationsLocalization` المسجل بالفعل، وتوحيد مفاتيح التسميات مع الموارد. أُصلحت مفاتيح واجهة ناقصة، واستخدام ترجمة حالة الموظف داخل معاملات روابط الصفحات، ومفاتيح التوظيف المصطنعة للقيم المخصصة.

## الملفات المعدلة وسببها

| الملف | السبب |
|---|---|
| `Program.cs` | إزالة مسار التشخيص المؤقت `/__diagd` واستيراده؛ الحفاظ على تسجيل الموارد والثقافات وترتيب middleware الصحيح. |
| `Resources/SharedResource.cs` | إعادة النوع إلى marker للتوطين وإزالة الخصائص التي تعيد مفاتيح تقنية. |
| `Resources/SharedResourceExtensions.cs` | ترجمة قيم التوظيف المعروفة وإظهار القيمة المخصصة الأصلية عندما لا يوجد مورد لها، بدل إظهار مفتاح مصنوع. |
| `Resources/SharedResource.ar-JO.resx` | إضافة ستة موارد ناقصة بالعربية. |
| `Resources/SharedResource.en-US.resx` | إضافة الموارد الستة نفسها بالإنجليزية. |
| `Models/Identity/IdentityViewModels.cs` | تصحيح Display وValidation ومفاتيح حقول تسجيل الدخول والحسابات. |
| `Models/Employees/EmployeeFormViewModel.cs` | تصحيح Display وValidation ومفاتيح حقول الموظف. |
| `Models/Attendance/AttendanceViewModels.cs` | تصحيح Display وValidation للحضور. |
| `Models/LeaveManagement/LeaveViewModels.cs` | تصحيح Display وValidation للإجازات. |
| `Models/Organization/DepartmentViewModels.cs` | تصحيح Display وValidation للأقسام. |
| `Models/Organization/PositionViewModels.cs` | تصحيح Display وValidation للمسميات الوظيفية. |
| `Views/Shared/_Layout.cshtml` | استبدال مفتاح Ara غير الموجود بالمورد الموجود ArabicLanguageShort. |
| `Views/Shared/Error.cshtml` | استخدام الموارد للنصوص المعروضة في صفحة الخطأ. |
| `Views/Home/Privacy.cshtml` | نقل نص الواجهة الثابت إلى الموارد. |
| `Views/Home/Index.cshtml` | تصحيح رموز وفاصل التاريخ المشوهة بالترميز. |
| `Views/LeaveManagement/Index.cshtml` | تصحيح فاصل نصي مشوه؛ مورد عنوان الأرصدة أُضيف في resx. |
| `Views/Reports/Index.cshtml` | تصحيح عرض حالة التوظيف للقيم المخصصة وفواصل مشوهة. |
| `Views/Departments/Details.cshtml` | تصحيح عرض حالة التوظيف للقيم المخصصة. |
| `Views/Departments/_DetailsModal.cshtml` | التصحيح نفسه في النافذة المشتركة. |
| `Views/Employees/Details.cshtml` | تصحيح عرض حالة ونوع التوظيف للقيم المخصصة. |
| `Views/Employees/_DetailsModal.cshtml` | التصحيح نفسه في النافذة المشتركة. |
| `Views/Employees/_Results.cshtml` | تصحيح عرض الحالة والحفاظ على قيمة الفلتر الأصلية في روابط التنقل. |
| `LOCALIZATION_REPORT.md` | توثيق النتائج والحدود والملفات. |

ملفات تحقق محلية إضافية تحت `obj/LocalizationVerification`، غير داخلة في Git: مشروع console لفحص الموارد وتسميات MVC، وسكربت `Verify-Http.ps1` لفحص الاستجابات وروابط اللغة.

## الاختبارات الفعلية

- `dotnet build ../HRMS.sln --no-restore`: نجاح، صفر تحذيرات وصفر أخطاء.
- مطابقة مفاتيح اللغتين: 459 موردًا لكل لغة، بلا تكرار، ولا مفاتيح حرفية ناقصة في استخدامات Views وControllers وتسميات ورسائل النماذج.
- فحص assemblies: الاسم HRMS، والموارد `HRMS.Resources.SharedResource.ar-JO.resources` و`HRMS.Resources.SharedResource.en-US.resources` في satellite assemblies. اختُبرت كل قيمة مجمعة مقابل ملف resx لكل لغة.
- فحص MVC: 54 تسمية حقل مترجمة في كل لغة، مع التحقق من وجود مفاتيح رسائل التحقق المخصصة.
- HTTP: صفحة Login تعرض تسجيل الدخول/Sign in والنصوص الأخرى من الموارد؛ العربية `lang="ar-JO" dir="rtl"` والإنجليزية `lang="en-US" dir="ltr"`.
- HTTP: متابعة روابط SetLanguage المستخرجة من HTML الواجهة في الاتجاهين، والتأكد من قيمة `.AspNetCore.Culture` وانتهاء صلاحيتها بعد نحو سنة، واستمرار اللغة عند التنقل إلى Privacy وError وعند إعادة طلب Login، وأولوية Cookie على Accept-Language.
- HTTP: قبول الإنجليزية بواسطة Accept-Language دون Cookie.
- HTTP: إرسال نموذج Login فارغ مع antiforgery token والتحقق من رسائل Email وPassword المترجمة من تحقق الخادم في اللغتين. لم يُنفذ تسجيل دخول أو تعديل بيانات.
- فحص القيم المخصصة للتوظيف: إظهار القيمة الأصلية عند عدم وجود مورد، وترجمة Active عند وجوده.
- `git diff --check`: اجتاز الفحص.
- أُوقفت نسخة الاختبار عند الانتهاء لمنع قفل ملفات البناء.

## حدود التحقق والمشكلة المتبقية

لم يُجر اختبار متصفح بصري أو نقر فعلي، لعدم وجود أداة متصفح متاحة. اختبارات التبديل استخدمت روابط الواجهة الحقيقية عبر HTTP، لا ادعاء مشاهدة النتيجة بصريًا.

قاعدة `(localdb)\MSSQLLocalDB` لا تتيح فتح قاعدة HRMS للمستخدم الحالي. التشغيل بالإعداد الأصلي يجعل Login يحاول قراءة الحسابات للتحقق من الإعداد الأولي ويفشل بسبب SQL 4060. لا علاقة لهذا بمطابقة موارد الترجمة. لاختبارات HTTP فقط استُخدم override في أمر التشغيل:

`dotnet run --no-build --no-restore --launch-profile http --urls http://127.0.0.1:5014 --Identity:AllowInitialAdministratorSetup=false`

لم يتغير ملف إعدادات، ولم تُعدل قاعدة البيانات أو تُنفذ migrations. الصفحات المحمية التي تحتاج تسجيل دخول وبيانات فعلية لم تُختبر عبر HTTP؛ تمت مراجعة استخدام مواردها واختبار الموارد المجمعة وتسميات نماذجها مستقلًا. تحتاج قيم التوظيف المخصصة موارد إضافية إذا أريد ترجمة محتوى البيانات نفسه.
