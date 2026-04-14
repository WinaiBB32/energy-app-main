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
    public class ElectricityBillController : ControllerBase
    {
        private readonly AppDbContext _context;
        public ElectricityBillController(AppDbContext context) { _context = context; }

        public class ElectricityBillDto
        {
            public string DocReceiveNumber { get; set; } = string.Empty;
            public string DocNumber { get; set; } = string.Empty;
            public string InvoiceNumber { get; set; } = string.Empty;
            public string MeterCode { get; set; } = string.Empty;
            public Guid? BuildingId { get; set; }
            public DateTime? BillingCycle { get; set; }
            public decimal OnPeakUnits { get; set; }
            public decimal OffPeakUnits { get; set; }
            public decimal PeaUnitUsed { get; set; }
            public decimal PeaAmount { get; set; }
            public decimal FtRate { get; set; }
            public decimal FtAmount { get; set; }
            public decimal MonthlyServiceFee { get; set; }
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
            var query = _context.ElectricityBills.AsQueryable();

            if (buildingId.HasValue)
                query = query.Where(r => r.BuildingId == buildingId);

            if (!string.IsNullOrEmpty(fromDate) && DateTime.TryParse(fromDate, out var fd))
                query = query.Where(r => r.BillingCycle >= fd);

            if (!string.IsNullOrEmpty(toDate) && DateTime.TryParse(toDate, out var td))
                query = query.Where(r => r.BillingCycle <= td);

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
            var item = await _context.ElectricityBills.FindAsync(id);
            if (item == null) return NotFound();
            return Ok(item);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ElectricityBillDto req)
        {
            if (req.BuildingId == null || req.BuildingId == Guid.Empty)
                return BadRequest(new { message = "กรุณาเลือกอาคาร" });

            var record = new ElectricityBill
            {
                DocReceiveNumber = req.DocReceiveNumber,
                DocNumber = req.DocNumber,
                InvoiceNumber = req.InvoiceNumber,
                MeterCode = req.MeterCode,
                BuildingId = req.BuildingId,
                BillingCycle = req.BillingCycle,
                OnPeakUnits = req.OnPeakUnits,
                OffPeakUnits = req.OffPeakUnits,
                PeaUnitUsed = req.OnPeakUnits + req.OffPeakUnits,
                PeaAmount = req.PeaAmount,
                FtRate = req.FtRate,
                FtAmount = req.FtAmount,
                MonthlyServiceFee = req.MonthlyServiceFee,
                Note = req.Note,
                RecordedBy = req.RecordedBy,
                DepartmentId = req.DepartmentId,
            };
            _context.ElectricityBills.Add(record);
            await _context.SaveChangesAsync();
            return Ok(new { message = "บันทึกข้อมูลบิลค่าไฟฟ้าสำเร็จ", id = record.Id });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] ElectricityBillDto req)
        {
            var record = await _context.ElectricityBills.FindAsync(id);
            if (record == null) return NotFound();
            if (req.BuildingId == null || req.BuildingId == Guid.Empty)
                return BadRequest(new { message = "กรุณาเลือกอาคาร" });

            record.DocReceiveNumber = req.DocReceiveNumber;
            record.DocNumber = req.DocNumber;
            record.InvoiceNumber = req.InvoiceNumber;
            record.MeterCode = req.MeterCode;
            record.BuildingId = req.BuildingId;
            record.BillingCycle = req.BillingCycle;
            record.OnPeakUnits = req.OnPeakUnits;
            record.OffPeakUnits = req.OffPeakUnits;
            record.PeaUnitUsed = req.OnPeakUnits + req.OffPeakUnits;
            record.PeaAmount = req.PeaAmount;
            record.FtRate = req.FtRate;
            record.FtAmount = req.FtAmount;
            record.MonthlyServiceFee = req.MonthlyServiceFee;
            record.Note = req.Note;
            record.RecordedBy = req.RecordedBy;
            record.DepartmentId = req.DepartmentId;

            await _context.SaveChangesAsync();
            return Ok(new { message = "แก้ไขข้อมูลบิลค่าไฟฟ้าสำเร็จ" });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var record = await _context.ElectricityBills.FindAsync(id);
            if (record == null) return NotFound();
            _context.ElectricityBills.Remove(record);
            await _context.SaveChangesAsync();
            return Ok(new { message = "ลบสำเร็จ" });
        }
        /// <summary>
        /// แนวโน้มหน่วยไฟฟ้ารายเดือน (Unit)
        /// </summary>
        [HttpGet("monthly-unit-trend")]
        public async Task<IActionResult> GetMonthlyUnitTrend([
            FromQuery] Guid? buildingId,
            [FromQuery] string? fromDate,
            [FromQuery] string? toDate)
        {
            var query = _context.ElectricityBills.AsQueryable();
            if (buildingId.HasValue)
                query = query.Where(r => r.BuildingId == buildingId);
            if (!string.IsNullOrEmpty(fromDate) && DateTime.TryParse(fromDate, out var fd))
                query = query.Where(r => r.BillingCycle >= fd);
            if (!string.IsNullOrEmpty(toDate) && DateTime.TryParse(toDate, out var td))
                query = query.Where(r => r.BillingCycle <= td);

            var result = await query
                .Where(r => r.BillingCycle.HasValue)
                .GroupBy(r => new { Year = r.BillingCycle!.Value.Year, Month = r.BillingCycle.Value.Month })
                .Select(g => new
                {
                    year = g.Key.Year,
                    month = g.Key.Month,
                    label = g.Key.Year + "-" + g.Key.Month.ToString("D2"),
                    totalUnit = g.Sum(x => x.PeaUnitUsed),
                    totalAmount = g.Sum(x => x.PeaAmount)
                })
                .OrderBy(x => x.year).ThenBy(x => x.month)
                .ToListAsync();
            return Ok(result);
        }
    }
}
