using EnergyApp.API.Data;
using EnergyApp.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyApp.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class TelephoneRecordController : ControllerBase
    {
        private readonly AppDbContext _context;
        public TelephoneRecordController(AppDbContext context) { _context = context; }

        public class TelephoneRecordDto
        {
            public string DocReceiveNumber { get; set; } = string.Empty;
            public string DocNumber { get; set; } = string.Empty;
            public DateTime? BillingCycle { get; set; }
            public string PhoneNumber { get; set; } = string.Empty;
            public string ProviderName { get; set; } = string.Empty;
            public decimal UsageAmount { get; set; }
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
            var query = _context.TelephoneRecords.AsQueryable();

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
            var item = await _context.TelephoneRecords.FindAsync(id);
            if (item == null) return NotFound();
            return Ok(item);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] TelephoneRecordDto req)
        {
            var record = new TelephoneRecord
            {
                DocReceiveNumber = req.DocReceiveNumber,
                DocNumber = req.DocNumber,
                BillingCycle = req.BillingCycle,
                PhoneNumber = req.PhoneNumber,
                ProviderName = req.ProviderName,
                UsageAmount = req.UsageAmount,
                VatAmount = req.VatAmount,
                TotalAmount = req.TotalAmount,
                RecordedBy = req.RecordedBy
            };
            _context.TelephoneRecords.Add(record);
            await _context.SaveChangesAsync();
            return Ok(new { message = "บันทึกข้อมูลโทรศัพท์สำเร็จ", id = record.Id });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] TelephoneRecordDto req)
        {
            var record = await _context.TelephoneRecords.FindAsync(id);
            if (record == null) return NotFound();

            record.DocReceiveNumber = req.DocReceiveNumber;
            record.DocNumber = req.DocNumber;
            record.BillingCycle = req.BillingCycle;
            record.PhoneNumber = req.PhoneNumber;
            record.ProviderName = req.ProviderName;
            record.UsageAmount = req.UsageAmount;
            record.VatAmount = req.VatAmount;
            record.TotalAmount = req.TotalAmount;
            record.RecordedBy = req.RecordedBy;

            await _context.SaveChangesAsync();
            return Ok(new { message = "แก้ไขข้อมูลโทรศัพท์สำเร็จ" });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var record = await _context.TelephoneRecords.FindAsync(id);
            if (record == null) return NotFound();
            _context.TelephoneRecords.Remove(record);
            await _context.SaveChangesAsync();
            return Ok(new { message = "ลบสำเร็จ" });
        }
    }
}
