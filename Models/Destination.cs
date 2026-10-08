using System.ComponentModel.DataAnnotations;
using System.Security.Principal;

namespace FreightDesk.Models
{
    public class Destination
    {
        public int Id { get; set; }
        [Required(ErrorMessage = "Destination name is required.")]
        [StringLength(100, ErrorMessage = "Destination name cannot exceed 100 characters")]
        public  string destination_name { get; set; }  
    }
}
