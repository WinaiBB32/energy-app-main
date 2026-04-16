namespace EnergyApp.API.Models.Office
{
    public class SarabanStat
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid? DepartmentId { get; set; }

        /// <summary>ประเภทเล่มทะเบียน เช่น รับ, ส่ง, เวียน</summary>
        public string BookType { get; set; } = string.Empty;

        /// <summary>ชื่อเล่มทะเบียน</summary>
        public string BookName { get; set; } = string.Empty;

        /// <summary>เดือน/ปี ที่บันทึก (เก็บเป็นวันที่ 1 ของเดือน)</summary>
        public DateTime RecordMonth { get; set; }

        /// <summary>รายชื่อผู้รับ</summary>
        public string ReceiverName { get; set; } = string.Empty;

        /// <summary>จำนวนเอกสารรับเข้าทั้งหมด</summary>
        public int ReceivedCount { get; set; }

        /// <summary>จำนวนเอกสารลงรับภายใน (กระดาษ)</summary>
        public int InternalPaperCount { get; set; }

        /// <summary>จำนวนเอกสารลงรับภายใน (ดิจิทัล)</summary>
        public int InternalDigitalCount { get; set; }

        /// <summary>จำนวนเอกสารลงรับภายนอก (กระดาษ)</summary>
        public int ExternalPaperCount { get; set; }

        /// <summary>จำนวนเอกสารลงรับภายนอก (ดิจิทัล)</summary>
        public int ExternalDigitalCount { get; set; }

        /// <summary>จำนวนเอกสารส่งต่อ</summary>
        public int ForwardedCount { get; set; }

        public string RecordedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
