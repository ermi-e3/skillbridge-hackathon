import { JobDto } from '../../core/models/job.models';

export type SalaryMode = 'range' | 'negotiable';
export type SalaryPeriod = 'month' | 'year' | 'project';

export function formatNumberWithCommas(val: number | string | null | undefined): string {
  if (val === null || val === undefined || val === '') return '';
  const cleaned = String(val).replace(/[^0-9]/g, '');
  if (!cleaned) return '';
  const num = Number(cleaned);
  return isNaN(num) ? '' : num.toLocaleString('en-US');
}

export function parseNumberFromCommas(val: string | null | undefined): number | null {
  if (!val) return null;
  const cleaned = String(val).replace(/[^0-9]/g, '');
  if (!cleaned) return null;
  const num = Number(cleaned);
  return isNaN(num) ? null : num;
}

export function formatCompensationText(
  mode: SalaryMode,
  minSalary: number | null | undefined,
  maxSalary: number | null | undefined,
  period: SalaryPeriod = 'month'
): string {
  if (mode === 'negotiable') {
    return 'Negotiable (ETB)';
  }

  const periodSuffix = period === 'year' ? '/ yr' : period === 'project' ? '/ proj' : '/ mo';

  if (minSalary && maxSalary && maxSalary > minSalary) {
    return `${minSalary.toLocaleString('en-US')} – ${maxSalary.toLocaleString('en-US')} ETB ${periodSuffix}`;
  }

  if (minSalary && maxSalary && maxSalary === minSalary) {
    return `${minSalary.toLocaleString('en-US')} ETB ${periodSuffix}`;
  }

  if (minSalary && !maxSalary) {
    return `${minSalary.toLocaleString('en-US')} ETB ${periodSuffix}`;
  }

  if (!minSalary && maxSalary) {
    return `Up to ${maxSalary.toLocaleString('en-US')} ETB ${periodSuffix}`;
  }

  return 'Competitive (ETB)';
}

export function extractSalaryFromJob(job: JobDto): { displayText: string; isNegotiable: boolean } {
  if (!job) return { displayText: 'Negotiable (ETB)', isNegotiable: true };

  // Check description for metadata markers
  if (job.description) {
    const match = job.description.match(/(?:\[Salary:\s*([^\]]+)\]|\*\*Compensation:\*\*\s*([^\n\r]+)|Compensation:\s*([^\n\r]+))/i);
    const rawMatch = match ? (match[1] || match[2] || match[3])?.trim() : null;
    if (rawMatch) {
      const isNeg = rawMatch.toLowerCase().includes('negotiable');
      return { displayText: rawMatch, isNegotiable: isNeg };
    }
  }

  // Realistic Ethiopian tech/professional market fallbacks based on role seniority
  switch (job.experienceLevel) {
    case 'Lead':
      return { displayText: '75,000 – 120,000 ETB / mo', isNegotiable: false };
    case 'Senior':
      return { displayText: '45,000 – 75,000 ETB / mo', isNegotiable: false };
    case 'MidLevel':
      return { displayText: '25,000 – 45,000 ETB / mo', isNegotiable: false };
    case 'Junior':
    case 'EntryLevel':
      return { displayText: '15,000 – 25,000 ETB / mo', isNegotiable: false };
    default:
      return { displayText: 'Negotiable (ETB)', isNegotiable: true };
  }
}

