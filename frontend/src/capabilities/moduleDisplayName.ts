export interface ModuleDisplaySource {
  readonly code: string;
  readonly displayName: string;
}

export function moduleDisplayName(
  modules: readonly ModuleDisplaySource[],
  code: string,
): string | undefined {
  return modules.find((module) => module.code === code)?.displayName;
}
