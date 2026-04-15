using EnergyApp.API.Data;
using EnergyApp.API.DTOs.Office;
using EnergyApp.API.Models.Office;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyApp.API.Controllers.Office
{
    [Authorize]
    [ApiController]
    [Route("api/v1/office/vehicles")]
    public class VehicleRecordController : ControllerBase
    {
        private readonly AppDbContext _context;
        private const int MaxVehiclesPerPerson = 5;

        public VehicleRecordController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<VehicleRecord>>> GetAll()
        {
            var records = await _context.VehicleRecords
                .OrderBy(v => v.FullName)
                .ThenBy(v => v.CreatedAtUtc)
                .ToListAsync();
            return Ok(records);
        }

        [HttpPost]
        public async Task<ActionResult<VehicleRecord>> Create([FromBody] VehicleRecordDto dto)
        {
            // ตรวจสอบไม่เกิน 5 คันต่อคน (FaceScanId)
            var count = await _context.VehicleRecords
                .CountAsync(v => v.FaceScanId == dto.FaceScanId);

            if (count >= MaxVehiclesPerPerson)
                return BadRequest(new { message = $"ไม่สามารถเพิ่มได้ เนื่องจากผู้ใช้นี้มีรถยนต์ครบ {MaxVehiclesPerPerson} คันแล้ว" });

            var vehicle = new VehicleRecord
            {
                FaceScanId = dto.FaceScanId,
                FullName = dto.FullName,
                Position = dto.Position,
                Department = dto.Department,
                PhoneNumber = dto.PhoneNumber,
                LicensePlate = dto.LicensePlate,
                Brand = dto.Brand,
                Model = dto.Model,
                Province = dto.Province
            };

            _context.VehicleRecords.Add(vehicle);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetAll), new { id = vehicle.Id }, vehicle);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<VehicleRecord>> Update(int id, [FromBody] VehicleRecordDto dto)
        {
            var vehicle = await _context.VehicleRecords.FindAsync(id);
            if (vehicle == null) return NotFound();

            vehicle.FaceScanId = dto.FaceScanId;
            vehicle.FullName = dto.FullName;
            vehicle.Position = dto.Position;
            vehicle.Department = dto.Department;
            vehicle.PhoneNumber = dto.PhoneNumber;
            vehicle.LicensePlate = dto.LicensePlate;
            vehicle.Brand = dto.Brand;
            vehicle.Model = dto.Model;
            vehicle.Province = dto.Province;
            vehicle.UpdatedAtUtc = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(vehicle);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var vehicle = await _context.VehicleRecords.FindAsync(id);
            if (vehicle == null) return NotFound();

            _context.VehicleRecords.Remove(vehicle);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
