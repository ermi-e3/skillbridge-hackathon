import { SkillDto } from '../../core/models/profile.models';
import { SKILL_CATEGORIES } from '../../features/candidate/onboarding/candidate-skill-onboarding.component';

export interface DepartmentOption {
  id: string;
  name: string;
}

const TECH_CATEGORY_IDS = new Set(['frontend', 'backend', 'database', 'devops', 'mobile']);

/**
 * Industry-wide departments derived from SKILL_CATEGORIES,
 * grouping software sub-disciplines into "Technology & Software".
 */
export const DEPARTMENT_OPTIONS: DepartmentOption[] = [
  ...SKILL_CATEGORIES.filter((c) => c.id !== 'all' && !TECH_CATEGORY_IDS.has(c.id)).map((c) => ({
    id: c.id,
    name: c.name,
  })),
  { id: 'technology', name: 'Technology & Software' },
];

/**
 * Resolves all matching department IDs for a job based on its required and preferred skills.
 */
export function getJobDepartments(
  requiredSkills: SkillDto[] = [],
  preferredSkills: SkillDto[] = []
): string[] {
  const skillNames = new Set(
    [...requiredSkills, ...preferredSkills].map((s) => s.name.toLowerCase())
  );

  if (skillNames.size === 0) {
    return [];
  }

  const matchedIds = new Set<string>();

  for (const group of SKILL_CATEGORIES) {
    if (group.id === 'all' || group.skillNames.length === 0) {
      continue;
    }

    const hasMatch = group.skillNames.some((name) => skillNames.has(name.toLowerCase()));
    if (hasMatch) {
      matchedIds.add(TECH_CATEGORY_IDS.has(group.id) ? 'technology' : group.id);
    }
  }

  return Array.from(matchedIds);
}

/**
 * Returns the primary human-readable department label for a job, or null if unmatched.
 */
export function getPrimaryJobDepartmentLabel(
  requiredSkills: SkillDto[] = [],
  preferredSkills: SkillDto[] = []
): string | null {
  const deptIds = getJobDepartments(requiredSkills, preferredSkills);
  if (deptIds.length === 0) {
    return null;
  }
  const match = DEPARTMENT_OPTIONS.find((d) => d.id === deptIds[0]);
  return match ? match.name : null;
}

/**
 * Checks whether a job belongs to the selected department ID.
 */
export function jobMatchesDepartment(
  requiredSkills: SkillDto[] = [],
  preferredSkills: SkillDto[] | undefined,
  departmentId: string
): boolean {
  if (!departmentId || departmentId === 'all') {
    return true;
  }
  const deptIds = getJobDepartments(requiredSkills, preferredSkills ?? []);
  return deptIds.includes(departmentId);
}

