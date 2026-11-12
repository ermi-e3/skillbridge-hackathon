export type DatePreset = 'all' | 'today' | '7d' | '30d' | '90d' | 'custom';

export const DATE_PRESET_OPTIONS: { value: DatePreset; label: string }[] = [
  { value: 'all', label: 'Any time' },
  { value: 'today', label: 'Today' },
  { value: '7d', label: 'Last 7 days' },
  { value: '30d', label: 'Last 30 days' },
  { value: '90d', label: 'Last 90 days' },
  { value: 'custom', label: 'Custom range' },
];

/**
 * Checks whether an ISO timestamp falls within the specified date preset or custom date bounds.
 */
export function matchesDateFilter(
  isoDate: string | null | undefined,
  preset: DatePreset,
  fromDate?: string,
  toDate?: string
): boolean {
  if (preset === 'all' && !fromDate && !toDate) {
    return true;
  }

  if (!isoDate) {
    return false;
  }

  const targetTime = new Date(isoDate).getTime();
  if (Number.isNaN(targetTime)) {
    return false;
  }

  const now = new Date();

  if (preset === 'today') {
    const startOfToday = new Date(now.getFullYear(), now.getMonth(), now.getDate(), 0, 0, 0, 0).getTime();
    return targetTime >= startOfToday;
  }

  if (preset === '7d' || preset === '30d' || preset === '90d') {
    const days = preset === '7d' ? 7 : preset === '30d' ? 30 : 90;
    const cutoff = now.getTime() - days * 24 * 60 * 60 * 1000;
    return targetTime >= cutoff;
  }

  // Custom date range (or explicit bounds)
  if (fromDate) {
    const [year, month, day] = fromDate.split('-').map(Number);
    if (year && month && day) {
      const startBound = new Date(year, month - 1, day, 0, 0, 0, 0).getTime();
      if (targetTime < startBound) {
        return false;
      }
    }
  }

  if (toDate) {
    const [year, month, day] = toDate.split('-').map(Number);
    if (year && month && day) {
      const endBound = new Date(year, month - 1, day, 23, 59, 59, 999).getTime();
      if (targetTime > endBound) {
        return false;
      }
    }
  }

  return true;
}

