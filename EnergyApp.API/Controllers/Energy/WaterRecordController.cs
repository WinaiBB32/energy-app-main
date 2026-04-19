using EnergyApp.API.Data;
using EnergyApp.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace EnergyApp.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class WaterRecordController : ControllerBase
    {
        private readonly AppDbContext _context;
        public WaterRecordController(AppDbContext context) { _context = context; }

        public class WaterRecordDto
        {
            public string DocReceiveNumber { get; set; } = string.Empty;
            public string DocNumber { get; set; } = string.Empty;
            public string InvoiceNumber { get; set; } = string.Empty;
            public DateTime? BillingCycle { get; set; }
            public string RegistrationNo { get; set; } = string.Empty;
            public string UserName { get; set; } = string.Empty;
            public string UsageAddress { get; set; } = string.Empty;
            public DateTime? ReadingDate { get; set; }
            public decimal CurrentMeter { get; set; }
            public decimal WaterUnitUsed { get; set; }
            public decimal RawWaterCharge { get; set; }
            public decimal WaterAmount { get; set; }
            public decimal MonthlyServiceFee { get; set; }
            public decimal VatAmount { get; set; }
            public decimal TotalAmount { get; set; }
            public string RecordedBy { get; set; } = string.Empty;
        }

        [HttpGet]
        public async Task<IActionResult> Get(
            [FromQuery] string? fromDate,
            [FromQuery] string? toDate,
            [FromQuery] int skip = 0,
            [FromQuery] int take = 50)
        {
            var query = _context.WaterRecords.AsQueryable();

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
            var item = await _context.WaterRecords.FindAsync(id);
            if (item == null) return NotFound();
            return Ok(item);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] WaterRecordDto req)
        {
            var record = new WaterRecord
            {
                DocReceiveNumber = req.DocReceiveNumber,
                DocNumber = req.DocNumber,
                InvoiceNumber = req.InvoiceNumber,
                BillingCycle = req.BillingCycle,
                RegistrationNo = req.RegistrationNo,
                UserName = req.UserName,
                UsageAddress = req.UsageAddress,
                ReadingDate = req.ReadingDate,
                CurrentMeter = req.CurrentMeter,
                WaterUnitUsed = req.WaterUnitUsed,
                RawWaterCharge = req.RawWaterCharge,
                WaterAmount = req.WaterAmount,
                MonthlyServiceFee = req.MonthlyServiceFee,
                VatAmount = req.VatAmount,
                TotalAmount = req.TotalAmount,
                RecordedBy = req.RecordedBy
            };
            _context.WaterRecords.Add(record);
            await _context.SaveChangesAsync();
            return Ok(new { message = "บันทึกข้อมูลน้ำประปาสำเร็จ", id = record.Id });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] WaterRecordDto req)
        {
            var record = await _context.WaterRecords.FindAsync(id);
            if (record == null) return NotFound();

            record.DocReceiveNumber = req.DocReceiveNumber;
            record.DocNumber = req.DocNumber;
            record.InvoiceNumber = req.InvoiceNumber;
            record.BillingCycle = req.BillingCycle;
            record.RegistrationNo = req.RegistrationNo;
            record.UserName = req.UserName;
            record.UsageAddress = req.UsageAddress;
            record.ReadingDate = req.ReadingDate;
            record.CurrentMeter = req.CurrentMeter;
            record.WaterUnitUsed = req.WaterUnitUsed;
            record.RawWaterCharge = req.RawWaterCharge;
            record.WaterAmount = req.WaterAmount;
            record.MonthlyServiceFee = req.MonthlyServiceFee;
            record.VatAmount = req.VatAmount;
            record.TotalAmount = req.TotalAmount;
            record.RecordedBy = req.RecordedBy;

            await _context.SaveChangesAsync();
            return Ok(new { message = "แก้ไขข้อมูลน้ำประปาสำเร็จ" });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var record = await _context.WaterRecords.FindAsync(id);
            if (record == null) return NotFound();
            _context.WaterRecords.Remove(record);
            await _context.SaveChangesAsync();
            return Ok(new { message = "ลบสำเร็จ" });
        }
    }
}
