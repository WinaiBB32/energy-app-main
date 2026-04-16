using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using EnergyApp.API.Models.Office;

namespace EnergyApp.API.Data
{
    public class VehicleRecordSeeder
    {
        private readonly AppDbContext _context;
        public VehicleRecordSeeder(AppDbContext context)
        {
            _context = context;
        }

        public void SeedFromCsv(string csvFilePath)
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Encoding = System.Text.Encoding.UTF8,
                HeaderValidated = null,
                MissingFieldFound = null
            };
            using var reader = new StreamReader(csvFilePath);
            using var csv = new CsvReader(reader, config);
            var records = csv.GetRecords<VehicleRecordCsv>().ToList();

            var vehicleRecords = records.Select(r => new VehicleRecord
            {
                FaceScanId = (r.FaceScanCode ?? string.Empty).Trim().Substring(0, Math.Min(50, (r.FaceScanCode ?? string.Empty).Trim().Length)),
                FullName = (r.FullName ?? string.Empty).Trim().Substring(0, Math.Min(150, (r.FullName ?? string.Empty).Trim().Length)),
                Position = (r.Position ?? string.Empty).Trim().Substring(0, Math.Min(100, (r.Position ?? string.Empty).Trim().Length)),
                Department = (r.Department ?? string.Empty).Trim().Substring(0, Math.Min(100, (r.Department ?? string.Empty).Trim().Length)),
                PhoneNumber = (r.ContactNumber ?? string.Empty).Trim().Substring(0, Math.Min(20, (r.ContactNumber ?? string.Empty).Trim().Length)),
                LicensePlate = (r.LicensePlate ?? string.Empty).Trim().Substring(0, Math.Min(20, (r.LicensePlate ?? string.Empty).Trim().Length)),
                Brand = (r.Brand ?? string.Empty).Trim().Substring(0, Math.Min(50, (r.Brand ?? string.Empty).Trim().Length)),
                Model = (r.Model ?? string.Empty).Trim().Substring(0, Math.Min(50, (r.Model ?? string.Empty).Trim().Length)),
                Province = (r.Province ?? string.Empty).Trim().Substring(0, Math.Min(100, (r.Province ?? string.Empty).Trim().Length)),
                CreatedAtUtc = DateTime.UtcNow
            }).ToList();

            _context.VehicleRecords.AddRange(vehicleRecords);
            _context.SaveChanges();
        }
    }

    // Helper class for mapping CSV columns
    public class VehicleRecordCsv
    {
        [CsvHelper.Configuration.Attributes.Name("User ID")]
        public string UserId { get; set; }
        [CsvHelper.Configuration.Attributes.Name("รหัสสแกนหน้า")]
        public string FaceScanCode { get; set; }
        [CsvHelper.Configuration.Attributes.Name("ชื่อ - นามสกุล")]
        public string FullName { get; set; }
        [CsvHelper.Configuration.Attributes.Name("ตำแหน่ง")]
        public string Position { get; set; }
        [CsvHelper.Configuration.Attributes.Name("หน่วยงาน")]
        public string Department { get; set; }
        [CsvHelper.Configuration.Attributes.Name("เบอร์ติดต่อ")]
        public string ContactNumber { get; set; }
        [CsvHelper.Configuration.Attributes.Name("ทะเบียนรถยนต์")]
        public string LicensePlate { get; set; }
        [CsvHelper.Configuration.Attributes.Name("ยี่ห้อ")]
        public string Brand { get; set; }
        [CsvHelper.Configuration.Attributes.Name("รุ่น")]
        public string Model { get; set; }
        [CsvHelper.Configuration.Attributes.Name("จังหวัด")]
        public string Province { get; set; }
    }
}
