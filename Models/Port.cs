using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Principal;

namespace FreightDesk.Models
{
    [Table("Destinations")]
    public class Port
    {
        public int Id { get; set; }
        [Required(ErrorMessage = "Port name is required.")]
        [StringLength(100, ErrorMessage = "Port name cannot exceed 100 characters")]
        [Column("destination_name")]
        public  string Name { get; set; }  
    }
}
