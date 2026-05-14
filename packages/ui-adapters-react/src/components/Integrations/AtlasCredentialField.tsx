import { useId, useRef, useState } from 'react';
import type { CredentialFieldSpec } from '../../contracts/Integrations';
import { CredentialAutocompleteHint, CredentialFieldKind } from '../../contracts/Integrations';

export interface AtlasCredentialFieldProps {
  field: CredentialFieldSpec;
  hasExistingValue: boolean;
  onSensitiveChanged: (key: string, value: string) => void;
  onNonSensitiveChanged: (key: string, value: unknown) => void;
}

function getAutocomplete(hint: CredentialAutocompleteHint): string {
  switch (hint) {
    case CredentialAutocompleteHint.CurrentPassword: return 'current-password';
    case CredentialAutocompleteHint.NewPassword: return 'new-password';
    case CredentialAutocompleteHint.OneTimeCode: return 'one-time-code';
    case CredentialAutocompleteHint.Username: return 'username';
    case CredentialAutocompleteHint.Email: return 'email';
    case CredentialAutocompleteHint.Url: return 'url';
    default: return 'off';
  }
}

/**
 * Per-credential form field for the Atlas Integration-Config UI surface.
 * Mirrors `AtlasCredentialField.razor` (Anchor/Bridge Blazor).
 * WCAG: SC 1.3.1 (label for), SC 3.3.2 (required indicator + aria-describedby),
 * SC 3.3.7 (leave-unchanged placeholder for existing secrets), SC 3.3.8 (autocomplete).
 */
export function AtlasCredentialField({
  field,
  hasExistingValue,
  onSensitiveChanged,
  onNonSensitiveChanged,
}: AtlasCredentialFieldProps) {
  const inputId = useId();
  const descId = field.helpText ? `${inputId}-desc` : undefined;
  const [showSecret, setShowSecret] = useState(false);
  const [touched, setTouched] = useState(false);
  const [value, setValue] = useState('');
  const inputRef = useRef<HTMLInputElement>(null);

  const isSecret = field.kind === CredentialFieldKind.Secret;
  const isUrl = field.kind === CredentialFieldKind.Url;
  const isReadOnly = field.kind === CredentialFieldKind.ReadOnlyOutput;
  const inputType = isSecret && !showSecret ? 'password' : isUrl ? 'url' : 'text';
  const autocomplete = getAutocomplete(field.autocompleteHint);
  const showLeaveUnchanged = isSecret && hasExistingValue && !touched;

  function handleFocus() {
    if (showLeaveUnchanged) {
      setTouched(true);
      setValue('');
    }
  }

  function handleChange(e: React.ChangeEvent<HTMLInputElement>) {
    const next = e.target.value;
    setValue(next);
    if (isSecret) {
      onSensitiveChanged(field.key, next);
    } else {
      onNonSensitiveChanged(field.key, next);
    }
  }

  function toggleSecret() {
    setShowSecret((prev) => !prev);
    requestAnimationFrame(() => inputRef.current?.focus());
  }

  return (
    <div className="atlas-cred-field">
      <label htmlFor={inputId} className="atlas-cred-label">
        {field.displayLabel}
        {field.isRequired && (
          <>
            <span className="atlas-required-marker" aria-hidden="true"> *</span>
            <span className="sf-visually-hidden">(required)</span>
          </>
        )}
      </label>

      {descId && (
        <p id={descId} className="atlas-cred-help">{field.helpText}</p>
      )}

      {isReadOnly ? (
        <output id={inputId} className="atlas-cred-readonly" aria-describedby={descId}>
          {value}
        </output>
      ) : (
        <div className="atlas-cred-input-row">
          {showLeaveUnchanged ? (
            <input
              id={inputId}
              ref={inputRef}
              type="password"
              className="atlas-cred-input atlas-cred-input--unchanged"
              placeholder="••••••••"
              autoComplete={autocomplete}
              aria-describedby={descId}
              aria-label={`${field.displayLabel} — currently set. Type to replace, or leave blank to keep.`}
              onFocus={handleFocus}
              onChange={handleChange}
            />
          ) : (
            <input
              id={inputId}
              ref={inputRef}
              type={inputType}
              className="atlas-cred-input"
              value={value}
              placeholder={field.placeholder ?? undefined}
              autoComplete={autocomplete}
              aria-describedby={descId}
              required={field.isRequired}
              onChange={handleChange}
            />
          )}
          {isSecret && (
            <button
              type="button"
              className="atlas-cred-toggle"
              aria-pressed={showSecret}
              aria-controls={inputId}
              aria-label={showSecret ? `Hide ${field.displayLabel}` : `Show ${field.displayLabel}`}
              onClick={toggleSecret}
            >
              {showSecret ? 'Hide' : 'Show'}
            </button>
          )}
        </div>
      )}
    </div>
  );
}
