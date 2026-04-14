using EnergyApp.API.Data;
using EnergyApp.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyApp.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class SarabanRecordController : ControllerBase
    {
        private readonly AppDbContext _context;
        public SarabanRecordController(AppDbContext context) { _context = context; }

        public class SarabanRecordDto
        {
            public string DocType { get; set; } = "BOOK";
            public string DocNumber { get; set; } = string.Empty;
            public string Subject { get; set; } = string.Empty;
            public string FromOrganization { get; set; } = string.Empty;
            public string ToOrganization { get; set; } = string.Empty;
            public DateTime? ReceivedDate { get; set; }
            public DateTime? DueDate { get; set; }
            public string Priority { get; set; } = "normal";
            public string Status { get; set; } = UserStatus.Pending;
            public string AssignedTo { get; set; } = string.Empty;
            public string Note { get; set; } = string.Empty;
            public string RecordedBy { get; set; } = string.Empty;
            public Guid? DepartmentId { get; set; }
        }

        [HttpGet]
        public async Task<IActionResult> Get(
            [FromQuery] Guid? departmentId,
            [FromQuery] string? docType,
            [FromQuery] string? status,
            [FromQuery] int skip = 0,
            [FromQuery] int take = 50)
        {
            var query = _context.SarabanRecords.AsQueryable();

            if (departmentId.HasValue)
                query = query.Where(r => r.DepartmentId == departmentId);

            if (!string.IsNullOrEmpty(docType))
                query = query.Where(r => r.DocType == docType);

            if (!string.IsNullOrEmpty(status))
                query = query.Where(r => r.Status == status);

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
            var item = await _context.SarabanRecords.FindAsync(id);
            if (item == null) return NotFound();
            return Ok(item);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] SarabanRecordDto req)
        {
            var record = new SarabanRecord
            {
                DocType = req.DocType,
                DocNumber = req.DocNumber,
                Subject = req.Subject,
                FromOrganization = req.FromOrganization,
                ToOrganization = req.ToOrganization,
                ReceivedDate = req.ReceivedDate,
                DueDate = req.DueDate,
                Priority = req.Priority,
                Status = req.Status,
                AssignedTo = req.AssignedTo,
                Note = req.Note,
                RecordedBy = req.RecordedBy,
                DepartmentId = req.DepartmentId
            };
            _context.SarabanRecords.Add(record);
            await _context.SaveChangesAsync();
            return Ok(new { message = "บันทึกสารบรรณสำเร็จ", id = record.Id });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] SarabanRecordDto req)
        {
            var record = await _context.SarabanRecords.FindAsync(id);
            if (record == null) return NotFound();

            record.DocType = req.DocType;
            record.DocNumber = req.DocNumber;
            record.Subject = req.Subject;
            record.FromOrganization = req.FromOrganization;
            record.ToOrganization = req.ToOrganization;
            record.ReceivedDate = req.ReceivedDate;
            record.DueDate = req.DueDate;
            record.Priority = req.Priority;
            record.Status = req.Status;
            record.AssignedTo = req.AssignedTo;
            record.Note = req.Note;
            record.RecordedBy = req.RecordedBy;
            record.DepartmentId = req.DepartmentId;

            await _context.SaveChangesAsync();
            return Ok(new { message = "แก้ไขสารบรรณสำเร็จ" });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var record = await _context.SarabanRecords.FindAsync(id);
            if (record == null) return NotFound();
            _context.SarabanRecords.Remove(record);
            await _context.SaveChangesAsync();
            return Ok(new { message = "ลบสำเร็จ" });
        }
    }
}
