using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using NuGet.Protocol;


namespace FreightDesk.Models
{
    [Table("Containers")]
    public class Shipment:INotifyPropertyChanged
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

        // Foreign Key for Carrier
        [Required(ErrorMessage = "Please select a carrier")]
        [Column("SteamShipLineId")]
        public int? CarrierId { get; set; }

               
        [ForeignKey("CarrierId")]
        public  Carrier Carrier { get; set; }

        // Foreign Key for Port
        [Range(1, int.MaxValue, ErrorMessage = "Please select a destination port")]
        [Column("DestinationId")]
        public int PortId { get; set; }
        [ForeignKey("PortId")]
        public  Port Port { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Please select a client")]
        [Column("ShippingId")]
        public int ClientId { get; set; }
        [ForeignKey("ClientId")]
        public  Client Client { get; set; }

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
        [Column("PaidToSSLDate")]
        public DateTime? PaidToCarrierDate { get; set; }
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
