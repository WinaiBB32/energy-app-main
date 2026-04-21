using EnergyApp.API.Data;
using EnergyApp.API.DTOs.Office;
using EnergyApp.API.Models.Office;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

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

        private static string ResolveId(string? id) =>
            string.IsNullOrWhiteSpace(id) ? $"EXT-{Guid.NewGuid().ToString("N")[..8].ToUpper()}" : id;

        [HttpPost]
        public async Task<ActionResult<VehicleRecord>> Create([FromBody] VehicleRecordDto dto)
        {
            var faceScanId = ResolveId(dto.FaceScanId);

            // ตรวจสอบไม่เกิน 5 คันต่อคน (FaceScanId) เฉพาะรหัสที่ระบุ (ไม่ใช่ EXT-)
            if (!faceScanId.StartsWith("EXT-"))
            {
                var count = await _context.VehicleRecords
                    .CountAsync(v => v.FaceScanId == faceScanId);
                if (count >= MaxVehiclesPerPerson)
                    return BadRequest(new { message = $"ไม่สามารถเพิ่มได้ เนื่องจากผู้ใช้นี้มีรถยนต์ครบ {MaxVehiclesPerPerson} คันแล้ว" });
            }

            var vehicle = new VehicleRecord
            {
                FaceScanId = faceScanId,
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

        [HttpPost("import-csv")]
        [RequestSizeLimit(5 * 1024 * 1024)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ImportCsv(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "ไม่พบไฟล์" });

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (ext != ".csv")
                return BadRequest(new { message = "รองรับเฉพาะไฟล์ .csv" });

            var imported = 0;
            var skipped = 0;
            var errors = new List<string>();

            using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
            var lineNum = 0;

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                lineNum++;
                if (lineNum == 1) continue; // skip header

                if (string.IsNullOrWhiteSpace(line)) continue;

                var cols = line.Split(',');
                if (cols.Length < 9)
                {
                    errors.Add($"แถว {lineNum}: คอลัมน์ไม่ครบ (ต้องการ 9 คอลัมน์)");
                    skipped++;
                    continue;
                }

                static string Trim(string s, int max) => s.Length > max ? s[..max] : s;

                var faceScanId = Trim(cols[0].Trim().Trim('"'), 50);
                var fullName   = Trim(cols[1].Trim().Trim('"'), 150);
                var position   = Trim(cols[2].Trim().Trim('"'), 100);
                var department = Trim(cols[3].Trim().Trim('"'), 100);
                var phone      = Trim(cols[4].Trim().Trim('"'), 20);
                var plate      = Trim(cols[5].Trim().Trim('"'), 20);
                var brand      = Trim(cols[6].Trim().Trim('"'), 50);
                var model      = Trim(cols[7].Trim().Trim('"'), 50);
                var province   = Trim(cols[8].Trim().Trim('"'), 100);

                if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(plate))
                {
                    errors.Add($"แถว {lineNum}: ข้อมูลไม่ครบ (ชื่อ/ทะเบียน)");
                    skipped++;
                    continue;
                }

                faceScanId = ResolveId(faceScanId);

                var count = !faceScanId.StartsWith("EXT-")
                    ? await _context.VehicleRecords.CountAsync(v => v.FaceScanId == faceScanId)
                    : 0;
                if (count >= MaxVehiclesPerPerson)
                {
                    errors.Add($"แถว {lineNum}: {fullName} ({faceScanId}) มีรถครบ {MaxVehiclesPerPerson} คันแล้ว");
                    skipped++;
                    continue;
                }

                _context.VehicleRecords.Add(new VehicleRecord
                {
                    FaceScanId   = faceScanId,
                    FullName     = fullName,
                    Position     = position,
                    Department   = department,
                    PhoneNumber  = phone,
                    LicensePlate = plate,
                    Brand        = brand,
                    Model        = model,
                    Province     = province,
                });
                imported++;
            }

            await _context.SaveChangesAsync();
            return Ok(new { imported, skipped, errors });
        }
    }
}
