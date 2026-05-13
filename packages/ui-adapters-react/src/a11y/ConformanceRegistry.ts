/**
 * React-side IConformanceRegistry implementation per ADR 0077 §7.
 * Mirrors DefaultConformanceRegistry from the Blazor adapter.
 * Idempotent on (locationId, surfaceId) — re-registering overwrites.
 */

export type Wcag22Level = 'A' | 'AA' | 'AAA';

export interface WcagSuccessCriterion {
  id: string;
  title: string;
}

export interface En301549Chapter {
  id: string;
  title: string;
}

export interface ConformanceException {
  reason: string;
  expiresAt?: string | null;
}

export interface ConformanceDeclaration {
  locationId: string;
  surfaceId: string;
  level: Wcag22Level;
  covered: WcagSuccessCriterion[];
  chapters: En301549Chapter[];
  exceptions: ConformanceException[];
  declaredAt: string; // ISO-8601
}

export interface IConformanceRegistry {
  register(declaration: ConformanceDeclaration): void;
  forLocation(locationId: string): ConformanceDeclaration[];
}

export class ConformanceRegistry implements IConformanceRegistry {
  private readonly _store = new Map<string, Map<string, ConformanceDeclaration>>();

  register(declaration: ConformanceDeclaration): void {
    const key = declaration.locationId.toLowerCase();
    if (!this._store.has(key)) {
      this._store.set(key, new Map());
    }
    this._store.get(key)!.set(declaration.surfaceId, declaration);
  }

  forLocation(locationId: string): ConformanceDeclaration[] {
    const byLocation = this._store.get(locationId.toLowerCase());
    return byLocation ? Array.from(byLocation.values()) : [];
  }
}

/** Singleton registry shared across the React application. */
export const conformanceRegistry: IConformanceRegistry = new ConformanceRegistry();
