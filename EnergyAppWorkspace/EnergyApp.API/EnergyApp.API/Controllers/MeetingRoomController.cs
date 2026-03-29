using EnergyApp.API.Data;
using EnergyApp.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyApp.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class MeetingRoomController : ControllerBase
    {
        private readonly AppDbContext _context;

        public MeetingRoomController(AppDbContext context)
        {
            _context = context;
        }

        public class MeetingRoomDto
        {
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var rooms = await _context.MeetingRooms
                .OrderBy(r => r.Name)
                .ToListAsync();

            return Ok(rooms);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] MeetingRoomDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { message = "กรุณากรอกชื่อห้องประชุม" });

            var room = new MeetingRoom
            {
                Name = dto.Name.Trim(),
                Description = dto.Description?.Trim() ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            };

            _context.MeetingRooms.Add(room);
            await _context.SaveChangesAsync();

            return Ok(new { message = "เพิ่มห้องประชุมสำเร็จ", data = room });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] MeetingRoomDto dto)
        {
            var room = await _context.MeetingRooms.FindAsync(id);
            if (room == null) return NotFound(new { message = "ไม่พบข้อมูลห้องประชุม" });

            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { message = "กรุณากรอกชื่อห้องประชุม" });

            room.Name = dto.Name.Trim();
            room.Description = dto.Description?.Trim() ?? string.Empty;

            await _context.SaveChangesAsync();

            return Ok(new { message = "แก้ไขห้องประชุมสำเร็จ" });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var room = await _context.MeetingRooms.FindAsync(id);
            if (room == null) return NotFound(new { message = "ไม่พบข้อมูลห้องประชุม" });

            _context.MeetingRooms.Remove(room);
            await _context.SaveChangesAsync();

            return Ok(new { message = "ลบห้องประชุมสำเร็จ" });
        }
    }
}

