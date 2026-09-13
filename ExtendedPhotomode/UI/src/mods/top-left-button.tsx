import { useValue } from "cs2/api";
import { Button, Tooltip } from "cs2/ui";
import { type ReactElement } from "react";
import { panelOpenBinding } from "./path-bindings";

/**
 * The mod's button in the top-left toolbar, beside the game's own and every other mod's.
 *
 * Until now the only way in was Ctrl+P, which is a shortcut you have to already know. A button in
 * the row where players look for a mod's controls is how the other tool mods announce themselves,
 * and it is the same floating-button variant they use, so it sits in the row as one of them rather
 * than as something bolted on.
 *
 * The icon is the game's own cinematic camera glyph, so it reads as "the camera thing" before the
 * tooltip is even open. It toggles the path panel, which is the hub for every shot type.
 */
export function TopLeftButton(): ReactElement {
    const open = useValue(panelOpenBinding.binding);

    return (
        <Tooltip tooltip="Extended Photomode" direction="down">
            <Button
                variant="floating"
                selected={open}
                src="Media/Game/Icons/CinematicCamera.svg"
                onSelect={() => panelOpenBinding.set(!open)}
            />
        </Tooltip>
    );
}
