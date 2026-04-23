/**
 * Minimal icon-provider contract. Kept tiny in the Wave 3.5 scaffold —
 * the Blazor side has a richer icon pipeline with size / flip / theme-color
 * modifiers (see `ISunfishCssProvider.IconClass` in the C# contract).
 * Future waves will expand this to match.
 */
export interface IIconProvider {
  /**
   * Returns the CSS class string for an icon name. Each provider maps to
   * its framework's icon font vocabulary (Bootstrap Icons, Fluent UI
   * Icons, Material Symbols).
   */
  iconClass(iconName: string, size?: 'small' | 'medium' | 'large'): string;
}
