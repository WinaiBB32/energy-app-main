using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EnergyApp.API.Models.Office
{
    [Table("CarRecords")]
    public class CarRecord
    {
        [Key]
        public int Id { get; set; }
        public string UserId { get; set; }
        public string FaceScanCode { get; set; }
        public string FullName { get; set; }
        public string Position { get; set; }
        public string Department { get; set; }
        public string ContactNumber { get; set; }
        public string LicensePlate { get; set; }
        public string Brand { get; set; }
        public string Model { get; set; }
        public string Province { get; set; }
    }
}