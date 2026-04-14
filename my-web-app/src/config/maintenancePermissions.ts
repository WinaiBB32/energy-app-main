export const MAINTENANCE_TECHNICIAN_PERMISSION = 'maintenance:technician'
export const MAINTENANCE_SUPERVISOR_PERMISSION = 'maintenance:supervisor'
export const MAINTENANCE_ADMIN_BUILDING_PERMISSION = 'maintenance:adminbuilding'
export const MAINTENANCE_ADMIN_BUILDING_CENTRAL_PERMISSION = 'maintenance:adminbuilding:central'

export const MAINTENANCE_PERMISSION_KEYS = [
  MAINTENANCE_TECHNICIAN_PERMISSION,
  MAINTENANCE_SUPERVISOR_PERMISSION,
  MAINTENANCE_ADMIN_BUILDING_PERMISSION,
  MAINTENANCE_ADMIN_BUILDING_CENTRAL_PERMISSION,
] as const

export function hasMaintenancePermission(
  adminSystems: string[] | null | undefined,
  permission: string,
): boolean {
  return (adminSystems ?? []).includes(permission)
}
