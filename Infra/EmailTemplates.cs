using System.Net;
using System.Text;
using FreightDesk.Models;

namespace FreightDesk.Infra
{
    public record EmailContent(string Subject, string Text, string Html);

    /// <summary>Builds customer emails. All dynamic values are HTML-encoded; branding comes from configuration.</summary>
    public static class EmailTemplates
    {
        public static EmailContent ArrivalNotice(BrandingOptions brand, ContainerTR c, int daysBefore, DateTime now)
        {
            var arrival = c.Arrival?.ToString("yyyy-MM-dd") ?? "N/A";
            var invoice = c.InvoiceNo?.ToString() ?? "N/A";
            var port = c.Destination?.destination_name ?? "N/A";

            var body = new StringBuilder();
            body.Append("<p>Hello,</p>");
            body.Append($"<p>Your container is due to arrive within the next {daysBefore} days.");
            if (!string.IsNullOrWhiteSpace(brand.AccountingEmail))
            {
                body.Append($" Please look for your invoice from <strong>{E(brand.AccountingEmail)}</strong> and pay it if you have not already.");
            }
            else
            {
                body.Append(" Please make sure your invoice has been paid.");
            }
            body.Append("</p>");
            body.Append("<p>Late payment can delay the release of your cargo at the port of discharge, and we cannot be held responsible for charges caused by such delays.</p>");
            body.Append(Details(
                ("Arrival date", arrival),
                ("Container number", c.ContainerNumber),
                ("Booking number", c.BookingNumber),
                ("Invoice", invoice),
                ("Destination", port)));
            body.Append("<p>Please make any preparations needed for a smooth arrival.</p>");

            var text = $"Container {c.ContainerNumber} (booking {c.BookingNumber}) is due to arrive on {arrival}. " +
                       "Please make sure your invoice has been paid to avoid delays releasing the cargo.";

            return new EmailContent(
                $"Container arrival reminder - {arrival}",
                text,
                Wrap(brand, "Container arrival notice", body.ToString(), now.Year));
        }

        public static EmailContent PaymentReminder(BrandingOptions brand, ContainerTR c, int daysLeft,
            string? bankAccount, DateTime now)
        {
            var arrival = c.Arrival?.ToString("yyyy-MM-dd") ?? "N/A";
            var when = daysLeft <= 0 ? "today" : daysLeft == 1 ? "tomorrow" : $"in {daysLeft} days";

            var rows = new List<(string, string)>
            {
                ("Invoice", c.InvoiceNo?.ToString() ?? "N/A"),
                ("Container number", c.ContainerNumber ?? "N/A"),
                ("Booking number", c.BookingNumber ?? "N/A"),
                ("Amount due", c.Price?.ToString("C") ?? "N/A"),
                ("Arrival date", arrival)
            };
            if (!string.IsNullOrWhiteSpace(bankAccount))
            {
                rows.Add(("Bank account", bankAccount));
            }

            var body = new StringBuilder();
            body.Append("<p>Hello,</p>");
            body.Append($"<p>Payment for your shipment is still outstanding, and your container arrives {E(when)}.</p>");
            body.Append("<p>Paying before arrival helps us release your cargo without delay.</p>");
            body.Append(Details(rows.ToArray()));
            if (!string.IsNullOrWhiteSpace(brand.AccountingEmail))
            {
                body.Append($"<p>Questions about your invoice? Contact <strong>{E(brand.AccountingEmail)}</strong>. If you have already paid, please ignore this reminder.</p>");
            }
            else
            {
                body.Append("<p>If you have already paid, please ignore this reminder.</p>");
            }

            var text = $"Payment reminder: container {c.ContainerNumber} (booking {c.BookingNumber}) arrives {when} ({arrival}) and payment is still outstanding.";

            return new EmailContent(
                $"Payment reminder - container {c.ContainerNumber} arrives {arrival}",
                text,
                Wrap(brand, "Payment reminder", body.ToString(), now.Year));
        }

        public static EmailContent ShipmentRegistered(BrandingOptions brand, string clientName, ContainerTR c,
            string? bankAccount, DateTime now)
        {
            var details = new List<(string, string)>
            {
                ("Job reference", c.JobReferenceNumber ?? "N/A"),
                ("Booking number", c.BookingNumber ?? "N/A"),
                ("Container number", c.ContainerNumber ?? "N/A")
            };
            if (!string.IsNullOrWhiteSpace(bankAccount))
            {
                details.Add(("Bank account", bankAccount));
            }
            details.Add(("Estimated arrival", c.Arrival?.ToString("yyyy-MM-dd") ?? "N/A"));
            details.Add(("Total price", c.Price?.ToString("C") ?? "N/A"));

            var body = new StringBuilder();
            body.Append($"<p>Hello {E(clientName)},</p>");
            body.Append("<p>Your shipment has been registered. Here are the details:</p>");
            body.Append(Details(details.ToArray()));
            if (!string.IsNullOrWhiteSpace(brand.TrackingUrl))
            {
                body.Append($"<p><a href='{E(brand.TrackingUrl)}' target='_blank'>Track your shipment</a></p>");
            }
            body.Append("<p>If you have any questions, just reply to this email.</p>");

            var text = $"Hello {clientName}, your shipment (container {c.ContainerNumber}, booking {c.BookingNumber}) has been registered with {brand.CompanyName}.";

            return new EmailContent(
                $"Shipment registered: {c.ContainerNumber}",
                text,
                Wrap(brand, "Shipment registered", body.ToString(), now.Year));
        }

        private static string Details(params (string Label, string Value)[] rows)
        {
            var sb = new StringBuilder("<div class='details'>");
            foreach (var (label, value) in rows)
            {
                sb.Append($"<p><strong>{E(label)}:</strong> {E(value)}</p>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static string Wrap(BrandingOptions brand, string title, string bodyHtml, int year)
        {
            var footer = new StringBuilder($"&copy; {year} {E(brand.CompanyName)}");
            if (!string.IsNullOrWhiteSpace(brand.WebsiteUrl))
            {
                footer.Append($" | <a href='{E(brand.WebsiteUrl)}'>Website</a>");
            }
            if (!string.IsNullOrWhiteSpace(brand.SupportEmail))
            {
                footer.Append($" | <a href='mailto:{E(brand.SupportEmail)}'>{E(brand.SupportEmail)}</a>");
            }

            return $@"<!DOCTYPE html>
<html lang='en'>
<head>
<meta charset='UTF-8'>
<meta name='viewport' content='width=device-width, initial-scale=1.0'>
<style>
body {{ font-family: Segoe UI, Arial, sans-serif; background: #f3f6f8; padding: 20px; color: #12303d; }}
.card {{ background: #ffffff; max-width: 600px; margin: auto; border-radius: 12px; overflow: hidden; }}
.head {{ background: #0f766e; color: #ffffff; padding: 18px 28px; }}
.head h1 {{ margin: 0; font-size: 20px; }}
.head small {{ opacity: .85; }}
.body {{ padding: 24px 28px; font-size: 15px; line-height: 1.55; }}
.details {{ background: #eef4f6; border-radius: 8px; padding: 10px 16px; margin: 16px 0; }}
.details p {{ margin: 6px 0; }}
.foot {{ text-align: center; font-size: 12px; color: #64808e; padding: 14px 28px 22px; }}
a {{ color: #0f766e; }}
</style>
</head>
<body>
<div class='card'>
<div class='head'><h1>{E(title)}</h1><small>{E(brand.CompanyName)}</small></div>
<div class='body'>{bodyHtml}</div>
<div class='foot'>{footer}</div>
</div>
</body>
</html>";
        }

        private static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
    }
}
