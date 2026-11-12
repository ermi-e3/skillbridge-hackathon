import { SkillDto } from '../../core/models/profile.models';
import { SKILL_CATEGORIES } from '../../features/candidate/onboarding/candidate-skill-onboarding.component';

export interface DepartmentOption {
  id: string;
  name: string;
}

const TECH_CATEGORY_IDS = new Set(['frontend', 'backend', 'database', 'devops', 'mobile']);

/**
 * Industry-wide categories/departments available across job posting, filtering, and discovery.
 */
export const DEPARTMENT_OPTIONS: DepartmentOption[] = [
  ...SKILL_CATEGORIES.filter((c) => c.id !== 'all' && !TECH_CATEGORY_IDS.has(c.id)).map((c) => ({
    id: c.id,
    name: c.name,
  })),
  { id: 'technology', name: 'Technology & Software' },
  { id: 'hr', name: 'Human Resources' },
  { id: 'legal', name: 'Legal & Compliance' },
  { id: 'creative', name: 'Creative & Design' },
  { id: 'general', name: 'Other / General' },
];

export const JOB_CATEGORY_NAMES: string[] = DEPARTMENT_OPTIONS.map((d) => d.name);

/**
 * Resolves all matching department IDs for a job based on its stored category or skills.
 */
export function getJobDepartments(
  requiredSkills: SkillDto[] = [],
  preferredSkills: SkillDto[] = [],
  category?: string | null
): string[] {
  const matchedIds = new Set<string>();

  if (category && category.trim()) {
    const normalizedCategory = category.trim().toLowerCase();
    const directMatch = DEPARTMENT_OPTIONS.find((d) => d.name.toLowerCase() === normalizedCategory);
    if (directMatch) {
      matchedIds.add(directMatch.id);
    }
  }

  const skillNames = new Set(
    [...requiredSkills, ...preferredSkills].map((s) => s.name.toLowerCase())
  );

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
 * Returns the primary human-readable department/category label for a job.
 */
export function getPrimaryJobDepartmentLabel(
  requiredSkills: SkillDto[] = [],
  preferredSkills: SkillDto[] = [],
  category?: string | null
): string | null {
  if (category && category.trim()) {
    return category.trim();
  }
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
  departmentId: string,
  category?: string | null
): boolean {
  if (!departmentId || departmentId === 'all') {
    return true;
  }
  const deptIds = getJobDepartments(requiredSkills, preferredSkills ?? [], category);
  return deptIds.includes(departmentId);
}
