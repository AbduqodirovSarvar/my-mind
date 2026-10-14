# MyMind API (backend)

Maqola platformasining backend'i. Frontend (`my-mind-ui`) va deploy (`ci-cd`) alohida repolarda.
Loyiha hujjati: https://claude.ai/code/artifact/bc7af302-472b-403d-8183-42299d926533

## Stek

.NET 10, ASP.NET Core Minimal API, EF Core 10 + Npgsql, PostgreSQL 17, Identity (cookie), FluentValidation, HtmlSanitizer, Serilog.
Testlar: xUnit v3 + Microsoft.Testing.Platform (`global.json`), WebApplicationFactory, Testcontainers (Docker kerak).

## Tuzilma

```
src/MyMind.Api              Endpoint'lar (modul bo'yicha MapGroup), middleware, DI
src/MyMind.Application      Features/<Modul>/: buyruqlar, so'rovlar, DTO, validatorlar
src/MyMind.Domain           Entity'lar va domen qoidalari
src/MyMind.Infrastructure   EF Core, IFileStorage, IEmailSender, sanitizatsiya
tests/MyMind.UnitTests      LayeringTests qatlam yo'nalishini tekshiradi
tests/MyMind.IntegrationTests
```

Bog'lanishlar: Api → Application + Infrastructure, Infrastructure → Application, Application → Domain.

## Buyruqlar

```
dotnet build MyMind.slnx
dotnet test --solution MyMind.slnx
dotnet format MyMind.slnx --verify-no-changes
docker compose -f compose.dev.yml up -d    # dev PostgreSQL on localhost:5433
dotnet run --project src/MyMind.Api        # http://localhost:5071, /scalar, /health
dotnet ef migrations add <Name> -p src/MyMind.Infrastructure -s src/MyMind.Api -o Persistence/Migrations
```

## Auth (1-bosqich)

- Identity + cookie `__Host-mymind`; `AppUser`/`AppRole` va `AppDbContext` Infrastructure'da; endpoint'lar `src/MyMind.Api/Features/Auth`.
- `/api` guruhidagi har bir POST/PUT/PATCH/DELETE `AntiforgeryValidationFilter` dan o'tadi; `XSRF-TOKEN` cookie `GET /api/auth/me` da va login/register/logout'da (yangi foydalanuvchi bilan) beriladi.
- Validatsiya: `.WithValidation<TRequest>()` (FluentValidation, Application'dagi validator).
- Rate limit: `.RequireRateLimiting(RateLimitPolicies.X)`, limitlar `RateLimits` sozlamasida.
- Integratsiya testlari: `ApiFactory.CreateBrowserClient()` — cookie va XSRF header'ni brauzer kabi boshqaradi; baza Testcontainers'da.
- EF migratsiyalari `generated_code` (.editorconfig), shuning uchun stil qoidalari ularga qo'llanmaydi.

## Kontent (2-bosqich)

- Use-case'lar Application'dagi servislarda (`CategoryService`, `ArticleService`, `PublicReadService`); ular `IAppDbContext`, `ICurrentUser`, `IArticleRenderer`, `IUserDirectory` orqali ishlaydi va `Result<T>` qaytaradi. Api `ToHttp(...)` bilan HTTP'ga aylantiradi (`ErrorKind` → 400/404/409/412/428/403).
- Egalik: har bir so'rov `UserId == currentUser.Id` bilan; begona ID (shu jumladan begona `parentId`/`categoryId`) → 404. Yangi `/api/me` endpoint'i `OwnershipTests` ro'yxatiga qo'shiladi.
- Maqola: qoralama (`Draft*`) va e'lon qilingan nusxa alohida; publish draftni renderer orqali nusxalaydi. `Version` = `xmin`, javoblarda ETag sifatida.
- `TipTapRenderer` faqat ma'lum node turlarini qabul qiladi (noma'lum → 400), hamma narsani encode qiladi, keyin `ArticleHtmlSanitizer` dan o'tkazadi. Yangi node turi qo'shilsa: renderer + sanitizer allow-list + `XssTests` payload'lari.
- Slug: `Domain/Common/Slug` (transliteratsiya, `MakeUnique`). Kategoriya slug'i URL'da bezak; maqola `(username, slug)` bo'yicha topiladi.

## Qoidalar

- ID: `Guid.CreateVersion7()`; vaqt: `timestamptz`, faqat UTC; DB nomlari `snake_case`.
- `/api/me/*` dagi har bir so'rov `user_id` bo'yicha filtrlanadi; begona resurs uchun `404`. Har bir yangi `/api/me` endpoint'iga egalik testi yoziladi.
- Xatolar ProblemDetails formatida; validatsiya FluentValidation bilan.
- Maqola HTML'ini server TipTap JSON'idan o'zi yasaydi va HtmlSanitizer'dan o'tkazadi; klient HTML'iga ishonilmaydi.
- Migratsiyalar faqat qo'shimcha (additive); ustun o'chirish ikki relizga bo'linadi.
- Paket versiyalari `Directory.Packages.props` da; `TreatWarningsAsErrors` + NuGet audit — zaif paket build'ni yiqitadi.
- Sirlar faqat user-secrets / muhit o'zgaruvchilari orqali.
