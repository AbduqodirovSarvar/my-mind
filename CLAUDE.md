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
dotnet run --project src/MyMind.Api        # http://localhost:5071, /scalar, /health
```

## Qoidalar

- ID: `Guid.CreateVersion7()`; vaqt: `timestamptz`, faqat UTC; DB nomlari `snake_case`.
- `/api/me/*` dagi har bir so'rov `user_id` bo'yicha filtrlanadi; begona resurs uchun `404`. Har bir yangi `/api/me` endpoint'iga egalik testi yoziladi.
- Xatolar ProblemDetails formatida; validatsiya FluentValidation bilan.
- Maqola HTML'ini server TipTap JSON'idan o'zi yasaydi va HtmlSanitizer'dan o'tkazadi; klient HTML'iga ishonilmaydi.
- Migratsiyalar faqat qo'shimcha (additive); ustun o'chirish ikki relizga bo'linadi.
- Paket versiyalari `Directory.Packages.props` da; `TreatWarningsAsErrors` + NuGet audit — zaif paket build'ni yiqitadi.
- Sirlar faqat user-secrets / muhit o'zgaruvchilari orqali.
