using EnergyApp.API.Data;
using EnergyApp.API.Models.Office;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyApp.API.Controllers.Office
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class SarabanStatController : ControllerBase
    {
        private readonly AppDbContext _context;
        public SarabanStatController(AppDbContext context) { _context = context; }

        public class SarabanStatDto
        {
            public Guid? DepartmentId { get; set; }
            public string BookType { get; set; } = string.Empty;
            public string BookName { get; set; } = string.Empty;
            public DateTime RecordMonth { get; set; }
            public string ReceiverName { get; set; } = string.Empty;
            public int ReceivedCount { get; set; }
            public int InternalPaperCount { get; set; }
            public int InternalDigitalCount { get; set; }
            public int ExternalPaperCount { get; set; }
            public int ExternalDigitalCount { get; set; }
            public int ForwardedCount { get; set; }
            public string RecordedBy { get; set; } = string.Empty;
        }

        [HttpGet]
        public async Task<IActionResult> Get(
            [FromQuery] Guid? departmentId,
            [FromQuery] string? bookType,
            [FromQuery] string? bookName,
            [FromQuery] int? year,
            [FromQuery] int? month,
            [FromQuery] int skip = 0,
            [FromQuery] int take = 500)
        {
            var query = _context.SarabanStats.AsQueryable();

            if (departmentId.HasValue)
                query = query.Where(r => r.DepartmentId == departmentId);
            if (!string.IsNullOrEmpty(bookType))
                query = query.Where(r => r.BookType == bookType);
            if (!string.IsNullOrEmpty(bookName))
                query = query.Where(r => r.BookName == bookName);
            if (year.HasValue)
                query = query.Where(r => r.RecordMonth.Year == year.Value);
            if (month.HasValue)
                query = query.Where(r => r.RecordMonth.Month == month.Value);

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(r => r.RecordMonth)
                .ThenBy(r => r.BookName)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

            return Ok(new { total, items });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var item = await _context.SarabanStats.FindAsync(id);
            if (item == null) return NotFound();
            return Ok(item);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] SarabanStatDto req)
        {
            var record = new SarabanStat
            {
                DepartmentId = req.DepartmentId,
                BookType = req.BookType,
                BookName = req.BookName,
                RecordMonth = new DateTime(req.RecordMonth.Year, req.RecordMonth.Month, 1, 0, 0, 0, DateTimeKind.Utc),
                ReceiverName = req.ReceiverName,
                ReceivedCount = req.ReceivedCount,
                InternalPaperCount = req.InternalPaperCount,
                InternalDigitalCount = req.InternalDigitalCount,
                ExternalPaperCount = req.ExternalPaperCount,
                ExternalDigitalCount = req.ExternalDigitalCount,
                ForwardedCount = req.ForwardedCount,
                RecordedBy = req.RecordedBy,
            };
            _context.SarabanStats.Add(record);
            await _context.SaveChangesAsync();
            return Ok(new { message = "บันทึกสถิติสารบรรณสำเร็จ", id = record.Id });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] SarabanStatDto req)
        {
            var record = await _context.SarabanStats.FindAsync(id);
            if (record == null) return NotFound();

            record.DepartmentId = req.DepartmentId;
            record.BookType = req.BookType;
            record.BookName = req.BookName;
            record.RecordMonth = new DateTime(req.RecordMonth.Year, req.RecordMonth.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            record.ReceiverName = req.ReceiverName;
            record.ReceivedCount = req.ReceivedCount;
            record.InternalPaperCount = req.InternalPaperCount;
            record.InternalDigitalCount = req.InternalDigitalCount;
            record.ExternalPaperCount = req.ExternalPaperCount;
            record.ExternalDigitalCount = req.ExternalDigitalCount;
            record.ForwardedCount = req.ForwardedCount;
            record.RecordedBy = req.RecordedBy;

            await _context.SaveChangesAsync();
            return Ok(new { message = "แก้ไขสถิติสารบรรณสำเร็จ" });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var record = await _context.SarabanStats.FindAsync(id);
            if (record == null) return NotFound();
            _context.SarabanStats.Remove(record);
            await _context.SaveChangesAsync();
            return Ok(new { message = "ลบสำเร็จ" });
        }
    }
}
