

using EnergyApp.API.Data;
using EnergyApp.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace EnergyApp.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/v1/[controller]")]
    public class SolarProductionController : ControllerBase
    {
        private readonly AppDbContext _context;
        public SolarProductionController(AppDbContext context) { _context = context; }

        public class SolarProductionDto
        {
            public Guid? BuildingId { get; set; }
            public DateTime? RecordDate { get; set; }
            public decimal SolarUnitProduced { get; set; }
            public decimal ProductionWh { get; set; }
            public decimal ToBatteryWh { get; set; }
            public decimal ToGridWh { get; set; }
            public decimal ToHomeWh { get; set; }
            public decimal ConsumptionWh { get; set; }
            public decimal FromBatteryWh { get; set; }
            public decimal FromGridWh { get; set; }
            public decimal FromSolarWh { get; set; }
            public string Note { get; set; } = string.Empty;
            public string RecordedBy { get; set; } = string.Empty;
            public string DepartmentId { get; set; } = string.Empty;
        }

        /// <summary>
        /// รวม Consumption (kWh) ตามช่วงเวลาที่เลือก
        /// </summary>
        [HttpGet("summary-consumption")]
        public async Task<IActionResult> GetSummaryConsumption(
            [FromQuery] Guid? buildingId,
            [FromQuery] string? fromDate,
            [FromQuery] string? toDate)
        {
            var query = _context.SolarProductions.AsQueryable();
            if (buildingId.HasValue)
                query = query.Where(r => r.BuildingId == buildingId);
            if (!string.IsNullOrEmpty(fromDate) && DateTime.TryParse(fromDate, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var fd))
                query = query.Where(r => r.RecordDate >= fd);
            if (!string.IsNullOrEmpty(toDate) && DateTime.TryParse(toDate, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var td))
                query = query.Where(r => r.RecordDate <= td);

            var totalConsumptionWh = await query.SumAsync(r => r.ConsumptionWh);
            var totalConsumptionKWh = totalConsumptionWh / 1000.0m;
            return Ok(new { totalConsumptionKWh });
        }

        [HttpGet]
        public async Task<IActionResult> Get(
            [FromQuery] Guid? buildingId,
            [FromQuery] string? fromDate,
            [FromQuery] string? toDate,
            [FromQuery] int skip = 0,
            [FromQuery] int take = 50)
        {
            var query = _context.SolarProductions.AsQueryable();

            if (buildingId.HasValue)
                query = query.Where(r => r.BuildingId == buildingId);

            if (!string.IsNullOrEmpty(fromDate) && DateTime.TryParse(fromDate, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var fd))
                query = query.Where(r => r.RecordDate >= fd);

            if (!string.IsNullOrEmpty(toDate) && DateTime.TryParse(toDate, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var td))
                query = query.Where(r => r.RecordDate <= td);

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

            return Ok(new { total, items });
        }

        /// <summary>
        /// แนวโน้ม Solar Unit รายเดือน (Unit)
        /// </summary>
        [HttpGet("monthly-unit-trend")]
        public async Task<IActionResult> GetMonthlyUnitTrend([
            FromQuery] Guid? buildingId,
            [FromQuery] string? fromDate,
            [FromQuery] string? toDate)
        {
            var query = _context.SolarProductions.AsQueryable();
            if (buildingId.HasValue)
                query = query.Where(r => r.BuildingId == buildingId);
            if (!string.IsNullOrEmpty(fromDate) && DateTime.TryParse(fromDate, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var fd))
                query = query.Where(r => r.RecordDate >= fd);
            if (!string.IsNullOrEmpty(toDate) && DateTime.TryParse(toDate, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var td))
                query = query.Where(r => r.RecordDate <= td);

            var result = await query
                .Where(r => r.RecordDate.HasValue)
                .GroupBy(r => new { Year = r.RecordDate!.Value.Year, Month = r.RecordDate.Value.Month })
                .Select(g => new
                {
                    year = g.Key.Year,
                    month = g.Key.Month,
                    label = g.Key.Year + "-" + g.Key.Month.ToString("D2"),
                    totalUnit = g.Sum(x => x.SolarUnitProduced)
                })
                .OrderBy(x => x.year).ThenBy(x => x.month)
                .ToListAsync();
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var item = await _context.SolarProductions.FindAsync(id);
            if (item == null) return NotFound();
            return Ok(item);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] SolarProductionDto req)
        {
            if (req.BuildingId == null || req.BuildingId == Guid.Empty)
                return BadRequest(new { message = "กรุณาเลือกอาคาร" });

            var record = new SolarProduction
            {
                BuildingId = req.BuildingId,
                RecordDate = req.RecordDate,
                SolarUnitProduced = req.SolarUnitProduced,
                ProductionWh = req.ProductionWh,
                ToBatteryWh = req.ToBatteryWh,
                ToGridWh = req.ToGridWh,
                ToHomeWh = req.ToHomeWh,
                ConsumptionWh = req.ConsumptionWh,
                FromBatteryWh = req.FromBatteryWh,
                FromGridWh = req.FromGridWh,
                FromSolarWh = req.FromSolarWh,
                Note = req.Note,
                RecordedBy = req.RecordedBy,
                DepartmentId = req.DepartmentId,
            };
            _context.SolarProductions.Add(record);
            await _context.SaveChangesAsync();
            return Ok(new { message = "บันทึกข้อมูล Solar สำเร็จ", id = record.Id });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] SolarProductionDto req)
        {
            var record = await _context.SolarProductions.FindAsync(id);
            if (record == null) return NotFound();
            if (req.BuildingId == null || req.BuildingId == Guid.Empty)
                return BadRequest(new { message = "กรุณาเลือกอาคาร" });

            record.BuildingId = req.BuildingId;
            record.RecordDate = req.RecordDate;
            record.SolarUnitProduced = req.SolarUnitProduced;
            record.ProductionWh = req.ProductionWh;
            record.ToBatteryWh = req.ToBatteryWh;
            record.ToGridWh = req.ToGridWh;
            record.ToHomeWh = req.ToHomeWh;
            record.ConsumptionWh = req.ConsumptionWh;
            record.FromBatteryWh = req.FromBatteryWh;
            record.FromGridWh = req.FromGridWh;
            record.FromSolarWh = req.FromSolarWh;
            record.Note = req.Note;
            record.RecordedBy = req.RecordedBy;
            record.DepartmentId = req.DepartmentId;

            await _context.SaveChangesAsync();
            return Ok(new { message = "แก้ไขข้อมูล Solar สำเร็จ" });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var record = await _context.SolarProductions.FindAsync(id);
            if (record == null) return NotFound();
            _context.SolarProductions.Remove(record);
            await _context.SaveChangesAsync();
            return Ok(new { message = "ลบสำเร็จ" });
        }
        /// <summary>
        /// อัปโหลดข้อมูล Solar (รองรับเฉพาะ .csv เท่านั้น และตรวจสอบความปลอดภัย)
        /// </summary>
        public class CsvUploadRequest { public IFormFile File { get; set; } = null!; }

        [HttpPost("upload")]
        [RequestSizeLimit(2 * 1024 * 1024)] // จำกัดขนาดไฟล์ 2MB
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadCsv([FromForm] CsvUploadRequest request)
        {
            // 1. ตรวจสอบ Content-Type และนามสกุลไฟล์
            var file = request.File;
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "กรุณาเลือกไฟล์ .csv" });
            if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "อนุญาตเฉพาะไฟล์ .csv เท่านั้น" });
            if (file.Length > 2 * 1024 * 1024)
                return BadRequest(new { message = "ขนาดไฟล์ต้องไม่เกิน 2MB" });
            if (file.ContentType != "text/csv" && file.ContentType != "application/vnd.ms-excel")
                return BadRequest(new { message = "Content-Type ไม่ถูกต้อง ต้องเป็น text/csv เท่านั้น" });

            // 2. อ่านไฟล์และตรวจสอบ header (กัน code injection/โจมตี)
            using var reader = new StreamReader(file.OpenReadStream());
            var headerLine = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(headerLine))
                return BadRequest(new { message = "ไฟล์ไม่มี header หรือว่างเปล่า" });

            // ตัวอย่าง header ที่ถูกต้อง (ต้องปรับตาม schema จริง)
            var expectedHeaders = new[] {
                "BuildingId","RecordDate","SolarUnitProduced","ProductionWh","ToBatteryWh","ToGridWh","ToHomeWh","ConsumptionWh","FromBatteryWh","FromGridWh","FromSolarWh","Note","RecordedBy","DepartmentId"
            };
            var headers = headerLine.Split(',').Select(h => h.Trim('"')).ToArray();
            if (!expectedHeaders.SequenceEqual(headers))
                return BadRequest(new { message = "header ไม่ถูกต้อง กรุณาใช้ template ที่ระบบกำหนด" });

            // 3. ตรวจสอบเนื้อหาแต่ละบรรทัด (กัน script, code injection, ข้อมูลผิด format)
            int lineNo = 1;
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                lineNo++;
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line.Contains("<script", StringComparison.OrdinalIgnoreCase) || line.Contains("<?php") || line.Contains("--"))
                    return BadRequest(new { message = $"พบเนื้อหาต้องสงสัยที่บรรทัด {lineNo}" });
                // สามารถเพิ่ม validation เพิ่มเติมได้ เช่น ตรวจสอบจำนวน column, format วันที่ ฯลฯ
            }

            // ถ้าผ่าน validation ทั้งหมด
            return Ok(new { message = "ไฟล์ผ่านการตรวจสอบเบื้องต้น สามารถนำเข้าได้" });
        }
    }
}
