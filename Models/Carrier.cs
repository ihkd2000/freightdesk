using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FreightDesk.Models
{
    [Table("SteamShipLines")]
    public class Carrier
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Name is required.")]
        [StringLength(100, ErrorMessage ="Name cannot exceed 100 characters")]
        public string Name { get; set; }

    }
}
