using EnergyApp.API.Data;
using EnergyApp.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyApp.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class ServiceRequestController : ControllerBase
    {
        private readonly AppDbContext _context;
        public ServiceRequestController(AppDbContext context) { _context = context; }

        public class ServiceRequestDto
        {
            public string Title { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
            public string Priority { get; set; } = "normal";
            public string Status { get; set; } = "pending";
            public string Extension { get; set; } = string.Empty;
            public string RequesterName { get; set; } = string.Empty;
            public string RequesterEmail { get; set; } = string.Empty;
            public string RequesterUid { get; set; } = string.Empty;
            public string AssignedTo { get; set; } = string.Empty;
            public string Note { get; set; } = string.Empty;
        }

        public class ChatMessageDto
        {
            public string Text { get; set; } = string.Empty;
            public string SenderName { get; set; } = string.Empty;
            public string SenderEmail { get; set; } = string.Empty;
            public string SenderId { get; set; } = string.Empty;
            public string SenderRole { get; set; } = string.Empty;
        }

        [HttpGet]
        public async Task<IActionResult> Get(
            [FromQuery] string? status,
            [FromQuery] string? category,
            [FromQuery] string? requesterUid,
            [FromQuery] int skip = 0,
            [FromQuery] int take = 50)
        {
            var query = _context.ServiceRequests.AsQueryable();

            if (!string.IsNullOrEmpty(status))
                query = query.Where(r => r.Status == status);

            if (!string.IsNullOrEmpty(category))
                query = query.Where(r => r.Category == category);

            if (!string.IsNullOrEmpty(requesterUid))
                query = query.Where(r => r.RequesterUid == requesterUid);

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
            var item = await _context.ServiceRequests.FindAsync(id);
            if (item == null) return NotFound();
            return Ok(item);
        }

        [HttpGet("{id}/messages")]
        public async Task<IActionResult> GetMessages(Guid id)
        {
            var messages = await _context.ChatMessages
                .Where(m => m.ServiceRequestId == id)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();
            return Ok(messages);
        }

        [HttpPost("{id}/messages")]
        public async Task<IActionResult> AddMessage(Guid id, [FromBody] ChatMessageDto req)
        {
            var request = await _context.ServiceRequests.FindAsync(id);
            if (request == null) return NotFound();

            var message = new ChatMessage
            {
                ServiceRequestId = id,
                Text = req.Text,
                SenderName = req.SenderName,
                SenderEmail = req.SenderEmail,
                SenderId = req.SenderId,
                SenderRole = req.SenderRole
            };
            _context.ChatMessages.Add(message);
            await _context.SaveChangesAsync();
            return Ok(new { message = "ส่งข้อความสำเร็จ", id = message.Id });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ServiceRequestDto req)
        {
            var record = new ServiceRequest
            {
                Title = req.Title,
                Description = req.Description,
                Category = req.Category,
                Priority = req.Priority,
                Status = req.Status,
                Extension = req.Extension,
                RequesterName = req.RequesterName,
                RequesterEmail = req.RequesterEmail,
                RequesterUid = req.RequesterUid,
                AssignedTo = req.AssignedTo,
                Note = req.Note
            };
            _context.ServiceRequests.Add(record);
            await _context.SaveChangesAsync();
            return Ok(new { message = "สร้างคำขอรับบริการสำเร็จ", id = record.Id });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] ServiceRequestDto req)
        {
            var record = await _context.ServiceRequests.FindAsync(id);
            if (record == null) return NotFound();

            record.Title = req.Title;
            record.Description = req.Description;
            record.Category = req.Category;
            record.Priority = req.Priority;
            record.Status = req.Status;
            record.Extension = req.Extension;
            record.RequesterName = req.RequesterName;
            record.RequesterEmail = req.RequesterEmail;
            record.RequesterUid = req.RequesterUid;
            record.AssignedTo = req.AssignedTo;
            record.Note = req.Note;
            record.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(new { message = "แก้ไขคำขอรับบริการสำเร็จ" });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var record = await _context.ServiceRequests.FindAsync(id);
            if (record == null) return NotFound();
            _context.ServiceRequests.Remove(record);
            await _context.SaveChangesAsync();
            return Ok(new { message = "ลบสำเร็จ" });
        }
    }
}
