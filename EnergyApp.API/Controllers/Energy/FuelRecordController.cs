using EnergyApp.API.Data;
using EnergyApp.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace EnergyApp.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class FuelRecordController : ControllerBase
    {
        private readonly AppDbContext _context;
        public FuelRecordController(AppDbContext context) { _context = context; }

        public class FuelRecordDto
        {
            public Guid? DepartmentId { get; set; }
            public DateTime? RefuelDate { get; set; }
            public string DocumentType { get; set; } = string.Empty;
            public string DocumentNumber { get; set; } = string.Empty;
            public string VehiclePlate { get; set; } = string.Empty;
            public string VehicleProvince { get; set; } = string.Empty;
            public string PurchaserName { get; set; } = string.Empty;
            public string FuelTypeName { get; set; } = string.Empty;
            public decimal Liters { get; set; }
            public decimal TotalAmount { get; set; }
            public string GasStationCompany { get; set; } = string.Empty;
            public string Note { get; set; } = string.Empty;
            public string RecordedBy { get; set; } = string.Empty;
        }

        [HttpGet]
        public async Task<IActionResult> Get(
            [FromQuery] Guid? departmentId,
            [FromQuery] string? fromDate,
            [FromQuery] string? toDate,
            [FromQuery] int skip = 0,
            [FromQuery] int take = 50)
        {
            var query = _context.FuelRecords.AsQueryable();

            if (departmentId.HasValue)
                query = query.Where(r => r.DepartmentId == departmentId);

            if (!string.IsNullOrEmpty(fromDate) && DateTime.TryParse(fromDate, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var fd))
            {
                var fdu = fd.ToUniversalTime();
                query = query.Where(r => r.CreatedAt >= fdu);
            }

            if (!string.IsNullOrEmpty(toDate) && DateTime.TryParse(toDate, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var td))
            {
                var tdu = td.ToUniversalTime();
                query = query.Where(r => r.CreatedAt <= tdu);
            }

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

            return Ok(new { total, items });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var item = await _context.FuelRecords.FindAsync(id);
            if (item == null) return NotFound();
            return Ok(item);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] FuelRecordDto req)
        {
            var record = new FuelRecord
            {
                DepartmentId = req.DepartmentId,
                RefuelDate = req.RefuelDate,
                DocumentType = req.DocumentType,
                DocumentNumber = req.DocumentNumber,
                VehiclePlate = req.VehiclePlate,
                VehicleProvince = req.VehicleProvince,
                PurchaserName = req.PurchaserName,
                FuelTypeName = req.FuelTypeName,
                Liters = req.Liters,
                TotalAmount = req.TotalAmount,
                GasStationCompany = req.GasStationCompany,
                Note = req.Note,
                RecordedBy = req.RecordedBy
            };
            _context.FuelRecords.Add(record);
            await _context.SaveChangesAsync();
            return Ok(new { message = "บันทึกข้อมูลเชื้อเพลิงสำเร็จ", id = record.Id });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] FuelRecordDto req)
        {
            var record = await _context.FuelRecords.FindAsync(id);
            if (record == null) return NotFound();

            record.DepartmentId = req.DepartmentId;
            record.RefuelDate = req.RefuelDate;
            record.DocumentType = req.DocumentType;
            record.DocumentNumber = req.DocumentNumber;
            record.VehiclePlate = req.VehiclePlate;
            record.VehicleProvince = req.VehicleProvince;
            record.PurchaserName = req.PurchaserName;
            record.FuelTypeName = req.FuelTypeName;
            record.Liters = req.Liters;
            record.TotalAmount = req.TotalAmount;
            record.GasStationCompany = req.GasStationCompany;
            record.Note = req.Note;
            record.RecordedBy = req.RecordedBy;

            await _context.SaveChangesAsync();
            return Ok(new { message = "แก้ไขข้อมูลเชื้อเพลิงสำเร็จ" });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var record = await _context.FuelRecords.FindAsync(id);
            if (record == null) return NotFound();
            _context.FuelRecords.Remove(record);
            await _context.SaveChangesAsync();
            return Ok(new { message = "ลบสำเร็จ" });
        }
    }
}
