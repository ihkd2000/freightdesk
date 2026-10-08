# FreightDesk

Shipment tracking and billing for freight forwarders. Blazor Server on ASP.NET Core 9 with EF Core (SQLite) and Identity.

- **Dashboard**: active shipments, arrivals due soon, unpaid and released counts.
- **Shipments**: register, search, filter by status, edit, soft-delete; clients are emailed on registration.
- **Directory**: clients, carriers and ports.
- **Administration**: users and roles (Admin only).
- **Arrival notices**: a background job emails each client once before arrival (Mailgun).
- **Payment reminders**: unpaid shipments get an email at configurable points before arrival (default 5 and 2 days).
- **Needs attention**: the dashboard lists overdue payments, unpaid shipments arriving soon, paid shipments ready to release, and clients without an email address.
- **Email log**: every email is recorded with its result; failed or lost emails can be sent again with one click.

## Run

```
dotnet user-secrets set "Seed:AdminEmail" "you@example.com"
dotnet user-secrets set "Seed:AdminPassword" "<strong password>"
dotnet run
```

The database is created and migrated on startup (`freightdesk.db`).

## Configuration

Use user-secrets or environment variables, not source control.

| Key | Purpose |
| --- | --- |
| `Branding:CompanyName`, `Tagline`, `WebsiteUrl`, `TrackingUrl`, `SupportEmail`, `AccountingEmail` | Name and links shown in the UI and customer emails |
| `Seed:AdminEmail` / `Seed:AdminPassword` | Creates the first Admin if that user does not exist |
| `EmailSettings:apikey`, `EmailSettings:domain`, `EmailSettings:FromAddress` | Mailgun |
| `Billing:BankAccount` | Shown in the registration email when set |
| `ArrivalNotifications:DaysBefore` / `PollMinutes` | Arrival notice window (default 10) and job interval (default 15) |
| `PaymentReminders:DaysBefore` / `PollMinutes` | Days before arrival to remind unpaid clients (default `[5, 2]`) and job interval (default 30) |
| `ConnectionStrings:DefaultConnection` | SQLite connection |

Roles: `Admin`, `Moderator`, `User`.

## Tests

```
dotnet test tests/FreightDesk.Tests
```

## License

Copyright (c) 2026 Ayman Anaam All rights reserved. See [LICENSE](LICENSE).
