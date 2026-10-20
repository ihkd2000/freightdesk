using FreightDesk.Data;
using FreightDesk.Models;
using Microsoft.EntityFrameworkCore;

namespace FreightDesk.Infra
{
    public record RuleError(string Field, string Message);

    /// <summary>Business rules for shipment data entry. Pure logic (apart from the duplicate lookup) so it is easy to test.</summary>
    public static class ShipmentRules
    {
        // ISO 6346 letter values: A=10 upwards, skipping multiples of 11.
        private const string Letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        /// <summary>Upper-cases and strips spaces and dashes, e.g. "mscu 123456-6" becomes "MSCU1234566".</summary>
        public static string NormalizeContainerNumber(string? value) =>
            new string((value ?? "").Where(ch => !char.IsWhiteSpace(ch) && ch != '-').ToArray()).ToUpperInvariant();

        /// <summary>The ISO 6346 check digit for the first 10 characters of a container number.</summary>
        public static int CheckDigit(string first10)
        {
            var sum = 0;
            for (var i = 0; i < 10; i++)
            {
                var ch = first10[i];
                var value = char.IsDigit(ch) ? ch - '0' : LetterValue(ch);
                sum += value << i;
            }
            return sum % 11 % 10;
        }

        private static int LetterValue(char ch)
        {
            var value = 10;
            foreach (var letter in Letters)
            {
                if (value % 11 == 0) value++;
                if (letter == ch) return value;
                value++;
            }
            throw new ArgumentOutOfRangeException(nameof(ch));
        }

        /// <summary>Null when the number is a valid ISO 6346 container number, otherwise a message for the user.</summary>
        public static string? ContainerNumberError(string? value)
        {
            var n = NormalizeContainerNumber(value);
            if (n.Length == 0)
            {
                return null; // [Required] reports the empty case.
            }

            var shapeOk = n.Length == 11
                          && n.Take(3).All(c => c is >= 'A' and <= 'Z')
                          && n[3] is 'U' or 'J' or 'Z'
                          && n.Skip(4).All(char.IsDigit);
            if (!shapeOk)
            {
                return "Container number must be 4 letters ending in U, J or Z, then 7 digits (e.g. MSCU1234566).";
            }

            var expected = CheckDigit(n[..10]);
            return n[10] - '0' == expected
                ? null
                : $"Check digit does not match: {n[..10]} should end in {expected}. Please re-check the number.";
        }

        /// <param name="checkContainerFormat">
        /// Existing records may predate the format check, so callers only ask for it when the number is entered or changed.
        /// </param>
        public static IReadOnlyList<RuleError> Validate(Shipment s, bool checkContainerFormat)
        {
            var errors = new List<RuleError>();

            if (checkContainerFormat)
            {
                var containerError = ContainerNumberError(s.ContainerNumber);
                if (containerError != null)
                {
                    errors.Add(new RuleError(nameof(Shipment.ContainerNumber), containerError));
                }
            }

            if (s.CutOffDate.HasValue && s.Sailing.HasValue && s.CutOffDate.Value.Date > s.Sailing.Value.Date)
            {
                errors.Add(new RuleError(nameof(Shipment.CutOffDate), "Cut-off must be on or before the sailing date."));
            }

            if (s.Sailing.HasValue && s.Arrival.HasValue && s.Sailing.Value.Date > s.Arrival.Value.Date)
            {
                errors.Add(new RuleError(nameof(Shipment.Arrival), "Arrival must be on or after the sailing date."));
            }
            else if (!s.Sailing.HasValue && s.CutOffDate.HasValue && s.Arrival.HasValue
                     && s.CutOffDate.Value.Date > s.Arrival.Value.Date)
            {
                errors.Add(new RuleError(nameof(Shipment.Arrival), "Arrival must be on or after the cut-off date."));
            }

            if (s.Price < 0)
            {
                errors.Add(new RuleError(nameof(Shipment.Price), "Price cannot be negative."));
            }

            if (s.CargoQuantity <= 0)
            {
                errors.Add(new RuleError(nameof(Shipment.CargoQuantity), "Quantity must be greater than zero."));
            }

            if (s.InvoiceNo <= 0)
            {
                errors.Add(new RuleError(nameof(Shipment.InvoiceNo), "Invoice number must be a positive number."));
            }

            return errors;
        }

        /// <summary>One more than the highest invoice number used so far (deleted shipments included), or 1.</summary>
        public static async Task<int> NextInvoiceNumberAsync(AppDbContext db, CancellationToken cancellationToken = default) =>
            (await db.Shipments.MaxAsync(x => x.InvoiceNo, cancellationToken) ?? 0) + 1;

        /// <summary>Id of another active shipment with the same container and booking number, if any.</summary>
        public static async Task<int?> FindDuplicateAsync(AppDbContext db, Shipment s, CancellationToken cancellationToken = default)
        {
            var container = NormalizeContainerNumber(s.ContainerNumber);
            var booking = (s.BookingNumber ?? "").Trim().ToUpper();
            if (container.Length == 0 || booking.Length == 0)
            {
                return null;
            }

            return await db.Shipments.AsNoTracking()
                .Where(x => x.Id != s.Id && x.Deleted != 1
                            && x.ContainerNumber.ToUpper() == container
                            && x.BookingNumber.Trim().ToUpper() == booking)
                .Select(x => (int?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}
