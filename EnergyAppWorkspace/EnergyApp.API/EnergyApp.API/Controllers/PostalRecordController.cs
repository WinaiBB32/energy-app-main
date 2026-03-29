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
            public int NormalMail { get; set; }
            public int RegisteredMail { get; set; }
            public int EmsMail { get; set; }
            public int TotalMail { get; set; }
            public string RecordedBy { get; set; } = string.Empty;
        }

        [HttpGet]
        public async Task<IActionResult> Get(
            [FromQuery] Guid? departmentId,
            [FromQuery] int skip = 0,
            [FromQuery] int take = 50)
        {
            var query = _context.PostalRecords.AsQueryable();

            if (departmentId.HasValue)
                query = query.Where(r => r.DepartmentId == departmentId);

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
                NormalMail = req.NormalMail,
                RegisteredMail = req.RegisteredMail,
                EmsMail = req.EmsMail,
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
            record.NormalMail = req.NormalMail;
            record.RegisteredMail = req.RegisteredMail;
            record.EmsMail = req.EmsMail;
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
