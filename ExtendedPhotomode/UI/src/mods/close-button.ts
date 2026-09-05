import { VT } from "vanilla/Components";

/**
 * The class for a panel close button, preferring vanilla's own.
 *
 * Reusing vanilla's class is what brings its hover, its circular hit area and its sizing — none of
 * which a styled "×" glyph had, and none of which is worth reimplementing by eye.
 *
 * Both spellings are tried because CS2's theme objects are not consistently camelCased: the class in
 * the DOM is `close-button_XXX`, and whether it surfaces as `closeButton` or `close-button` depends
 * on how that particular stylesheet was processed. Guessing one and shipping it is how you get a
 * silent fallback that looks almost right.
 *
 * The caller supplies its own class as the last resort, and that fallback is styled to match
 * vanilla's geometry — 24rem, circular, transparent — so the button is correct even when the theme
 * does not resolve at all.
 */
export function closeButtonClass(fallback: string): string {
    const theme = VT.panel as Record<string, string> | undefined;

    return theme?.closeButton ?? theme?.["close-button"] ?? fallback;
}
