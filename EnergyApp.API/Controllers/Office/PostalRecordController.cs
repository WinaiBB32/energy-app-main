using EnergyApp.API.Data;
using EnergyApp.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyApp.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class PostalRecordController : ControllerBase
    {
        private readonly AppDbContext _context;
        public PostalRecordController(AppDbContext context) { _context = context; }

        public class PostalRecordDto
        {
            public Guid? DepartmentId { get; set; }
            public DateTime RecordMonth { get; set; }
            public int IncomingNormalMail { get; set; }
            public int IncomingRegisteredMail { get; set; }
            public int IncomingEmsMail { get; set; }
            public int IncomingTotalMail { get; set; }
            public int NormalMail { get; set; }
            public decimal NormalMailUnitPrice { get; set; }
            public int RegisteredMail { get; set; }
            public decimal RegisteredMailUnitPrice { get; set; }
            public int EmsMail { get; set; }
            public decimal EmsMailUnitPrice { get; set; }
            public int TotalMail { get; set; }
            public string RecordedBy { get; set; } = string.Empty;
        }

        [HttpGet]
        public async Task<IActionResult> Get(
            [FromQuery] Guid? departmentId,
            [FromQuery] string? recordType,
            [FromQuery] int skip = 0,
            [FromQuery] int take = 50)
        {
            var query = _context.PostalRecords.AsQueryable();

            if (departmentId.HasValue)
                query = query.Where(r => r.DepartmentId == departmentId);

            var normalizedType = (recordType ?? string.Empty).Trim().ToLowerInvariant();
            if (normalizedType == "incoming")
            {
                query = query.Where(r => r.IncomingTotalMail > 0 && r.TotalMail == 0);
            }
            else if (normalizedType == "outgoing")
            {
                query = query.Where(r => r.TotalMail > 0 && r.IncomingTotalMail == 0);
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
            var item = await _context.PostalRecords.FindAsync(id);
            if (item == null) return NotFound();
            return Ok(item);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PostalRecordDto req)
        {
            var record = new PostalRecord
            {
                DepartmentId = req.DepartmentId,
                RecordMonth = req.RecordMonth,
                IncomingNormalMail = req.IncomingNormalMail,
                IncomingRegisteredMail = req.IncomingRegisteredMail,
                IncomingEmsMail = req.IncomingEmsMail,
                IncomingTotalMail = req.IncomingTotalMail,
                NormalMail = req.NormalMail,
                NormalMailUnitPrice = req.NormalMailUnitPrice,
                RegisteredMail = req.RegisteredMail,
                RegisteredMailUnitPrice = req.RegisteredMailUnitPrice,
                EmsMail = req.EmsMail,
                EmsMailUnitPrice = req.EmsMailUnitPrice,
                TotalMail = req.TotalMail,
                RecordedBy = req.RecordedBy
            };
            _context.PostalRecords.Add(record);
            await _context.SaveChangesAsync();
            return Ok(new { message = "บันทึกข้อมูลไปรษณีย์สำเร็จ", id = record.Id });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] PostalRecordDto req)
        {
            var record = await _context.PostalRecords.FindAsync(id);
            if (record == null) return NotFound();

            record.DepartmentId = req.DepartmentId;
            record.RecordMonth = req.RecordMonth;
            record.IncomingNormalMail = req.IncomingNormalMail;
            record.IncomingRegisteredMail = req.IncomingRegisteredMail;
            record.IncomingEmsMail = req.IncomingEmsMail;
            record.IncomingTotalMail = req.IncomingTotalMail;
            record.NormalMail = req.NormalMail;
            record.NormalMailUnitPrice = req.NormalMailUnitPrice;
            record.RegisteredMail = req.RegisteredMail;
            record.RegisteredMailUnitPrice = req.RegisteredMailUnitPrice;
            record.EmsMail = req.EmsMail;
            record.EmsMailUnitPrice = req.EmsMailUnitPrice;
            record.TotalMail = req.TotalMail;
            record.RecordedBy = req.RecordedBy;

            await _context.SaveChangesAsync();
            return Ok(new { message = "แก้ไขข้อมูลไปรษณีย์สำเร็จ" });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var record = await _context.PostalRecords.FindAsync(id);
            if (record == null) return NotFound();
            _context.PostalRecords.Remove(record);
            await _context.SaveChangesAsync();
            return Ok(new { message = "ลบสำเร็จ" });
        }
    }
}
