import { describe, expect, it } from 'vitest';
import {
  DimensionChangeKind,
  DimensionPassFail,
  DimensionPolicyKind,
  OverallVerdict,
  parseSystemRequirementsResult,
  type DimensionEvaluation,
  type SystemRequirementsResult,
} from './SystemRequirements';

// ===== Fixtures matching the C# JsonStringEnumConverter PascalCase output =====

const passDimensionEval: DimensionEvaluation = {
  dimension: DimensionChangeKind.Hardware,
  policy: DimensionPolicyKind.Required,
  outcome: DimensionPassFail.Pass,
};

const failDimensionEval: DimensionEvaluation = {
  dimension: DimensionChangeKind.Network,
  policy: DimensionPolicyKind.Recommended,
  outcome: DimensionPassFail.Fail,
  operatorRecoveryAction: { actionKey: 'connect-wifi', argumentMap: { ssid: 'Office-Net' } },
  detail: 'Network connectivity unavailable.',
};

const passResult: SystemRequirementsResult = {
  overall: OverallVerdict.Pass,
  dimensions: [passDimensionEval],
  evaluatedAt: '2026-05-12T00:00:00+00:00',
};

const blockResult: SystemRequirementsResult = {
  overall: OverallVerdict.Block,
  dimensions: [failDimensionEval],
  operatorRecoveryAction: { actionKey: 'contact-support' },
  evaluatedAt: '2026-05-12T01:00:00+00:00',
};

describe('SystemRequirements TypeScript contract', () => {
  describe('parseSystemRequirementsResult', () => {
    it('accepts a valid Pass result fixture', () => {
      const json = JSON.parse(JSON.stringify(passResult));
      const parsed = parseSystemRequirementsResult(json);
      expect(parsed.overall).toBe(OverallVerdict.Pass);
      expect(parsed.dimensions).toHaveLength(1);
      expect(parsed.dimensions[0].outcome).toBe(DimensionPassFail.Pass);
    });

    it('accepts a valid Block result with operatorRecoveryAction', () => {
      const json = JSON.parse(JSON.stringify(blockResult));
      const parsed = parseSystemRequirementsResult(json);
      expect(parsed.overall).toBe(OverallVerdict.Block);
      expect(parsed.operatorRecoveryAction?.actionKey).toBe('contact-support');
    });

    it('throws when "overall" field is absent', () => {
      const malformed = { dimensions: [], evaluatedAt: '2026-05-12T00:00:00+00:00' };
      expect(() => parseSystemRequirementsResult(malformed)).toThrow(
        /missing required field "overall"/,
      );
    });

    it('throws when "dimensions" field is absent', () => {
      const malformed = { overall: 'Pass', evaluatedAt: '2026-05-12T00:00:00+00:00' };
      expect(() => parseSystemRequirementsResult(malformed)).toThrow(
        /missing required field "dimensions"/,
      );
    });

    it('throws when "evaluatedAt" field is absent', () => {
      const malformed = { overall: 'Pass', dimensions: [] };
      expect(() => parseSystemRequirementsResult(malformed)).toThrow(
        /missing required field "evaluatedAt"/,
      );
    });

    it('throws for null input', () => {
      expect(() => parseSystemRequirementsResult(null)).toThrow(/expected object/);
    });

    it('throws for primitive input', () => {
      expect(() => parseSystemRequirementsResult('Pass')).toThrow(/expected object/);
    });
  });

  describe('DimensionChangeKind enum values match C# JsonStringEnumConverter output', () => {
    it('all 10 dimension keys are PascalCase strings', () => {
      expect(DimensionChangeKind.Hardware).toBe('Hardware');
      expect(DimensionChangeKind.User).toBe('User');
      expect(DimensionChangeKind.Regulatory).toBe('Regulatory');
      expect(DimensionChangeKind.Runtime).toBe('Runtime');
      expect(DimensionChangeKind.FormFactor).toBe('FormFactor');
      expect(DimensionChangeKind.Edition).toBe('Edition');
      expect(DimensionChangeKind.Network).toBe('Network');
      expect(DimensionChangeKind.TrustAnchor).toBe('TrustAnchor');
      expect(DimensionChangeKind.SyncState).toBe('SyncState');
      expect(DimensionChangeKind.VersionVector).toBe('VersionVector');
    });
  });

  describe('OverallVerdict enum values match C# wire format', () => {
    it('Pass / WarnOnly / Block are PascalCase', () => {
      expect(OverallVerdict.Pass).toBe('Pass');
      expect(OverallVerdict.WarnOnly).toBe('WarnOnly');
      expect(OverallVerdict.Block).toBe('Block');
    });
  });

  describe('round-trip JSON parsing', () => {
    it('survives JSON.stringify + JSON.parse for a full result', () => {
      const original: SystemRequirementsResult = {
        overall: OverallVerdict.WarnOnly,
        dimensions: [
          {
            dimension: DimensionChangeKind.TrustAnchor,
            policy: DimensionPolicyKind.Recommended,
            outcome: DimensionPassFail.Fail,
          },
        ],
        evaluatedAt: '2026-05-12T00:00:00+00:00',
      };
      const roundTripped = parseSystemRequirementsResult(JSON.parse(JSON.stringify(original)));
      expect(roundTripped.overall).toBe(OverallVerdict.WarnOnly);
      expect(roundTripped.dimensions[0].dimension).toBe(DimensionChangeKind.TrustAnchor);
    });
  });
});
