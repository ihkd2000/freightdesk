using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using NuGet.Protocol;


namespace FreightDesk.Models
{
    public class ContainerTR:INotifyPropertyChanged
    {
        public int Id { get; set; }
        
        public  string Owner { get; set; }

        public int? InvoiceNo { get; set; }

        [Required(ErrorMessage="Job reference is required")]
        public string JobReferenceNumber { get; set; }

        [Required(ErrorMessage = "Booking number is required")]
        public string BookingNumber { get; set; }

        [Required(ErrorMessage = "Container number is required")]
        public string  ContainerNumber { get; set; }

        // Foreign Key for SteamShipLine
        [Required(ErrorMessage = "Please select a carrier")]
        public int? SteamShipLineId { get; set; }

               
        [ForeignKey("SteamShipLineId")]
        public  SteamShipLine SteamShipLine { get; set; }

        // Foreign Key for Destination
        [Range(1, int.MaxValue, ErrorMessage = "Please select a destination port")]
        public int DestinationId { get; set; }
        [ForeignKey("DestinationId")]
        public  Destination Destination { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Please select a client")]
        public int ShippingId { get; set; }
        [ForeignKey("ShippingId")]
        public  Shipping Shipping { get; set; }

        public decimal? CargoQuantity { get; set; }
        public  string? Shipper { get; set; }
        public  string? CargoDescription { get; set; }
        public DateTime? InvoiceDate { get; set; }
        public DateTime? CutOffDate { get; set; }
        public DateTime? Sailing { get; set; }
        public DateTime? Arrival { get; set; }
        //public DateTime? Arrival { get=> _arrival; set {
        //    _arrival = value;
        //        OnPropertyChanged();
        //    } }

        public DateTime? PaymentReceivedDate { get; set; }
        public DateTime? PaidToSSLDate { get; set; }
        public DateTime? ReleaseDate { get; set; }
        public decimal? Price { get; set; }
        public int? Emailed { get; set; }

        /// <summary>How many payment-reminder stages have already been emailed for the current arrival date.</summary>
        public int PaymentRemindersSent { get; set; }
        public string? Notes { get; set; }

        public int Deleted { get; set; }


        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
