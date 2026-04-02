using EnergyApp.API.Data;
using EnergyApp.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyApp.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/v1/[controller]")]
    public class SolarProductionController : ControllerBase
    {
        private readonly AppDbContext _context;
        public SolarProductionController(AppDbContext context) { _context = context; }

        public class SolarProductionDto
        {
            public Guid? BuildingId { get; set; }
            public DateTime? RecordDate { get; set; }
            public decimal SolarUnitProduced { get; set; }
            public decimal ProductionWh { get; set; }
            public decimal ToBatteryWh { get; set; }
            public decimal ToGridWh { get; set; }
            public decimal ToHomeWh { get; set; }
            public decimal ConsumptionWh { get; set; }
            public decimal FromBatteryWh { get; set; }
            public decimal FromGridWh { get; set; }
            public decimal FromSolarWh { get; set; }
            public string Note { get; set; } = string.Empty;
            public string RecordedBy { get; set; } = string.Empty;
            public string DepartmentId { get; set; } = string.Empty;
        }

        [HttpGet]
        public async Task<IActionResult> Get(
            [FromQuery] Guid? buildingId,
            [FromQuery] string? fromDate,
            [FromQuery] string? toDate,
            [FromQuery] int skip = 0,
            [FromQuery] int take = 50)
        {
            var query = _context.SolarProductions.AsQueryable();

            if (buildingId.HasValue)
                query = query.Where(r => r.BuildingId == buildingId);

            if (!string.IsNullOrEmpty(fromDate) && DateTime.TryParse(fromDate, out var fd))
                query = query.Where(r => r.RecordDate >= fd);

            if (!string.IsNullOrEmpty(toDate) && DateTime.TryParse(toDate, out var td))
                query = query.Where(r => r.RecordDate <= td);

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
            var item = await _context.SolarProductions.FindAsync(id);
            if (item == null) return NotFound();
            return Ok(item);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] SolarProductionDto req)
        {
            var record = new SolarProduction
            {
                BuildingId = req.BuildingId,
                RecordDate = req.RecordDate,
                SolarUnitProduced = req.SolarUnitProduced,
                ProductionWh = req.ProductionWh,
                ToBatteryWh = req.ToBatteryWh,
                ToGridWh = req.ToGridWh,
                ToHomeWh = req.ToHomeWh,
                ConsumptionWh = req.ConsumptionWh,
                FromBatteryWh = req.FromBatteryWh,
                FromGridWh = req.FromGridWh,
                FromSolarWh = req.FromSolarWh,
                Note = req.Note,
                RecordedBy = req.RecordedBy,
                DepartmentId = req.DepartmentId,
            };
            _context.SolarProductions.Add(record);
            await _context.SaveChangesAsync();
            return Ok(new { message = "บันทึกข้อมูล Solar สำเร็จ", id = record.Id });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] SolarProductionDto req)
        {
            var record = await _context.SolarProductions.FindAsync(id);
            if (record == null) return NotFound();

            record.BuildingId = req.BuildingId;
            record.RecordDate = req.RecordDate;
            record.SolarUnitProduced = req.SolarUnitProduced;
            record.ProductionWh = req.ProductionWh;
            record.ToBatteryWh = req.ToBatteryWh;
            record.ToGridWh = req.ToGridWh;
            record.ToHomeWh = req.ToHomeWh;
            record.ConsumptionWh = req.ConsumptionWh;
            record.FromBatteryWh = req.FromBatteryWh;
            record.FromGridWh = req.FromGridWh;
            record.FromSolarWh = req.FromSolarWh;
            record.Note = req.Note;
            record.RecordedBy = req.RecordedBy;
            record.DepartmentId = req.DepartmentId;

            await _context.SaveChangesAsync();
            return Ok(new { message = "แก้ไขข้อมูล Solar สำเร็จ" });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var record = await _context.SolarProductions.FindAsync(id);
            if (record == null) return NotFound();
            _context.SolarProductions.Remove(record);
            await _context.SaveChangesAsync();
            return Ok(new { message = "ลบสำเร็จ" });
        }
    }
}
