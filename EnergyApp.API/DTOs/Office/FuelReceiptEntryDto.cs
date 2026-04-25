namespace EnergyApp.API.DTOs.Office
{
    public class FuelReceiptEntryDto
    {
        public int Day { get; set; }
        public string Month { get; set; } = string.Empty;
        public int Year { get; set; }
        public string Detail { get; set; } = string.Empty;
        public string ReceiptNo { get; set; } = string.Empty;
        public string BookNo { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string DriverName { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
        public decimal Liters { get; set; }
    }
}
