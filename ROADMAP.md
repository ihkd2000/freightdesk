# Roadmap

Planned work for FreightDesk, roughly in priority order. Estimates are rough.

## Cleanup and refactoring (~6 h)

- [ ] Remove the legacy models `ContainerTR`, `Shipping`, `Destination` and `SteamShipLine`; they were replaced by `Shipment`, `Client`, `Port` and `Carrier` and are no longer used.
- [ ] Rename the legacy table and columns (`Containers`, `SteamShipLineId`, `DestinationId`, `ShippingId`, `PaidToSSLDate`) with a migration so the schema matches the model names.
- [ ] Replace the `int Deleted` / `int? Emailed` flags with `bool` (or `DateTime?`) columns.
- [ ] Drop the unused `INotifyPropertyChanged` plumbing and the stray `using NuGet.Protocol` from models.
- [ ] Fix nullable-reference warnings across models and pages.
- [ ] Remove unused package references (`Microsoft.VisualStudio.Web.CodeGeneration.Design`, `Hellang.Middleware.ProblemDetails` if unused).

## Features (~12 h)

- [ ] Invoice PDF: generate an invoice from a shipment (invoice number, date, price, bank details) and attach it to the registration email.
- [ ] CSV export of the shipment list with the current filters.
- [ ] Shipment status history: record who changed what and when.
- [ ] Bulk import of shipments from a CSV/Excel sheet, for offices moving off spreadsheets.
- [ ] SMTP mail sender as an alternative to Mailgun (`IMailSender` already abstracts it).
- [ ] Client-facing tracking page (read-only link per shipment).

## Tests (~5 h)

- [ ] Unit tests for `AttentionRules` and `ShipmentStatus`.
- [ ] Tests for `EmailDispatcher` retry and email-log behaviour.
- [ ] bUnit component tests for the shipment create/edit forms and validation.
- [ ] Integration test that boots the app against an in-memory SQLite database.

## Deployment and docs (~4 h)

- [ ] Dockerfile and `docker-compose.yml` with a volume for the SQLite database.
- [ ] Publish self-contained release binaries from GitHub Actions on tagged versions.
- [ ] Screenshots and a short walkthrough in the README.
- [ ] `appsettings.example.json` documenting every setting with placeholder values.
- [ ] CONTRIBUTING.md.
