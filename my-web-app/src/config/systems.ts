/**
 * SYSTEMS — Single Source of Truth สำหรับทุกระบบใน Portal
 *
 * เพิ่มระบบใหม่ที่นี่ที่เดียว Portal, Admin Tool และหน้าจัดการสิทธิ์จะแสดงโดยอัตโนมัติ
 *
 * Fields:
 *  id              — รหัสระบบ (ใช้เป็น key ใน accessibleSystems / adminSystems)
 *  label           — ชื่อเต็มแสดงใน Portal
 *  shortLabel      — ชื่อย่อสำหรับ card สิทธิ์ (ถ้าไม่กำหนดจะใช้ label)
 *  desc            — คำอธิบายสำหรับผู้ใช้งานทั่วไป
 *  icon            — PrimeIcons class (ไม่ต้องใส่ "pi " นำหน้า)
 *  color           — ชื่อสี Tailwind (amber, cyan, rose, ...)
 *  cardBorder      — Tailwind border class สำหรับ card จัดการสิทธิ์ (border-l-xxx-400)
 *  path            — route หลักที่ Portal card จะพาไป
 *  accessRule      — กฎพิเศษ: 'maintenance' | 'adminTool' | undefined (ใช้ hasAccess(id) ปกติ)
 *  portalOnly      — true = แสดงใน Portal เท่านั้น ไม่แสดงใน Admin Tool
 *
 *  --- สำหรับ Admin Tool ---
 *  dataPath        — path หลักสำหรับ "จัดการข้อมูล" (ถ้าไม่กำหนดจะใช้ path)
 *  adminDesc       — คำอธิบายสำหรับ Admin / หน้าจัดการสิทธิ์ (ถ้าไม่กำหนดจะใช้ desc)
 *  masterDataPath  — path สำหรับ "ตั้งค่าหลัก / Master Data"
 *  adminPaths      — ลิงก์เพิ่มเติมใน Admin Tool
 */
export interface SystemConfig {
  id: string
  label: string
  shortLabel?: string
  desc: string
  icon: string
  color: string
  cardBorder: string
  path: string
  accessRule?: 'maintenance' | 'adminTool'
  portalOnly?: boolean
  // Admin Tool
  dataPath?: string
  adminDesc?: string
  masterDataPath?: string
  adminPaths?: Array<{ label: string; path: string }>
}

export const SYSTEMS: SystemConfig[] = [
  {
    id: 'system1',
    label: 'ค่าไฟฟ้า & Solar',
    shortLabel: 'ไฟฟ้า & Solar',
    desc: 'บันทึกบิลค่าไฟ กฟภ./กฟน. และพลังงาน Solar Cell',
    adminDesc: 'บิลค่าไฟ กฟภ./กฟน. และ Solar',
    icon: 'pi-bolt',
    color: 'amber',
    cardBorder: 'border-l-amber-400',
    path: '/electricity/dashboard',
    dataPath: '/electricity',
    masterDataPath: '/admin/buildings',
    adminPaths: [
      { label: 'แดชบอร์ดระบบ', path: '/electricity/dashboard' },
      { label: 'บันทึก Solar', path: '/electricity/solar' },
    ],
  },
  {
    id: 'system2',
    label: 'ค่าน้ำประปา',
    shortLabel: 'น้ำประปา',
    desc: 'บันทึกเลขมิเตอร์ คำนวณหน่วยน้ำ และสรุปค่าใช้จ่าย',
    adminDesc: 'บันทึกมิเตอร์และค่าน้ำ',
    icon: 'pi-wave-pulse',
    color: 'cyan',
    cardBorder: 'border-l-cyan-400',
    path: '/water/dashboard',
    dataPath: '/water',
    adminPaths: [{ label: 'แดชบอร์ดระบบ', path: '/water/dashboard' }],
  },
  {
    id: 'system3',
    label: 'น้ำมันเชื้อเพลิง',
    shortLabel: 'น้ำมันเชื้อเพลิง',
    desc: 'บันทึกการเบิกจ่ายน้ำมัน เลขไมล์ และวิเคราะห์การใช้รถ',
    adminDesc: 'การเติมน้ำมันและใบรับรอง',
    icon: 'pi-car',
    color: 'rose',
    cardBorder: 'border-l-rose-400',
    path: '/fuel/dashboard',
    dataPath: '/fuel',
    masterDataPath: '/admin/fuel-types',
    adminPaths: [
      { label: 'แดชบอร์ดระบบ', path: '/fuel/dashboard' },
      { label: 'ประวัติการเติมน้ำมัน', path: '/fuel/history' },
    ],
  },
  {
    id: 'system4',
    label: 'ค่าโทรศัพท์',
    shortLabel: 'ค่าโทรศัพท์',
    desc: 'บันทึกและวิเคราะห์ค่าใช้จ่ายโทรศัพท์รายเดือน',
    adminDesc: 'บันทึกค่าใช้จ่ายโทรศัพท์',
    icon: 'pi-phone',
    color: 'emerald',
    cardBorder: 'border-l-emerald-400',
    path: '/telephone/dashboard',
    dataPath: '/telephone',
    adminPaths: [{ label: 'แดชบอร์ดระบบ', path: '/telephone/dashboard' }],
  },
  {
    id: 'system5',
    label: 'สถิติงานสารบรรณ',
    shortLabel: 'สารบรรณ',
    desc: 'บันทึกและรายงานสถิติงานรับ-ส่งเอกสาร',
    adminDesc: 'สถิติงานรับ-ส่งเอกสาร',
    icon: 'pi-folder-open',
    color: 'violet',
    cardBorder: 'border-l-violet-400',
    path: '/saraban/dashboard',
    dataPath: '/saraban',
    adminPaths: [{ label: 'แดชบอร์ดระบบ', path: '/saraban/dashboard' }],
  },
  {
    id: 'system6',
    label: 'ระบบ IP-Phone',
    shortLabel: 'IP-Phone',
    desc: 'สถิติการโทรเข้า/ออก อัตราการรับสาย และวิเคราะห์ปริมาณสายรายเดือน',
    adminDesc: 'สถิติการโทรเข้า/ออกรายเดือน',
    icon: 'pi-desktop',
    color: 'teal',
    cardBorder: 'border-l-teal-400',
    path: '/ipphone/dashboard',
    dataPath: '/ipphone/dashboard',
    adminPaths: [
      { label: 'นำเข้าสถิติ CSV', path: '/ipphone/upload' },
      { label: 'ผูกผู้ใช้-เบอร์โทร', path: '/ipphone/mapping' },
    ],
  },
  {
    id: 'system12',
    label: 'สมุดโทรศัพท์องค์กร',
    shortLabel: 'สมุดโทรศัพท์',
    desc: 'ค้นหาเบอร์โทรศัพท์ภายใน IP-Phone และ Analog ของบุคลากรในองค์กร',
    adminDesc: 'ค้นหาเบอร์โทรภายใน IP-Phone / Analog',
    icon: 'pi-address-book',
    color: 'cyan',
    cardBorder: 'border-l-cyan-400',
    path: '/directory',
    dataPath: '/directory',
  },
  {
    id: 'system7',
    label: 'ระบบไปรษณีย์',
    shortLabel: 'ไปรษณีย์',
    desc: 'บันทึกสถิติการจัดส่งไปรษณีย์ ธรรมดา/ลงทะเบียน/EMS',
    adminDesc: 'สถิติจัดส่ง ธรรมดา/ลงทะเบียน/EMS',
    icon: 'pi-envelope',
    color: 'blue',
    cardBorder: 'border-l-blue-400',
    path: '/postal/dashboard',
    dataPath: '/postal',
    adminPaths: [{ label: 'แดชบอร์ดระบบ', path: '/postal/dashboard' }],
  },
  {
    id: 'system8',
    label: 'สถิติห้องประชุม',
    shortLabel: 'ห้องประชุม',
    desc: 'บันทึกและตรวจสอบสถิติการใช้งานห้องประชุมส่วนกลาง',
    adminDesc: 'สถิติการใช้ห้องประชุม',
    icon: 'pi-users',
    color: 'teal',
    cardBorder: 'border-l-indigo-400',
    path: '/meeting/dashboard',
    dataPath: '/meeting',
    masterDataPath: '/admin/meeting-rooms',
    adminPaths: [{ label: 'แดชบอร์ดระบบ', path: '/meeting/dashboard' }],
  },
  {
    id: 'system9',
    label: 'ระบบแจ้งซ่อมงานอาคาร',
    shortLabel: 'ซ่อมงานอาคาร',
    desc: 'แจ้งซ่อม ติดตามงาน ซ่อมภายใน/ภายนอก และประวัติสินทรัพย์',
    adminDesc: 'ใบงานซ่อม คลังอะไหล่ และช่างภายนอก',
    icon: 'pi-wrench',
    color: 'orange',
    cardBorder: 'border-l-orange-400',
    path: '/maintenance/dashboard',
    dataPath: '/maintenance/service',
    accessRule: 'maintenance',
    adminPaths: [
      { label: 'คลังอะไหล่', path: '/maintenance/spare-parts' },
      { label: 'Timeline ช่างภายนอก', path: '/maintenance/external-timeline' },
    ],
  },
  {
    id: 'system11',
    label: 'ระบบรถยนต์สำนักงาน',
    shortLabel: 'รถยนต์สำนักงาน',
    desc: 'บันทึกและจัดการข้อมูลรถยนต์สำนักงาน ทะเบียน ยี่ห้อ และผู้ใช้งาน',
    adminDesc: 'บันทึกรถยนต์ Officer = เพิ่ม/แก้ไข/ลบ',
    icon: 'pi-car',
    color: 'teal',
    cardBorder: 'border-l-teal-400',
    path: '/vehicle',
    dataPath: '/vehicle',
    adminPaths: [
      { label: 'จัดการหน่วยงาน', path: '/vehicle/departments' },
      { label: 'จัดการจังหวัด', path: '/vehicle/provinces' },
    ],
  },
  {
    id: 'system13',
    label: 'TV Dashboard',
    shortLabel: 'TV Dashboard',
    desc: 'ระบบแสดงสรุปข้อมูลสำนักงานบนจอ TV และจัดการเนื้อหาที่แสดง',
    adminDesc: 'จัดการ Dashboard สำหรับจอ TV',
    icon: 'pi-desktop',
    color: 'indigo',
    cardBorder: 'border-l-indigo-400',
    path: '/tv-dashboard',
    dataPath: '/tv-dashboard',
    accessRule: 'adminTool',
    portalOnly: true,
  },
  {
    id: 'system10',
    label: 'Admin Tool',
    shortLabel: 'Admin Tool',
    desc: 'ศูนย์รวมเครื่องมือผู้ดูแลระบบและการจัดการสิทธิ์ของทุกระบบ',
    adminDesc: 'เครื่องมือผู้ดูแลระบบและการกำหนดสิทธิ์',
    icon: 'pi-shield',
    color: 'violet',
    cardBorder: 'border-l-slate-400',
    path: '/admin/system-management',
    accessRule: 'adminTool',
    portalOnly: true,
  },
]

/** ระบบทั้งหมดยกเว้นที่ portalOnly (ใช้ใน Admin Tool) */
export const ADMIN_SYSTEMS = SYSTEMS.filter((s) => !s.portalOnly)
