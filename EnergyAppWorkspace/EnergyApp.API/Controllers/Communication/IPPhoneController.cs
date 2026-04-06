using Microsoft.AspNetCore.Mvc;

namespace EnergyApp.API.Controllers
{
    [ApiController]
    [Route("api/v1/ipphone")] // กำหนด Route ให้ตรงกับที่ Frontend เรียกใช้งาน
    public class IPPhoneController : ControllerBase
    {
        // Endpoint: GET /api/v1/ipphone/my-extensions
        [HttpGet("my-extensions")]
        public IActionResult GetMyExtensions()
        {
            // ส่งค่า Array ว่างกลับไปก่อนเพื่อป้องกัน Error 404 ในหน้า PortalView
            // TODO: เมื่อมีการสร้างตารางสำหรับ IP Phone แล้ว ค่อยมาเขียนโค้ดดึงข้อมูลจาก Database ที่นี่
            return Ok(new object[] { });
        }
    }
}