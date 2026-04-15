using EnergyApp.API.Data;
using EnergyApp.API.Models.Office;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyApp.API.Controllers.Office
{
    [Authorize]
    [ApiController]
    [Route("api/v1/office/vehicle-provinces")]
    public class VehicleProvinceController : ControllerBase
    {
        private readonly AppDbContext _context;

        public VehicleProvinceController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<VehicleProvince>>> GetAll()
        {
            var list = await _context.VehicleProvinces
                .OrderBy(p => p.Name)
                .ToListAsync();
            return Ok(list);
        }

        [HttpPost]
        public async Task<ActionResult<VehicleProvince>> Create([FromBody] VehicleMasterDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { message = "กรุณากรอกชื่อจังหวัด" });

            var item = new VehicleProvince { Name = dto.Name.Trim() };
            _context.VehicleProvinces.Add(item);
            await _context.SaveChangesAsync();
            return Ok(item);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<VehicleProvince>> Update(int id, [FromBody] VehicleMasterDto dto)
        {
            var item = await _context.VehicleProvinces.FindAsync(id);
            if (item == null) return NotFound();

            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { message = "กรุณากรอกชื่อจังหวัด" });

            item.Name = dto.Name.Trim();
            await _context.SaveChangesAsync();
            return Ok(item);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _context.VehicleProvinces.FindAsync(id);
            if (item == null) return NotFound();

            _context.VehicleProvinces.Remove(item);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
