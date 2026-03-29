using EnergyApp.API.Data;
using EnergyApp.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyApp.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class ElectricityRecordController : ControllerBase
    {
        private readonly AppDbContext _context;
        public ElectricityRecordController(AppDbContext context) { _context = context; }

        public class ElectricityRecordDto
        {
            public string Type { get; set; } = "PEA_BILL";
            public string DocReceiveNumber { get; set; } = string.Empty;
            public string DocNumber { get; set; } = string.Empty;
            public Guid? BuildingId { get; set; }
            public DateTime? BillingCycle { get; set; }
            public decimal PeaUnitUsed { get; set; }
            public decimal PeaAmount { get; set; }
            public decimal FtRate { get; set; }
            public DateTime? RecordDate { get; set; }
            public decimal SolarUnitProduced { get; set; }
            public string Note { get; set; } = string.Empty;
            public string RecordedBy { get; set; } = string.Empty;
        }

        [HttpGet]
        public async Task<IActionResult> Get(
            [FromQuery] Guid? buildingId,
            [FromQuery] string? type,
            [FromQuery] string? fromDate,
            [FromQuery] string? toDate,
            [FromQuery] int skip = 0,
            [FromQuery] int take = 50)
        {
            var query = _context.ElectricityRecords.AsQueryable();

            if (buildingId.HasValue)
                query = query.Where(r => r.BuildingId == buildingId);

            if (!string.IsNullOrEmpty(type))
                query = query.Where(r => r.Type == type);

            if (!string.IsNullOrEmpty(fromDate) && DateTime.TryParse(fromDate, out var fd))
            {
                var fdu = fd.ToUniversalTime();
                query = query.Where(r => r.CreatedAt >= fdu);
            }

            if (!string.IsNullOrEmpty(toDate) && DateTime.TryParse(toDate, out var td))
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
            var item = await _context.ElectricityRecords.FindAsync(id);
            if (item == null) return NotFound();
            return Ok(item);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ElectricityRecordDto req)
        {
            var record = new ElectricityRecord
            {
                Type = req.Type,
                DocReceiveNumber = req.DocReceiveNumber,
                DocNumber = req.DocNumber,
                BuildingId = req.BuildingId,
                BillingCycle = req.BillingCycle,
                PeaUnitUsed = req.PeaUnitUsed,
                PeaAmount = req.PeaAmount,
                FtRate = req.FtRate,
                RecordDate = req.RecordDate,
                SolarUnitProduced = req.SolarUnitProduced,
                Note = req.Note,
                RecordedBy = req.RecordedBy
            };
            _context.ElectricityRecords.Add(record);
            await _context.SaveChangesAsync();
            return Ok(new { message = "บันทึกข้อมูลไฟฟ้าสำเร็จ", id = record.Id });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] ElectricityRecordDto req)
        {
            var record = await _context.ElectricityRecords.FindAsync(id);
            if (record == null) return NotFound();

            record.Type = req.Type;
            record.DocReceiveNumber = req.DocReceiveNumber;
            record.DocNumber = req.DocNumber;
            record.BuildingId = req.BuildingId;
            record.BillingCycle = req.BillingCycle;
            record.PeaUnitUsed = req.PeaUnitUsed;
            record.PeaAmount = req.PeaAmount;
            record.FtRate = req.FtRate;
            record.RecordDate = req.RecordDate;
            record.SolarUnitProduced = req.SolarUnitProduced;
            record.Note = req.Note;
            record.RecordedBy = req.RecordedBy;

            await _context.SaveChangesAsync();
            return Ok(new { message = "แก้ไขข้อมูลไฟฟ้าสำเร็จ" });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var record = await _context.ElectricityRecords.FindAsync(id);
            if (record == null) return NotFound();
            _context.ElectricityRecords.Remove(record);
            await _context.SaveChangesAsync();
            return Ok(new { message = "ลบสำเร็จ" });
        }
    }
}
