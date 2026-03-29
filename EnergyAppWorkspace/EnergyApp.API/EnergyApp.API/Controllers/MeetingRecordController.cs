using EnergyApp.API.Data;
using EnergyApp.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyApp.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class MeetingRecordController : ControllerBase
    {
        private readonly AppDbContext _context;
        public MeetingRecordController(AppDbContext context) { _context = context; }

        public class MeetingRecordDto
        {
            public Guid RoomId { get; set; }
            public DateTime RecordMonth { get; set; }
            public int UsageCount { get; set; }
            public string RecordedByUid { get; set; } = string.Empty;
            public string RecordedByName { get; set; } = string.Empty;
        }

        [HttpGet]
        public async Task<IActionResult> Get(
            [FromQuery] Guid? roomId,
            [FromQuery] string? month,
            [FromQuery] int skip = 0,
            [FromQuery] int take = 50)
        {
            var query = _context.MeetingRecords.AsQueryable();

            if (roomId.HasValue)
                query = query.Where(r => r.RoomId == roomId);

            if (!string.IsNullOrEmpty(month) && DateTime.TryParse(month, out var monthDate))
            {
                query = query.Where(r =>
                    r.RecordMonth.Year == monthDate.Year &&
                    r.RecordMonth.Month == monthDate.Month);
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
            var item = await _context.MeetingRecords.FindAsync(id);
            if (item == null) return NotFound();
            return Ok(item);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] MeetingRecordDto req)
        {
            var record = new MeetingRecord
            {
                RoomId = req.RoomId,
                RecordMonth = req.RecordMonth,
                UsageCount = req.UsageCount,
                RecordedByUid = req.RecordedByUid,
                RecordedByName = req.RecordedByName
            };
            _context.MeetingRecords.Add(record);
            await _context.SaveChangesAsync();
            return Ok(new { message = "บันทึกข้อมูลการใช้ห้องประชุมสำเร็จ", id = record.Id });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] MeetingRecordDto req)
        {
            var record = await _context.MeetingRecords.FindAsync(id);
            if (record == null) return NotFound();

            record.RoomId = req.RoomId;
            record.RecordMonth = req.RecordMonth;
            record.UsageCount = req.UsageCount;
            record.RecordedByUid = req.RecordedByUid;
            record.RecordedByName = req.RecordedByName;

            await _context.SaveChangesAsync();
            return Ok(new { message = "แก้ไขข้อมูลการใช้ห้องประชุมสำเร็จ" });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var record = await _context.MeetingRecords.FindAsync(id);
            if (record == null) return NotFound();
            _context.MeetingRecords.Remove(record);
            await _context.SaveChangesAsync();
            return Ok(new { message = "ลบสำเร็จ" });
        }
    }
}
