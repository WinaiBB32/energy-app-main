using EnergyApp.API.Data;
using EnergyApp.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyApp.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class IPPhoneMonthStatController : ControllerBase
    {
        private readonly AppDbContext _db;
        public IPPhoneMonthStatController(AppDbContext db) { _db = db; }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int skip = 0, [FromQuery] int take = 100)
        {
            var query = _db.IPPhoneMonthStats.OrderByDescending(s => s.CreatedAt);
            var total = await query.CountAsync();
            var items = await query.Skip(skip).Take(take).ToListAsync();
            return Ok(new { total, items });
        }

        [HttpGet("{id}/logs")]
        public async Task<IActionResult> GetLogs(string id)
        {
            var logs = await _db.IPPhoneCallLogs
                .Where(l => l.StatId == id)
                .OrderBy(l => l.Extension)
                .ToListAsync();
            return Ok(logs);
        }

        public class CreateStatDto
        {
            public DateTime ReportMonth { get; set; }
            public string FileName { get; set; } = string.Empty;
            public string UploadedBy { get; set; } = string.Empty;
            public int TotalRecords { get; set; }
            public List<IPPhoneCallLog> Logs { get; set; } = new();
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateStatDto dto)
        {
            var stat = new IPPhoneMonthStat
            {
                ReportMonth = dto.ReportMonth,
                FileName = dto.FileName,
                UploadedBy = dto.UploadedBy,
                TotalRecords = dto.TotalRecords,
                CreatedAt = DateTime.UtcNow,
            };
            _db.IPPhoneMonthStats.Add(stat);

            foreach (var log in dto.Logs)
            {
                log.Id = Guid.NewGuid().ToString();
                log.StatId = stat.Id;
                log.ReportMonth = dto.ReportMonth;
                log.CreatedAt = DateTime.UtcNow;
                _db.IPPhoneCallLogs.Add(log);
            }

            await _db.SaveChangesAsync();
            return Ok(stat);
        }

        [HttpGet("check-duplicate")]
        public async Task<IActionResult> CheckDuplicate([FromQuery] int year, [FromQuery] int month)
        {
            var start = new DateTime(year, month, 1);
            var end = start.AddMonths(1);
            var exists = await _db.IPPhoneMonthStats
                .AnyAsync(s => s.ReportMonth >= start && s.ReportMonth < end);
            return Ok(new { exists });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var stat = await _db.IPPhoneMonthStats.FindAsync(id);
            if (stat == null) return NotFound();

            var logs = _db.IPPhoneCallLogs.Where(l => l.StatId == id);
            _db.IPPhoneCallLogs.RemoveRange(logs);
            _db.IPPhoneMonthStats.Remove(stat);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
