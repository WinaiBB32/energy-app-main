using EnergyApp.API.Data;
using EnergyApp.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

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

        [HttpGet("logs")]
        public async Task<IActionResult> GetLogsByDateRange(
            [FromQuery] string? fromDate,
            [FromQuery] string? toDate)
        {
            var query = _db.IPPhoneCallLogs.AsQueryable();

            if (!string.IsNullOrEmpty(fromDate) && DateTime.TryParse(fromDate, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var fd))
                query = query.Where(l => l.ReportMonth >= fd.ToUniversalTime());

            if (!string.IsNullOrEmpty(toDate) && DateTime.TryParse(toDate, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var td))
                query = query.Where(l => l.ReportMonth <= td.ToUniversalTime());

            var logs = await query.OrderBy(l => l.ReportMonth).ThenBy(l => l.Extension).ToListAsync();
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

        public class UpdateLogDto
        {
            public int AnsweredInbound { get; set; }
            public int NoAnswerInbound { get; set; }
            public int BusyInbound { get; set; }
            public int FailedInbound { get; set; }
            public int VoicemailInbound { get; set; }
            public int TotalInbound { get; set; }
            public int AnsweredOutbound { get; set; }
            public int NoAnswerOutbound { get; set; }
            public int BusyOutbound { get; set; }
            public int FailedOutbound { get; set; }
            public int VoicemailOutbound { get; set; }
            public int TotalOutbound { get; set; }
            public int TotalCalls { get; set; }
            public string TotalTalkDuration { get; set; } = string.Empty;
        }

        [HttpPut("logs/{logId}")]
        public async Task<IActionResult> UpdateLog(string logId, [FromBody] UpdateLogDto dto)
        {
            var log = await _db.IPPhoneCallLogs.FindAsync(logId);
            if (log == null) return NotFound();

            log.AnsweredInbound = dto.AnsweredInbound;
            log.NoAnswerInbound = dto.NoAnswerInbound;
            log.BusyInbound = dto.BusyInbound;
            log.FailedInbound = dto.FailedInbound;
            log.VoicemailInbound = dto.VoicemailInbound;
            log.TotalInbound = dto.TotalInbound;
            log.AnsweredOutbound = dto.AnsweredOutbound;
            log.NoAnswerOutbound = dto.NoAnswerOutbound;
            log.BusyOutbound = dto.BusyOutbound;
            log.FailedOutbound = dto.FailedOutbound;
            log.VoicemailOutbound = dto.VoicemailOutbound;
            log.TotalOutbound = dto.TotalOutbound;
            log.TotalCalls = dto.TotalCalls;
            log.TotalTalkDuration = dto.TotalTalkDuration;

            await _db.SaveChangesAsync();
            return Ok(log);
        }
    }
}
