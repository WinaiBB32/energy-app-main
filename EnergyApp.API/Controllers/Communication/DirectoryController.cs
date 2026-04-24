using EnergyApp.API.Data;
using EnergyApp.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyApp.API.Controllers
{
    [ApiController]
    [Route("api/v1/Directory")]
    public class DirectoryController : ControllerBase
    {
        private readonly AppDbContext _context;
        public DirectoryController(AppDbContext context) { _context = context; }

        public class DirectoryEntryDto
        {
            public string OwnerName { get; set; } = string.Empty;
            public string Location { get; set; } = string.Empty;
            public string IpPhoneNumber { get; set; } = string.Empty;
            public string AnalogNumber { get; set; } = string.Empty;
            public string DeviceCode { get; set; } = string.Empty;
            public Guid? DepartmentId { get; set; }
            public string Workgroup { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string Keywords { get; set; } = string.Empty;
            public bool IsPublished { get; set; } = true;
        }

        [HttpGet]
        public async Task<IActionResult> Get(
            [FromQuery] string? keyword,
            [FromQuery] string? workgroup,
            [FromQuery] Guid? departmentId,
            [FromQuery] int skip = 0,
            [FromQuery] int take = 50)
        {
            var query = _context.IPPhoneDirectories.AsQueryable();

            if (!string.IsNullOrEmpty(keyword))
                query = query.Where(r =>
                    r.OwnerName.Contains(keyword) ||
                    r.Keywords.Contains(keyword) ||
                    r.IpPhoneNumber.Contains(keyword) ||
                    r.AnalogNumber.Contains(keyword) ||
                    r.Location.Contains(keyword));

            if (!string.IsNullOrEmpty(workgroup))
                query = query.Where(r => r.Workgroup == workgroup);

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
            var item = await _context.IPPhoneDirectories.FindAsync(id);
            if (item == null) return NotFound();
            return Ok(item);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] DirectoryEntryDto req)
        {
            var record = new IPPhoneDirectory
            {
                OwnerName = req.OwnerName,
                Location = req.Location,
                IpPhoneNumber = req.IpPhoneNumber,
                AnalogNumber = req.AnalogNumber,
                DeviceCode = req.DeviceCode,
                DepartmentId = req.DepartmentId,
                Workgroup = req.Workgroup,
                Description = req.Description,
                Keywords = req.Keywords,
                IsPublished = req.IsPublished
            };
            _context.IPPhoneDirectories.Add(record);
            await _context.SaveChangesAsync();
            return Ok(new { message = "บันทึกรายชื่อสำเร็จ", id = record.Id });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] DirectoryEntryDto req)
        {
            var record = await _context.IPPhoneDirectories.FindAsync(id);
            if (record == null) return NotFound();

            record.OwnerName = req.OwnerName;
            record.Location = req.Location;
            record.IpPhoneNumber = req.IpPhoneNumber;
            record.AnalogNumber = req.AnalogNumber;
            record.DeviceCode = req.DeviceCode;
            record.DepartmentId = req.DepartmentId;
            record.Workgroup = req.Workgroup;
            record.Description = req.Description;
            record.Keywords = req.Keywords;
            record.IsPublished = req.IsPublished;

            await _context.SaveChangesAsync();
            return Ok(new { message = "แก้ไขรายชื่อสำเร็จ" });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var record = await _context.IPPhoneDirectories.FindAsync(id);
            if (record == null) return NotFound();
            _context.IPPhoneDirectories.Remove(record);
            await _context.SaveChangesAsync();
            return Ok(new { message = "ลบสำเร็จ" });
        }
    }
}
