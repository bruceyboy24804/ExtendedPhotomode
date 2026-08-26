import { bindValue, trigger, useValue } from "cs2/api";
import { Tooltip } from "cs2/ui";
import { type ReactElement, useEffect, useRef } from "react";
import mod from "mod.json";

const canGenerate$ = bindValue<boolean>(mod.id, "BINDING:canGenerateOrbit", false);

const shotType$ = bindValue<number>(mod.id, "BINDING:shotType", 1);

const ShotType = { Orbit: 1, DollyZoom: 2, Path: 3 } as const;

const kShots: Record<number, { readonly icon: string; readonly tooltip: string }> = {
    [ShotType.Orbit]: {
        icon: "coui://extendedphotomode/Camera_Icons/orbit.svg",
        tooltip:
            "Generate an orbit around a point ahead of the camera, using the Orbit Shot settings. " +
            "The camera stays where it is.",
    },
    [ShotType.DollyZoom]: {
        icon: "coui://extendedphotomode/Camera_Icons/camera-dolly.svg",
        tooltip:
            "Generate a dolly zoom around the orbit centre: the camera moves while the lens " +
            "counter-zooms, holding the subject the same size while the background rushes.",
    },
    [ShotType.Path]: {
        icon: "coui://extendedphotomode/Camera_Icons/orbit.svg",
        tooltip:
            "Generate a shot along the path drawn with the path tool, using the Path Shot settings. " +
            "Draw one with Ctrl+P first.",
    },
};

function generateShot(): void {
    trigger("audio", "playSound", "select-item", 1);
    trigger(mod.id, "TRIGGER:generateShot");
}

/**
 * The one button that generates whichever shot the Shot dropdown names.
 *
 * Deliberately one button rather than one per generator. The photo mode button row is a flex
 * container that already overflows, and every button added shrinks all of them — so a button per
 * shot type stops being usable at about three. Adding a generator is now an enum entry and a case in
 * `EPM_OrbitUISystem.GenerateShot`, with nothing to change here.
 *
 * Built from the vanilla Take Photo button's own markup rather than from `cs2/ui`'s `Button`, which
 * is Hall of Fame's approach: the panel's buttons carry generated class names we cannot reference,
 * so cloning one is the only way to match them exactly. `cs2/ui`'s `Button` also breaks focus inside
 * this panel — it logs a "second focus key" error against `PhotoModePanel`.
 *
 * The clone brings the vanilla button's own tooltip with it, anchored in the wrong place and with
 * text we cannot change. Wrapping the clone in our own `<Tooltip>` and hanging the click handler on
 * the wrapper is what works around that.
 */
export function GenerateShotButton({ html }: { readonly html: string }): ReactElement {
    const canGenerate = useValue(canGenerate$);
    const shotType = useValue(shotType$);
    const hostRef = useRef<HTMLElement>(null);

    const shot = kShots[shotType] ?? kShots[ShotType.Orbit];

    useEffect(() => {
        const button = hostRef.current?.firstElementChild;

        if (!(button instanceof HTMLButtonElement)) {
            console.warn("ExtendedPhotomode: expected the button template to contain a <button>.");
            return;
        }

        const icon = button.firstElementChild;

        if (icon instanceof HTMLElement) {
            const current = icon.style.maskImage.match(/url\(["']?([^"')]+)["']?\)/)?.[1] ?? "";
            const mediaIndex = current.indexOf("Media/");
            const prefix = mediaIndex > 0 ? current.slice(0, mediaIndex) : "";

            icon.style.maskImage = `url(${prefix}${shot.icon})`;
        }
    }, [shot.icon]);

    useEffect(() => {
        const button = hostRef.current?.firstElementChild;

        if (button instanceof HTMLButtonElement) {
            button.disabled = !canGenerate;
            button.style.opacity = canGenerate ? "" : "0.4";
        }
    }, [canGenerate]);

    return (
        <Tooltip
            tooltip={
                canGenerate
                    ? shot.tooltip
                    : "Open the cinematic camera panel first — the shot is written to its timeline."
            }
        >
            <span
                ref={hostRef}
                style={{ flexShrink: 1, minWidth: 0 }}
                onClick={canGenerate ? generateShot : undefined}
                dangerouslySetInnerHTML={{ __html: html }}
            />
        </Tooltip>
    );
}
